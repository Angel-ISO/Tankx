import { ChangeDetectionStrategy, Component, signal } from '@angular/core';

import { MovementDirection } from './models/game-events';

export interface BulletState {
  x: number;
  y: number;
  direction: MovementDirection;
}

const bulletVectors: Record<MovementDirection, { x: number; y: number }> = {
  up: { x: 0, y: -1 },
  down: { x: 0, y: 1 },
  left: { x: -1, y: 0 },
  right: { x: 1, y: 0 },
};

@Component({
  selector: 'app-bullet',
  standalone: true,
  template: '',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BulletComponent {
  private readonly bulletState = signal<BulletState | null>(null);

  readonly state = this.bulletState.asReadonly();

  launch(x: number, y: number, direction: MovementDirection): void {
    this.bulletState.set({ x, y, direction });
  }

  advance(distance: number): BulletState | null {
    const bullet = this.bulletState();
    if (!bullet) return null;

    const vector = bulletVectors[bullet.direction];
    const nextBullet = {
      ...bullet,
      x: bullet.x + vector.x * distance,
      y: bullet.y + vector.y * distance,
    };
    this.bulletState.set(nextBullet);
    return nextBullet;
  }

  stop(): void {
    this.bulletState.set(null);
  }
}
