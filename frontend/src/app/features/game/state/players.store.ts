import { computed } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { MovementDirection, PlayerState, TankState } from '../models/game-events';

interface PlayersSignalState {
  entities: Record<string, TankState>;
}

const initialState: PlayersSignalState = {
  entities: {},
};

const initialScore = 0;

const initialAmmunition = 12;

const normalizePlayer = (player: PlayerState, existing?: TankState): TankState => ({
  ...player,
  score: player.score ?? existing?.score ?? initialScore,
  ammunition: player.ammunition ?? existing?.ammunition ?? initialAmmunition,
  tankType: player.tankType ?? existing?.tankType ?? 'tank_green',
  direction: player.direction ?? player.lastDirection ?? existing?.direction ?? 'up',
  status: player.status ?? existing?.status ?? (player.health <= 0 ? 'destroyed' : 'active'),
});

export const PlayersStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ entities }) => ({
    players: computed(() => Object.values(entities())),
    playerCount: computed(() => Object.keys(entities()).length),
  })),
  withMethods((store) => ({
    addPlayer(player: PlayerState): void {
      patchState(store, (state) => ({
        entities: {
          ...state.entities,
          [player.playerId]: normalizePlayer(player, state.entities[player.playerId]),
        },
      }));
    },

    removePlayer(playerId: string): void {
      patchState(store, (state) => {
        const { [playerId]: removedPlayer, ...remainingPlayers } = state.entities;
        void removedPlayer;
        return { entities: remainingPlayers };
      });
    },

    updatePlayerPosition(
      playerId: string,
      x: number,
      y: number,
      lastDirection?: MovementDirection,
    ): void {
      patchState(store, (state) => {
        const player = state.entities[playerId];
        if (!player) {
          return state;
        }

        return {
          entities: {
            ...state.entities,
            [playerId]: {
              ...player,
              x,
              y,
              lastDirection,
              direction: lastDirection ?? player.direction,
            },
          },
        };
      });
    },

    replacePlayers(players: PlayerState[]): void {
      patchState(store, (state) => ({
        entities: Object.fromEntries(
          players.map((player) => [
            player.playerId,

            normalizePlayer(player, state.entities[player.playerId]),
          ]),
        ),
      }));
    },

    incrementPlayerScore(playerId: string, points: number): void {
      if (points <= 0) {
        return;
      }

      patchState(store, (state) => {
        const player = state.entities[playerId];

        if (!player) {
          return state;
        }

        return {
          entities: {
            ...state.entities,

            [playerId]: {
              ...player,

              score: (player.score ?? initialScore) + points,
            },
          },
        };
      });
    },

    useAmmunition(playerId: string): boolean {
      const player = store.entities()[playerId];

      const ammunition = player?.ammunition ?? initialAmmunition;

      if (!player || ammunition <= 0) {
        return false;
      }

      patchState(store, {
        entities: {
          ...store.entities(),

          [playerId]: { ...player, ammunition: ammunition - 1 },
        },
      });

      return true;
    },

    updatePlayerHealth(playerId: string, health: number): void {
      const player = store.entities()[playerId];
      if (!player) return;

      const normalizedHealth = Math.max(0, Math.min(100, health));
      patchState(store, {
        entities: {
          ...store.entities(),
          [playerId]: {
            ...player,
            health: normalizedHealth,
            status: normalizedHealth === 0 ? 'destroyed' : 'active',
          },
        },
      });
    },

    clearPlayers(): void {
      patchState(store, initialState);
    },
  })),
);
