import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { beforeEach, describe, expect, it } from 'vitest';

import { GameSignalrService } from '../../../core/signalr/game-signalr.service';
import { BlockDestroyedEvent } from '../models/game-events';
import { MapStore } from './map.store';

describe('MapStore', () => {
  let store: InstanceType<typeof MapStore>;
  const destroyedBlocks = new Subject<BlockDestroyedEvent>();
  const mapState = new Subject<BlockDestroyedEvent[]>();

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        MapStore,
        {
          provide: GameSignalrService,
          useValue: {
            blockDestroyed$: destroyedBlocks.asObservable(),
            mapStateUpdated$: mapState.asObservable(),
          },
        },
      ],
    });
    store = TestBed.inject(MapStore);
    store.resetMap();
  });

  it('destroys only destructible blocks', () => {
    expect(store.cells()[1][5]).toBe(2);

    expect(store.destroyBlock(1, 5)).toBe(true);
    expect(store.cells()[1][5]).toBe(0);
    expect(store.destroyBlock(0, 0)).toBe(false);
    expect(store.cells()[0][0]).toBe(1);
  });

  it('updates the map when another client destroys a block', () => {
    destroyedBlocks.next({ playerId: 'remote-player', row: 1, column: 5 });

    expect(store.cells()[1][5]).toBe(0);
  });
});
