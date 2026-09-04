import { TestBed } from '@angular/core/testing';
import { PlayersStore } from './players.store';

declare const expect: any;
declare const describe: any;
declare const it: any;
declare const beforeEach: any;

describe('PlayersStore', () => {
  let store: InstanceType<typeof PlayersStore>;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [PlayersStore] });
    store = TestBed.inject(PlayersStore);
  });

  const player = (health = 100) => ({
    playerId: 'player-1',
    playerName: 'Angel',
    tankType: 'tank_green',
    x: 100,
    y: 100,
    health,
    score: 0,
    ammunition: 12,
    kills: 0,
    deaths: 0,
    direction: 'up' as const,
    status: 'active' as const,
    isConnected: true,
    isHost: true,
  });

  it('adds a player', () => {
    store.addPlayer(player());

    expect(store.playerCount()).toBe(1);
    expect(store.players()[0]).toEqual({
      playerId: 'player-1',
      playerName: 'Angel',
      x: 100,
      y: 100,
      health: 100,
      score: 0,

      ammunition: 12,
      tankType: 'tank_green',
      direction: 'up',
      status: 'active',
      kills: 0,
      deaths: 0,
      isConnected: true,
      isHost: true,
    });
  });

  it('updates a player position without losing the rest of the player state', () => {
    store.addPlayer(player(75));

    store.updatePlayerPosition('player-1', 140, 80, 'right');

    expect(store.players()[0]).toEqual({
      playerId: 'player-1',
      playerName: 'Angel',
      x: 140,
      y: 80,
      health: 75,
      score: 0,

      ammunition: 12,

      lastDirection: 'right',
      direction: 'right',
      status: 'active',
      tankType: 'tank_green',
      kills: 0,
      deaths: 0,
      isConnected: true,
      isHost: true,
    });
  });

  it('removes a player', () => {
    store.addPlayer(player());

    store.removePlayer('player-1');

    expect(store.playerCount()).toBe(0);
    expect(store.players()).toEqual([]);
  });

  it('increments score and consumes ammunition for a simulated hit', () => {
    store.addPlayer(player());

    const fired = store.useAmmunition('player-1');

    store.incrementPlayerScore('player-1', 100);

    expect(fired).toBe(true);

    expect(store.players()[0].ammunition).toBe(11);

    expect(store.players()[0].score).toBe(100);
  });

  it('marks a tank as destroyed when its health reaches zero', () => {
    store.addPlayer(player());

    store.updatePlayerHealth('player-1', 0);

    expect(store.players()[0].health).toBe(0);
    expect(store.players()[0].status).toBe('destroyed');
  });
});
