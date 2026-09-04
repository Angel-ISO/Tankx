import { DestroyRef, computed, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withState,
} from '@ngrx/signals';

import { GameSignalrService } from '../../../core/signalr/game-signalr.service';
import mapData from '../maps/battle-city-map.json';
import { BlockDestroyedEvent } from '../models/game-events';

export type MapCell = 0 | 1 | 2;

export interface MapExplosion {
  id: string;
  row: number;
  column: number;
}

interface MapState {
  cells: MapCell[][];
  explosions: MapExplosion[];
}

const createInitialCells = (): MapCell[][] => (mapData as MapCell[][]).map((row) => [...row]);

const initialState: MapState = {
  cells: createInitialCells(),
  explosions: [],
};

export const MapStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ cells }) => ({
    rowCount: computed(() => cells().length),
    columnCount: computed(() => cells()[0]?.length ?? 0),
    destructibleBlockCount: computed(() =>
      cells().reduce((total, row) => total + row.filter((cell) => cell === 2).length, 0),
    ),
  })),
  withMethods((store) => ({
    destroyBlock(row: number, column: number): boolean {
      if (store.cells()[row]?.[column] !== 2) {
        return false;
      }

      const cells = store.cells().map((mapRow) => [...mapRow]);
      cells[row][column] = 0;
      const explosion: MapExplosion = {
        id: `${row}-${column}-${Date.now()}`,
        row,
        column,
      };

      patchState(store, (state) => ({
        cells,
        explosions: [...state.explosions, explosion],
      }));

      setTimeout(() => {
        patchState(store, (state) => ({
          explosions: state.explosions.filter((item) => item.id !== explosion.id),
        }));
      }, 420);

      return true;
    },

    synchronizeDestroyedBlocks(blocks: BlockDestroyedEvent[]): void {
      const cells = store.cells().map((mapRow) => [...mapRow]);
      for (const block of blocks) {
        if (cells[block.row]?.[block.column] === 2) {
          cells[block.row][block.column] = 0;
        }
      }
      patchState(store, { cells });
    },

    resetMap(): void {
      patchState(store, { cells: createInitialCells(), explosions: [] });
    },
  })),
  withHooks((store, hub = inject(GameSignalrService), destroyRef = inject(DestroyRef)) => ({
    onInit(): void {
      hub.blockDestroyed$
        .pipe(takeUntilDestroyed(destroyRef))
        .subscribe((event) => store.destroyBlock(event.row, event.column));

      hub.mapStateUpdated$
        .pipe(takeUntilDestroyed(destroyRef))
        .subscribe((blocks) => store.synchronizeDestroyedBlocks(blocks));
    },
  })),
);
