import { createReducer, on } from '@ngrx/store';
import { PlayerState } from '../models/game-events';
import { playerJoined, playerMoved } from './players.actions';

export const playersFeatureKey = 'players';

export interface PlayersState {
  players: Record<string, PlayerState>;
}

export const initialPlayersState: PlayersState = {
  players: {},
};

export const playersReducer = createReducer(
  initialPlayersState,
  on(playerJoined, (state, { player }) => ({
    ...state,
    players: {
      ...state.players,
      [player.playerId]: player,
    },
  })),
  on(playerMoved, (state, { movement }) => ({
    ...state,
    players: {
      ...state.players,
      [movement.playerId]: {
        ...state.players[movement.playerId],
        playerId: movement.playerId,
        playerName: movement.playerName,
        x: movement.x,
        y: movement.y,
        lastDirection: movement.direction,
        direction: movement.direction,
      },
    },
  }))
);
