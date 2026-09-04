import { createFeatureSelector, createSelector } from '@ngrx/store';
import { playersFeatureKey, PlayersState } from './players.reducer';

export const selectPlayersState = createFeatureSelector<PlayersState>(playersFeatureKey);

export const selectPlayers = createSelector(selectPlayersState, (state) =>
  Object.values(state.players)
);
