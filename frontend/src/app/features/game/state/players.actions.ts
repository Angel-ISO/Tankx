import { createAction, props } from '@ngrx/store';
import { PlayerMovementEvent, PlayerState } from '../models/game-events';

export const playerJoined = createAction(
  '[Game Hub] Player Joined',
  props<{ player: PlayerState }>()
);

export const playerMoved = createAction(
  '[Game Hub] Player Moved',
  props<{ movement: PlayerMovementEvent }>()
);
