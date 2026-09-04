import { Injectable, signal } from '@angular/core';

interface SpriteData {
  x: number;
  y: number;
  width: number;
  height: number;
}

interface TextureAtlas {
  [spriteName: string]: SpriteData;
}

type TankBaseColor = 'blue' | 'red' | 'green' | 'dark' | 'sand';

const defaultTankType = 'tank_green';

const tankSprites: Record<string, string> = {
  tank_green: 'tank_green.png',
  tank_blue: 'tank_blue.png',
  tank_red: 'tank_red.png',
  tank_dark: 'tank_dark.png',
  tank_sand: 'tank_sand.png',
  tank_bigRed: 'tank_bigRed.png',
  tank_darkLarge: 'tank_darkLarge.png',
  tank_huge: 'tank_huge.png',
};

const tankBaseColors: Record<string, TankBaseColor> = {
  tank_green: 'green',
  tank_blue: 'blue',
  tank_red: 'red',
  tank_dark: 'dark',
  tank_sand: 'sand',
  tank_bigRed: 'red',
  tank_darkLarge: 'dark',
  tank_huge: 'dark',
};

@Injectable({ providedIn: 'root' })
export class SpriteService {
  private spritesheetImage: HTMLImageElement | null = null;
  private textureAtlas: TextureAtlas = {};
  private readonly loadedSignal = signal(false);
  private readonly loadingSignal = signal(false);

  readonly loaded = this.loadedSignal.asReadonly();
  readonly loading = this.loadingSignal.asReadonly();

  async loadSprites(): Promise<void> {
    if (this.loaded() || this.loading()) {
      return;
    }

    this.loadingSignal.set(true);

    try {
      // Cargar el spritesheet
      const spritesheetPath = 'assets/Spritesheet/allSprites_default.png';
      this.spritesheetImage = await this.loadImage(spritesheetPath);

      // Cargar y parsear el XML
      const xmlPath = 'assets/Spritesheet/allSprites_default.xml';
      const xmlContent = await this.fetchXml(xmlPath);
      this.textureAtlas = this.parseXml(xmlContent);

      this.loadedSignal.set(true);
      console.log('[SpriteService] Sprites loaded successfully');
    } catch (error) {
      console.error('[SpriteService] Failed to load sprites:', error);
      throw error;
    } finally {
      this.loadingSignal.set(false);
    }
  }

  getSprite(spriteName: string): SpriteData | null {
    if (!this.loaded()) {
      console.warn('[SpriteService] Sprites not loaded yet');
      return null;
    }

    const sprite = this.textureAtlas[spriteName];
    if (!sprite) {
      console.warn(`[SpriteService] Sprite not found: ${spriteName}`);
      return null;
    }

    return sprite;
  }

  drawSprite(
    ctx: CanvasRenderingContext2D,
    spriteName: string,
    destX: number,
    destY: number,
    options?: { width?: number; height?: number; rotation?: number }
  ): void {
    const sprite = this.getSprite(spriteName);
    if (!sprite || !this.spritesheetImage) {
      return;
    }

    const destWidth = options?.width ?? sprite.width;
    const destHeight = options?.height ?? sprite.height;

    ctx.save();

    if (options?.rotation) {
      ctx.translate(destX + destWidth / 2, destY + destHeight / 2);
      ctx.rotate(options.rotation);
      ctx.translate(-(destX + destWidth / 2), -(destY + destHeight / 2));
    }

    ctx.drawImage(
      this.spritesheetImage,
      sprite.x,
      sprite.y,
      sprite.width,
      sprite.height,
      destX,
      destY,
      destWidth,
      destHeight
    );

    ctx.restore();
  }

  getTankSprite(tankType: string): string {
    return tankSprites[tankType] ?? tankSprites[defaultTankType];
  }

  getTankBaseColor(tankType: string): TankBaseColor {
    return tankBaseColors[tankType] ?? 'green';
  }

  getExplosionSprite(frame: number): string {
    const frameNum = Math.max(1, Math.min(5, frame));
    return `explosion${frameNum}.png`;
  }

  getBulletSprite(
    color: TankBaseColor,
    size: 1 | 2 | 3 = 1,
    outlined = false,
  ): string {
    const colorMap = {
      blue: 'Blue',
      red: 'Red',
      green: 'Green',
      dark: 'Dark',
      sand: 'Sand',
    };
    return `bullet${colorMap[color]}${size}${outlined ? '_outline' : ''}.png`;
  }

  getObstacleSprite(type: 'barrel' | 'crate' | 'barricade', variant?: string): string {
    const variants: Record<string, string> = {
      barrelBlack: 'barrelBlack_side.png',
      barrelGreen: 'barrelGreen_side.png',
      barrelRed: 'barrelRed_side.png',
      barrelRust: 'barrelRust_side.png',
      crateMetal: 'crateMetal.png',
      crateWood: 'crateWood.png',
      barricadeMetal: 'barricadeMetal.png',
      barricadeWood: 'barricadeWood.png',
    };

    const key = variant ? `${type}${variant.charAt(0).toUpperCase() + variant.slice(1)}` : `crateWood`;
    return variants[key] || 'crateWood.png';
  }

  private loadImage(path: string): Promise<HTMLImageElement> {
    return new Promise((resolve, reject) => {
      const img = new Image();
      img.onload = () => resolve(img);
      img.onerror = () => reject(new Error(`Failed to load image: ${path}`));
      img.src = path;
    });
  }

  private async fetchXml(path: string): Promise<string> {
    const response = await fetch(path);
    if (!response.ok) {
      throw new Error(`Failed to fetch XML: ${path}`);
    }
    return await response.text();
  }

  private parseXml(xmlContent: string): TextureAtlas {
    const parser = new DOMParser();
    const xmlDoc = parser.parseFromString(xmlContent, 'text/xml');
    const subTextures = xmlDoc.getElementsByTagName('SubTexture');
    const atlas: TextureAtlas = {};

    for (let i = 0; i < subTextures.length; i++) {
      const subTexture = subTextures[i];
      const name = subTexture.getAttribute('name');
      const x = parseInt(subTexture.getAttribute('x') || '0', 10);
      const y = parseInt(subTexture.getAttribute('y') || '0', 10);
      const width = parseInt(subTexture.getAttribute('width') || '0', 10);
      const height = parseInt(subTexture.getAttribute('height') || '0', 10);

      if (name) {
        atlas[name] = { x, y, width, height };
      }
    }

    return atlas;
  }
}
