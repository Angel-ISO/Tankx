import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  OnInit,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';

import { ProfileService } from '../../core/profile/profile.service';
import { GameSignalrService } from '../../core/signalr/game-signalr.service';
import { SpriteService } from '../../core/sprites/sprite.service';
import { GameChatComponent } from './game-chat.component';
import { BulletState, MatchEndedEvent, MovementDirection, TankState } from './models/game-events';
import { MapCell, MapExplosion, MapStore } from './state/map.store';
import { PlayersStore } from './state/players.store';

const cellSize = 64;
const tankSize = 42;
const terrainSprites = ['tileGrass1.png', 'tileGrass2.png'] as const;
const indestructibleSprites = ['barricadeMetal.png', 'crateMetal.png'] as const;
const destructibleSprites = [
  { name: 'crateWood.png', width: 54, height: 54 },
  { name: 'barricadeWood.png', width: 56, height: 56 },
  { name: 'barrelGreen_top.png', width: 46, height: 46 },
  { name: 'barrelRed_top.png', width: 46, height: 46 },
  { name: 'barrelRust_top.png', width: 46, height: 46 },
  { name: 'sandbagBeige_open.png', width: 58, height: 42 },
  { name: 'sandbagBrown_open.png', width: 58, height: 42 },
] as const;

@Component({
  selector: 'app-game',
  standalone: true,
  imports: [GameChatComponent],
  templateUrl: './game.component.html',
  styleUrl: './game.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GameComponent implements OnInit {
  private readonly gameSignalr = inject(GameSignalrService);
  private readonly profiles = inject(ProfileService);
  private readonly router = inject(Router);
  private readonly spriteService = inject(SpriteService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly battlefield = viewChild<ElementRef<HTMLCanvasElement>>('battlefield');
  private readonly canvasViewport = viewChild<ElementRef<HTMLDivElement>>('canvasViewport');

  protected readonly playersStore = inject(PlayersStore);
  protected readonly mapStore = inject(MapStore);
  protected readonly playerName = computed(
    () => this.profiles.profile()?.displayName ?? this.gameSignalr.playerName,
  );
  protected readonly connectionStatus = this.gameSignalr.connectionStatus;
  protected readonly matchEnded = signal<MatchEndedEvent | null>(null);
  protected readonly combatMessage = signal('Move into position and fire with Space.');
  protected readonly localPlayer = computed<TankState>(
    () =>
      this.playersStore
        .players()
        .find((player) => player.playerId === this.gameSignalr.playerId) ?? {
        playerId: this.gameSignalr.playerId,
        playerName: this.playerName(),
        ...this.gameSignalr.localPosition(),
        health: 100,
        score: 0,
        ammunition: 12,
        kills: 0,
        deaths: 0,
        direction: 'up',
        status: 'active',
        isConnected: true,
        isHost: false,
        tankType: this.profiles.profile()?.tankType ?? 'tank_green',
      },
  );
  protected readonly healthHearts = computed(() =>
    Array.from({ length: Math.ceil(Math.max(0, this.localPlayer().health) / 20) }),
  );
  protected readonly ammunitionIcons = computed(() =>
    Array.from({ length: Math.max(0, this.localPlayer().ammunition ?? 0) }),
  );

  private readonly renderArena = effect(() => {
    const canvas = this.battlefield()?.nativeElement;
    const cells = this.mapStore.cells();
    const explosions = this.mapStore.explosions();
    const players = this.playersStore.players();
    const bullets = this.gameSignalr.gameSnapshot()?.bullets ?? [];

    if (canvas) {
      this.drawArena(canvas, cells, explosions, players, bullets);
      const localPlayer = players.find((player) => player.playerId === this.gameSignalr.playerId);
      if (localPlayer) this.centerViewport(localPlayer);
    }
  });

  async ngOnInit(): Promise<void> {
    this.gameSignalr.gameStateUpdated$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((snapshot) => this.playersStore.replacePlayers(snapshot.players));
    this.gameSignalr.matchEnded$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => this.matchEnded.set(event));

    try {
      await this.spriteService.loadSprites();
      const profile = await this.profiles.getOrCreate();
      await this.gameSignalr.start(profile.displayName);
      const snapshot = this.gameSignalr.gameSnapshot();
      if (snapshot) this.playersStore.replacePlayers(snapshot.players);
    } catch (error) {
      console.error('[Game] Initialization error:', error);
    }
  }

  @HostListener('window:keydown', ['$event'])
  handleKeydown(event: KeyboardEvent): void {
    const target = event.target;
    if (
      target instanceof HTMLInputElement ||
      target instanceof HTMLTextAreaElement ||
      target instanceof HTMLSelectElement ||
      (target instanceof HTMLElement && target.isContentEditable) ||
      (event.code === 'Space' && target instanceof HTMLButtonElement)
    ) {
      return;
    }

    if (event.code === 'Space') {
      event.preventDefault();
      if (!event.repeat) this.fireBullet();
      return;
    }

    const direction = this.mapKeyToDirection(event.key);
    if (!direction) return;

    event.preventDefault();
    void this.move(direction);
  }

  async move(direction: MovementDirection): Promise<void> {
    const player = this.localPlayer();
    if (player.status === 'destroyed') return;

    try {
      await this.gameSignalr.sendMovement(direction);
    } catch (error) {
      console.error('[SignalR] Could not send movement', error);
    }
  }

  protected async fireBullet(): Promise<void> {
    const player = this.localPlayer();
    if (player.status === 'destroyed') return;
    try {
      await this.gameSignalr.shoot();
      this.combatMessage.set(`Projectile fired ${player.direction}.`);
    } catch (error) {
      console.error('[SignalR] Could not fire', error);
    }
  }

  protected async returnToLobby(): Promise<void> {
    try {
      await this.gameSignalr.leaveRoom();
    } finally {
      this.playersStore.clearPlayers();
      this.mapStore.resetMap();
      await this.router.navigate(['/lobby']);
    }
  }

  private drawArena(
    canvas: HTMLCanvasElement,
    cells: MapCell[][],
    explosions: MapExplosion[],
    players: TankState[],
    bullets: BulletState[],
  ): void {
    const width = (cells[0]?.length ?? 0) * cellSize;
    const height = cells.length * cellSize;
    if (canvas.width !== width) canvas.width = width;
    if (canvas.height !== height) canvas.height = height;

    const context = canvas.getContext('2d');
    if (!context) return;

    cells.forEach((row, rowIndex) => {
      row.forEach((cell, columnIndex) => {
        this.drawCell(context, cell, columnIndex * cellSize, rowIndex * cellSize);
      });
    });

    for (const explosion of explosions) {
      const x = explosion.column * cellSize;
      const y = explosion.row * cellSize;
      const explosionFrame = Math.floor((Date.now() / 100) % 5) + 1;
      const explosionSprite = this.spriteService.getExplosionSprite(explosionFrame);
      this.spriteService.drawSprite(context, explosionSprite, x, y, {
        width: cellSize,
        height: cellSize,
      });
    }

    for (const bullet of bullets) {
      const owner = players.find((player) => player.playerId === bullet.ownerId);
      const bulletColor = this.spriteService.getTankBaseColor(owner?.tankType ?? 'tank_green');
      const bulletSprite = this.spriteService.getBulletSprite(bulletColor, 2, true);
      const bulletWidth = 12;
      const bulletHeight = 16;
      this.spriteService.drawSprite(
        context,
        bulletSprite,
        bullet.x - bulletWidth / 2,
        bullet.y - bulletHeight / 2,
        {
          width: bulletWidth,
          height: bulletHeight,
          rotation: this.getRotationForDirection(bullet.direction),
        },
      );
    }

    for (const player of players) {
      this.drawTank(context, player, canvas.width, canvas.height);
    }
  }

  private drawTank(
    context: CanvasRenderingContext2D,
    player: TankState,
    mapWidth: number,
    mapHeight: number,
  ): void {
    const x = Math.min(mapWidth - tankSize, Math.max(0, player.x));
    const y = Math.min(mapHeight - tankSize, Math.max(0, player.y));
    const isLocal = player.playerId === this.gameSignalr.playerId;

    const tankSpriteName = this.spriteService.getTankSprite(player.tankType);
    const tankSprite = this.spriteService.getSprite(tankSpriteName);

    // Draw the full tank sprite (body + barrel) preserving its aspect ratio,
    // rotated to face the player's movement direction.
    // The sprite art has the barrel pointing down, so an extra half-turn
    // aligns it with the direction of travel.
    const drawHeight = tankSize;
    const drawWidth = tankSprite ? (tankSprite.width / tankSprite.height) * drawHeight : tankSize;
    const drawX = x + (tankSize - drawWidth) / 2;
    const drawY = y + (tankSize - drawHeight) / 2;

    this.spriteService.drawSprite(context, tankSpriteName, drawX, drawY, {
      width: drawWidth,
      height: drawHeight,
      rotation: this.getRotationForDirection(player.direction) + Math.PI,
    });

    // Draw destroyed state
    if (player.status === 'destroyed') {
      context.strokeStyle = '#ff5147';
      context.lineWidth = 3;
      context.beginPath();
      context.moveTo(x + 5, y + 5);
      context.lineTo(x + tankSize - 5, y + tankSize - 5);
      context.moveTo(x + tankSize - 5, y + 5);
      context.lineTo(x + 5, y + tankSize - 5);
      context.stroke();
    }

    // Draw player name
    context.fillStyle = '#ffffff';
    context.font = '12px monospace';
    context.fillText(player.playerName, x - 4, y + tankSize + 12);
  }

  private getRotationForDirection(direction: MovementDirection): number {
    switch (direction) {
      case 'up':
        return 0;
      case 'right':
        return Math.PI / 2;
      case 'down':
        return Math.PI;
      case 'left':
        return -Math.PI / 2;
    }
  }

  private centerViewport(player: TankState): void {
    const viewport = this.canvasViewport()?.nativeElement;
    if (!viewport) return;

    viewport.scrollTo({
      left: Math.max(0, player.x - viewport.clientWidth / 2),
      top: Math.max(0, player.y - viewport.clientHeight / 2),
      behavior: 'smooth',
    });
  }

  private drawCell(context: CanvasRenderingContext2D, cell: MapCell, x: number, y: number): void {
    const column = x / cellSize;
    const row = y / cellSize;
    const variant = row * 31 + column * 17;
    const terrainSprite = terrainSprites[variant % terrainSprites.length];
    this.spriteService.drawSprite(context, terrainSprite, x, y, {
      width: cellSize,
      height: cellSize,
    });

    if (cell === 1) {
      const obstacleSprite = indestructibleSprites[variant % indestructibleSprites.length];
      const obstacleSize = 56;
      this.spriteService.drawSprite(
        context,
        obstacleSprite,
        x + (cellSize - obstacleSize) / 2,
        y + (cellSize - obstacleSize) / 2,
        {
          width: obstacleSize,
          height: obstacleSize,
        },
      );
    } else if (cell === 2) {
      const obstacle = destructibleSprites[variant % destructibleSprites.length];
      this.spriteService.drawSprite(
        context,
        obstacle.name,
        x + (cellSize - obstacle.width) / 2,
        y + (cellSize - obstacle.height) / 2,
        {
          width: obstacle.width,
          height: obstacle.height,
        },
      );
    }
  }

  private mapKeyToDirection(key: string): MovementDirection | null {
    switch (key) {
      case 'ArrowUp':
      case 'w':
      case 'W':
        return 'up';
      case 'ArrowDown':
      case 's':
      case 'S':
        return 'down';
      case 'ArrowLeft':
      case 'a':
      case 'A':
        return 'left';
      case 'ArrowRight':
      case 'd':
      case 'D':
        return 'right';
      default:
        return null;
    }
  }
}
