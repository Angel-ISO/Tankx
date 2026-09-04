import { PlayerState } from '../models/game-events';
import { playerJoined, playerMoved } from './players.actions';
import { initialPlayersState, playersReducer } from './players.reducer';

const player: PlayerState = {
  playerId: 'p1',
  playerName: 'Player 1',
  tankType: 'tank_green',
  x: 100,
  y: 100,
  health: 100,
  score: 0,
  ammunition: 12,
  kills: 0,
  deaths: 0,
  direction: 'up',
  status: 'active',
  isConnected: true,
  isHost: false,
};

describe('playersReducer', () => {
  it('adds a connected player', () => {
    const state = playersReducer(initialPlayersState, playerJoined({ player }));
    expect(state.players['p1']).toEqual(player);
  });

  it('updates a player when a server-confirmed movement arrives', () => {
    const joined = playersReducer(initialPlayersState, playerJoined({ player }));
    const state = playersReducer(
      joined,
      playerMoved({
        movement: {
          playerId: 'p1',
          playerName: 'Player 1',
          direction: 'right',
          x: 116,
          y: 100,
        },
      }),
    );

    expect(state.players['p1']).toEqual({
      ...player,
      x: 116,
      direction: 'right',
      lastDirection: 'right',
    });
  });
});
