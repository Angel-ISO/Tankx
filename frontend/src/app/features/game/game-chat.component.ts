import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { GameSignalrService } from '../../core/signalr/game-signalr.service';
import { LobbyStore } from '../lobby/state/lobby.store';

@Component({
  selector: 'app-game-chat',
  imports: [FormsModule],
  templateUrl: './game-chat.component.html',
  styleUrl: './game-chat.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GameChatComponent {
  private readonly hub = inject(GameSignalrService);

  protected readonly store = inject(LobbyStore);
  protected readonly connectionStatus = this.hub.connectionStatus;
  protected readonly open = signal(false);
  protected readonly message = signal('');

  protected toggle(): void {
    this.open.update((open) => !open);
  }

  protected async send(): Promise<void> {
    const message = this.message().trim();
    if (!message) return;

    try {
      await this.store.sendMessage(message);
      this.message.set('');
    } catch (error) {
      console.error('[SignalR] Could not send chat message', error);
    }
  }
}
