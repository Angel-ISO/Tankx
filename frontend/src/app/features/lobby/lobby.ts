import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { ProfileService } from '../../core/profile/profile.service';
import { GameSignalrService } from '../../core/signalr/game-signalr.service';
import { PlayersStore } from '../game/state/players.store';
import { LobbyStore } from './state/lobby.store';

@Component({
  selector: 'app-lobby',
  imports: [FormsModule],
  templateUrl: './lobby.html',
  styleUrl: './lobby.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Lobby implements OnInit {
  private readonly profiles = inject(ProfileService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly store = inject(LobbyStore);
  protected readonly gameHub = inject(GameSignalrService);
  protected readonly playersStore = inject(PlayersStore);
  protected readonly profile = this.profiles.profile;
  protected readonly profileError = signal<string | null>(null);
  protected readonly chatMessage = signal('');
  protected readonly maxPlayers = signal(4);
  protected readonly roomPassword = signal('');
  protected readonly joinCode = signal('');
  protected readonly joinPassword = signal('');
  protected readonly roomError = signal<string | null>(null);

  ngOnInit(): void {
    this.gameHub.matchStarted$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.enterGame());
    void this.connect();
  }

  protected async connect(): Promise<void> {
    this.profileError.set(null);
    try {
      const profile = await this.profiles.getOrCreate();
      await this.store.connect(profile.displayName);
    } catch {
      this.profileError.set('Could not load your pilot profile.');
    }
  }

  protected async sendChat(): Promise<void> {
    const message = this.chatMessage();
    await this.store.sendMessage(message);
    this.chatMessage.set('');
  }

  protected enterGame(): void {
    void this.router.navigate(['/game']);
  }

  protected async createRoom(): Promise<void> {
    await this.runRoomOperation(() =>
      this.gameHub.createRoom(this.maxPlayers(), this.roomPassword()),
    );
  }

  protected async joinRoom(roomCode = this.joinCode()): Promise<void> {
    await this.runRoomOperation(() => this.gameHub.joinRoom(roomCode, this.joinPassword()));
  }

  protected async leaveRoom(): Promise<void> {
    await this.runRoomOperation(() => this.gameHub.leaveRoom());
  }

  protected async startMatch(): Promise<void> {
    await this.runRoomOperation(() => this.gameHub.startMatch());
  }

  private async runRoomOperation(operation: () => Promise<unknown>): Promise<void> {
    this.roomError.set(null);
    try {
      await operation();
    } catch (error) {
      this.roomError.set(error instanceof Error ? error.message : 'Room operation failed.');
    }
  }
}
