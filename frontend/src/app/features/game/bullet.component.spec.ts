import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { BulletComponent } from './bullet.component';

describe('BulletComponent', () => {
  let bullet: BulletComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [BulletComponent] });
    bullet = TestBed.createComponent(BulletComponent).componentInstance;
  });

  it('launches and advances in the selected direction', () => {
    bullet.launch(100, 100, 'left');

    expect(bullet.advance(12)).toEqual({ x: 88, y: 100, direction: 'left' });

    bullet.stop();
    expect(bullet.state()).toBeNull();
  });
});
