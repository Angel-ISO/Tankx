import { Injectable, NgZone, inject, signal } from '@angular/core';
import mqtt, { MqttClient } from 'mqtt';
import { environment } from '../../../environments/environment';
import { GameOverEvent } from '../../features/game/models/game-events';

export interface CollisionEvent {
  type: 'collision';
  playerId: string;
  playerName: string;
  targetId: string;
  targetName: string;
  damage: number;
  sentAt: string;
}

export interface GameOverMessage {
  type: 'gameOver';
  playerId: string;
  playerName: string;
  targetId: string;
  targetName: string;
  sentAt: string;
}

export interface KillFeedEntry extends CollisionEvent {
  receivedAt: number;
  latencyMs: number;
}

export interface GameOverEntry extends GameOverMessage {
  receivedAt: number;
  latencyMs: number;
}

export type MqttConnectionStatus = 'disconnected' | 'connecting' | 'connected';

/**
 * Cliente MQTT del frontend (mqtt.js + HiveMQ Cloud sobre WebSocket seguro).
 *
 * Suscribe `tankx/telemetry` y expone los eventos de colisión como un signal
 * para el kill feed del HUD. Mide la latencia de extremo a extremo
 * (sentAt del servidor -> llegada al navegador) para el benchmark del lab 6.
 */
@Injectable({ providedIn: 'root' })
export class GameMqttService {
  private readonly zone = inject(NgZone);
  private client: MqttClient | null = null;

  private readonly connectionStatusSignal = signal<MqttConnectionStatus>('disconnected');
  private readonly collisionsSignal = signal<KillFeedEntry[]>([]);
  private readonly gameOverSignal = signal<GameOverEntry | null>(null);

  readonly connectionStatus = this.connectionStatusSignal.asReadonly();
  readonly collisions = this.collisionsSignal.asReadonly();
  readonly gameOver = this.gameOverSignal.asReadonly();

  connect(): void {
    if (this.client) {
      return;
    }

    this.zone.run(() => this.connectionStatusSignal.set('connecting'));
    this.client = mqtt.connect(environment.mqtt.url, {
      username: environment.mqtt.username,
      password: environment.mqtt.password,
      // Un clientId único por pestaña: dos clientes con el mismo id se expulsan.
      clientId: `tankx-${crypto.randomUUID().slice(0, 8)}`,
      clean: true,
    });

    this.client.on('connect', () => {
      this.zone.run(() => {
        this.connectionStatusSignal.set('connected');
        console.log('[MQTT] Connected to broker');
      });
      this.client?.subscribe(environment.mqtt.topic);
    });

    this.client.on('message', (topic, payload) => {
      try {
        const message = JSON.parse(payload.toString()) as CollisionEvent | GameOverMessage;
        if (message.type === 'collision') {
          const entry: KillFeedEntry = {
            ...message,
            receivedAt: Date.now(),
            latencyMs: Date.now() - Date.parse(message.sentAt),
          };
          this.zone.run(() => {
            this.collisionsSignal.update((current) => [...current.slice(-9), entry]);
          });
          console.info('[MQTT] collision event received', entry);
        } else if (message.type === 'gameOver') {
          const entry: GameOverEntry = {
            ...message,
            receivedAt: Date.now(),
            latencyMs: Date.now() - Date.parse(message.sentAt),
          };
          this.zone.run(() => this.gameOverSignal.set(entry));
          console.info('[MQTT] gameOver event received', entry);
        }
      } catch (error) {
        console.warn('[MQTT] Could not parse incoming message', error);
      }
    });

    this.client.on('close', () => {
      this.zone.run(() => this.connectionStatusSignal.set('disconnected'));
    });

    this.client.on('error', (error) => {
      console.error('[MQTT] Connection error', error);
    });
  }

  disconnect(): void {
    this.client?.end();
    this.client = null;
    this.zone.run(() => this.connectionStatusSignal.set('disconnected'));
  }
}
