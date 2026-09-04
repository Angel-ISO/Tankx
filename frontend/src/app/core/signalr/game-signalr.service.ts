import { Injectable, InjectionToken, NgZone, inject, signal } from '@angular/core';
import { Store } from '@ngrx/store';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  BlockDestroyedEvent,
  ChatMessageEvent,
  ConnectionStatus,
  GameStateSnapshot,
  MatchEndedEvent,
  MatchStartedEvent,
  MovementDirection,
  PlayerJoinedEvent,
  PlayerMovementEvent,
  RoomSummary,
} from '../../features/game/models/game-events';
import { playerJoined, playerMoved } from '../../features/game/state/players.actions';
import { AuthService } from '../auth/auth.service';

export type GameHubConnection = Pick<
  signalR.HubConnection,
  'invoke' | 'on' | 'onclose' | 'onreconnected' | 'onreconnecting' | 'start' | 'state' | 'stop'
>;

export const GAME_HUB_CONNECTION_FACTORY = new InjectionToken<() => GameHubConnection>(
  'GameHubConnectionFactory',
  {
    providedIn: 'root',
    factory: () => {
      const auth = inject(AuthService);
      return () =>
        new signalR.HubConnectionBuilder()
          .withUrl(environment.gameHubUrl, {
            accessTokenFactory: () => auth.accessToken() ?? '',
          })
          .withAutomaticReconnect()
          .build();
    },
  },
);

@Injectable({ providedIn: 'root' })
export class GameSignalrService {
  private readonly auth = inject(AuthService);
  private readonly store = inject(Store);
  private readonly zone = inject(NgZone);
  private readonly connection = inject(GAME_HUB_CONNECTION_FACTORY)();
  private readonly connectionStatusSignal = signal<ConnectionStatus>('disconnected');
  private readonly localPositionSignal = signal({ x: 64, y: 64 });
  private readonly roomsSignal = signal<RoomSummary[]>([]);
  private readonly currentRoomSignal = signal<RoomSummary | null>(null);
  private readonly gameSnapshotSignal = signal<GameStateSnapshot | null>(null);
  private readonly playerJoinedSubject = new Subject<PlayerJoinedEvent>();
  private readonly playerMovedSubject = new Subject<PlayerMovementEvent>();
  private readonly playerLeftSubject = new Subject<string>();
  private readonly messageReceivedSubject = new Subject<ChatMessageEvent>();
  private readonly gameStateUpdatedSubject = new Subject<GameStateSnapshot>();
  private readonly blockDestroyedSubject = new Subject<BlockDestroyedEvent>();
  private readonly mapStateUpdatedSubject = new Subject<BlockDestroyedEvent[]>();
  private readonly matchStartedSubject = new Subject<MatchStartedEvent>();
  private readonly matchEndedSubject = new Subject<MatchEndedEvent>();

  readonly connectionStatus = this.connectionStatusSignal.asReadonly();
  readonly localPosition = this.localPositionSignal.asReadonly();
  readonly rooms = this.roomsSignal.asReadonly();
  readonly currentRoom = this.currentRoomSignal.asReadonly();
  readonly gameSnapshot = this.gameSnapshotSignal.asReadonly();
  readonly playerJoined$ = this.playerJoinedSubject.asObservable();
  readonly playerMoved$ = this.playerMovedSubject.asObservable();
  readonly playerLeft$ = this.playerLeftSubject.asObservable();
  readonly messageReceived$ = this.messageReceivedSubject.asObservable();
  readonly gameStateUpdated$ = this.gameStateUpdatedSubject.asObservable();
  readonly blockDestroyed$ = this.blockDestroyedSubject.asObservable();
  readonly mapStateUpdated$ = this.mapStateUpdatedSubject.asObservable();
  readonly matchStarted$ = this.matchStartedSubject.asObservable();
  readonly matchEnded$ = this.matchEndedSubject.asObservable();

  get playerId(): string {
    return this.auth.user()?.id ?? '';
  }

  get playerName(): string {
    return (
      this.gameSnapshot()?.players.find((player) => player.playerId === this.playerId)?.playerName ??
      'Pilot'
    );
  }

  constructor() {
    this.registerServerEvents();
    this.registerConnectionLifecycle();
  }

  async start(_playerName?: string): Promise<void> {
    if (this.connection.state !== signalR.HubConnectionState.Disconnected) return;
    this.connectionStatusSignal.set('connecting');
    try {
      await this.connection.start();
      this.connectionStatusSignal.set('connected');
      await this.refreshRooms();
      const previousRoomCode = sessionStorage.getItem('tankx-room-code');
      if (previousRoomCode) {
        try {
          await this.joinRoom(previousRoomCode);
        } catch {
          sessionStorage.removeItem('tankx-room-code');
        }
      }
    } catch (error) {
      this.connectionStatusSignal.set('disconnected');
      throw error;
    }
  }

  async stop(): Promise<void> {
    await this.connection.stop();
    this.connectionStatusSignal.set('disconnected');
  }

  async refreshRooms(): Promise<void> {
    await this.startIfNeeded();
    this.roomsSignal.set(await this.connection.invoke<RoomSummary[]>('ListRooms'));
  }

  async createRoom(maxPlayers: number, password?: string): Promise<RoomSummary> {
    await this.startIfNeeded();
    const room = await this.connection.invoke<RoomSummary>('CreateRoom', {
      maxPlayers,
      mapName: 'battle-city',
      password: password?.trim() || null,
    });
    this.currentRoomSignal.set(room);
    sessionStorage.setItem('tankx-room-code', room.roomCode);
    return room;
  }

  async joinRoom(roomCode: string, password?: string): Promise<RoomSummary> {
    await this.startIfNeeded();
    const room = await this.connection.invoke<RoomSummary>('JoinRoom', {
      roomCode: roomCode.trim().toUpperCase(),
      password: password?.trim() || null,
    });
    this.currentRoomSignal.set(room);
    sessionStorage.setItem('tankx-room-code', room.roomCode);
    return room;
  }

  async leaveRoom(): Promise<void> {
    await this.connection.invoke('LeaveRoom');
    this.currentRoomSignal.set(null);
    this.gameSnapshotSignal.set(null);
    sessionStorage.removeItem('tankx-room-code');
    await this.refreshRooms();
  }

  async startMatch(): Promise<void> {
    await this.connection.invoke('StartMatch');
  }

  async sendMovement(direction: MovementDirection): Promise<void> {
    await this.connection.invoke('SendMovement', { direction });
  }

  async shoot(): Promise<void> {
    await this.connection.invoke('Shoot');
  }

  async sendMessage(message: string): Promise<void> {
    await this.connection.invoke('SendMessage', { message: message.trim() });
  }

  private async startIfNeeded(): Promise<void> {
    if (this.connection.state === signalR.HubConnectionState.Disconnected) await this.start();
  }

  private registerServerEvents(): void {
    this.connection.on('RoomsUpdated', (rooms: RoomSummary[]) =>
      this.zone.run(() => this.roomsSignal.set(rooms)),
    );
    this.connection.on('PlayerJoined', (player: PlayerJoinedEvent) => {
      this.zone.run(() => {
        this.store.dispatch(playerJoined({ player }));
        this.playerJoinedSubject.next(player);
      });
    });
    this.connection.on('PlayerMoved', (movement: PlayerMovementEvent) => {
      this.zone.run(() => {
        this.store.dispatch(playerMoved({ movement }));
        this.playerMovedSubject.next(movement);
      });
    });
    this.connection.on('PlayerLeft', (playerId: string) =>
      this.zone.run(() => this.playerLeftSubject.next(playerId)),
    );
    this.connection.on('ReceiveMessage', (message: ChatMessageEvent) =>
      this.zone.run(() => this.messageReceivedSubject.next(message)),
    );
    this.connection.on('BlockDestroyed', (block: BlockDestroyedEvent) =>
      this.zone.run(() => this.blockDestroyedSubject.next(block)),
    );
    this.connection.on('GameStateUpdated', (snapshot: GameStateSnapshot) => {
      this.zone.run(() => {
        this.gameSnapshotSignal.set(snapshot);
        this.currentRoomSignal.update((room) =>
          room
            ? { ...room, playerCount: snapshot.players.length, status: snapshot.status }
            : room,
        );
        const local = snapshot.players.find((player) => player.playerId === this.playerId);
        if (local) this.localPositionSignal.set({ x: local.x, y: local.y });
        this.mapStateUpdatedSubject.next(snapshot.destroyedBlocks);
        this.gameStateUpdatedSubject.next(snapshot);
      });
    });
    this.connection.on('MatchStarted', (event: MatchStartedEvent) =>
      this.zone.run(() => this.matchStartedSubject.next(event)),
    );
    this.connection.on('MatchEnded', (event: MatchEndedEvent) =>
      this.zone.run(() => this.matchEndedSubject.next(event)),
    );
  }

  private registerConnectionLifecycle(): void {
    this.connection.onreconnecting(() =>
      this.zone.run(() => this.connectionStatusSignal.set('reconnecting')),
    );
    this.connection.onreconnected(() => {
      this.zone.run(() => this.connectionStatusSignal.set('connected'));
      const roomCode = this.currentRoom()?.roomCode;
      if (roomCode) void this.joinRoom(roomCode);
      else void this.refreshRooms();
    });
    this.connection.onclose(() =>
      this.zone.run(() => this.connectionStatusSignal.set('disconnected')),
    );
  }
}
