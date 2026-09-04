import { TestBed } from '@angular/core/testing';
import { provideStore } from '@ngrx/store';
import * as signalR from '@microsoft/signalr';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../auth/auth.service';
import { playersFeatureKey, playersReducer } from '../../features/game/state/players.reducer';
import {
  GAME_HUB_CONNECTION_FACTORY,
  GameHubConnection,
  GameSignalrService,
} from './game-signalr.service';

describe('GameSignalrService', () => {
  let fakeConnection: GameHubConnection;
  let fakeConnectionState: signalR.HubConnectionState;

  beforeEach(() => {
    sessionStorage.clear();
    fakeConnectionState = signalR.HubConnectionState.Disconnected;
    fakeConnection = {
      get state() {
        return fakeConnectionState;
      },
      start: vi.fn(async () => {
        fakeConnectionState = signalR.HubConnectionState.Connected;
      }),
      stop: vi.fn(async () => {
        fakeConnectionState = signalR.HubConnectionState.Disconnected;
      }),
      on: vi.fn(),
      onclose: vi.fn(),
      onreconnected: vi.fn(),
      onreconnecting: vi.fn(),
      invoke: vi.fn(async (method: string) => (method === 'ListRooms' ? [] : undefined)) as unknown as GameHubConnection['invoke'],
    };

    TestBed.configureTestingModule({
      providers: [
        provideStore({ [playersFeatureKey]: playersReducer }),
        { provide: GAME_HUB_CONNECTION_FACTORY, useValue: () => fakeConnection },
        {
          provide: AuthService,
          useValue: { user: () => ({ id: '00000000-0000-0000-0000-000000000001' }) },
        },
      ],
    });
  });

  it('sends only movement intent to the authoritative server', async () => {
    const service = TestBed.inject(GameSignalrService);

    await service.start();
    await service.sendMovement('right');

    expect(fakeConnection.start).toHaveBeenCalledTimes(1);
    expect(fakeConnection.invoke).toHaveBeenCalledWith('SendMovement', { direction: 'right' });
  });

  it('sends a shot command without client collision data', async () => {
    const service = TestBed.inject(GameSignalrService);

    await service.start();
    await service.shoot();

    expect(fakeConnection.invoke).toHaveBeenCalledWith('Shoot');
  });
});
