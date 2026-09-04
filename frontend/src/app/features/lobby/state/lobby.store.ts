import { DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';
import { GameSignalrService } from '../../../core/signalr/game-signalr.service';
import { ChatMessageEvent, ConnectionStatus } from '../../game/models/game-events';
import { PlayersStore } from '../../game/state/players.store';

interface LobbyState {
  connectionStatus: ConnectionStatus;
  messages: ChatMessageEvent[];
  errorMessage: string | null;
}

const initialState: LobbyState = {
  connectionStatus: 'disconnected',
  messages: [],
  errorMessage: null,
};

export const LobbyStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store, hub = inject(GameSignalrService), playersStore = inject(PlayersStore)) => ({
    async connect(playerName: string): Promise<void> {
      patchState(store, { connectionStatus: 'connecting', errorMessage: null });

      try {
        await hub.start(playerName);
        patchState(store, { connectionStatus: 'connected' });
      } catch {
        patchState(store, {
          connectionStatus: 'disconnected',
          errorMessage: 'Could not connect to the game server.',
        });
      }
    },

    async disconnect(): Promise<void> {
      await hub.stop();
      playersStore.clearPlayers();
      patchState(store, { connectionStatus: 'disconnected' });
    },

    async sendMessage(message: string): Promise<void> {
      const normalizedMessage = message.trim();
      if (!normalizedMessage || store.connectionStatus() !== 'connected') {
        return;
      }

      await hub.sendMessage(normalizedMessage);
    },
  })),
  withHooks((
    store,
    hub = inject(GameSignalrService),
    playersStore = inject(PlayersStore),
    destroyRef = inject(DestroyRef)
  ) => ({
    onInit(): void {
      toObservable(hub.connectionStatus)
        .pipe(takeUntilDestroyed(destroyRef))
        .subscribe((connectionStatus) => patchState(store, { connectionStatus }));

      hub.gameStateUpdated$
        .pipe(takeUntilDestroyed(destroyRef))
        .subscribe((snapshot) => playersStore.replacePlayers(snapshot.players));

      hub.playerJoined$
        .pipe(takeUntilDestroyed(destroyRef))
        .subscribe((player) => playersStore.addPlayer(player));

      hub.playerMoved$
        .pipe(takeUntilDestroyed(destroyRef))
        .subscribe((movement) => {
          playersStore.updatePlayerPosition(
            movement.playerId,
            movement.x,
            movement.y,
            movement.direction
          );
        });

      hub.playerLeft$
        .pipe(takeUntilDestroyed(destroyRef))
        .subscribe((playerId) => playersStore.removePlayer(playerId));

      hub.messageReceived$
        .pipe(takeUntilDestroyed(destroyRef))
        .subscribe((message) => {
          patchState(store, (state) => ({
            messages: [...state.messages.slice(-49), message],
          }));
        });
    },
  }))
);
