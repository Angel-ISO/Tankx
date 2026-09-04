export type MovementDirection = 'up' | 'down' | 'left' | 'right';
export type TankStatus = 'active' | 'destroyed';
export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';
export type RoomStatus = 'waiting' | 'inProgress' | 'finished';

export interface PlayerState {
  playerId: string;
  playerName: string;
  tankType: string;
  x: number;
  y: number;
  health: number;
  score: number;
  ammunition: number;
  kills: number;
  deaths: number;
  direction: MovementDirection;
  status: TankStatus;
  isConnected: boolean;
  isHost: boolean;
  lastDirection?: MovementDirection;
}

export type TankState = PlayerState;

export interface PlayerJoinedEvent extends PlayerState {}

export interface PlayerMovementEvent {
  playerId: string;
  playerName: string;
  direction: MovementDirection;
  x: number;
  y: number;
}

export interface ChatMessageEvent {
  playerId: string;
  playerName: string;
  message: string;
  sentAt: string;
}

export interface BulletState {
  id: string;
  ownerId: string;
  x: number;
  y: number;
  direction: MovementDirection;
}

export interface BlockDestroyedEvent {
  playerId: string;
  row: number;
  column: number;
}

export interface RoomSummary {
  roomCode: string;
  hostPlayerId: string;
  hostPlayerName: string;
  playerCount: number;
  maxPlayers: number;
  mapName: string;
  isPrivate: boolean;
  status: RoomStatus;
}

export interface GameStateSnapshot {
  roomCode: string;
  matchId: string | null;
  status: RoomStatus;
  hostPlayerId: string;
  maxPlayers: number;
  mapName: string;
  players: PlayerState[];
  bullets: BulletState[];
  destroyedBlocks: BlockDestroyedEvent[];
  serverTime: string;
}

export interface MatchStartedEvent {
  matchId: string;
  startedAt: string;
}

export interface MatchEndedEvent {
  matchId: string;
  winnerId: string;
  winnerName: string;
  finishedAt: string;
}

export interface CollisionEvent {
  playerId: string;
  playerName: string;
  targetId: string;
  targetName: string;
  damage: number;
  sentAt: string;
}

export interface GameOverEvent {
  playerId: string;
  playerName: string;
  targetId: string;
  targetName: string;
  sentAt: string;
}
