import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideStore } from '@ngrx/store';

import { routes } from './app.routes';
import { AuthService } from './core/auth/auth.service';
import { playersFeatureKey, playersReducer } from './features/game/state/players.reducer';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(),
    provideRouter(routes),
    provideStore({ [playersFeatureKey]: playersReducer }),
    provideAppInitializer(() => inject(AuthService).initialize()),
  ]
};
