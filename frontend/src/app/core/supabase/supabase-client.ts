import { InjectionToken } from '@angular/core';
import { SupabaseClient, createClient } from '@supabase/supabase-js';
import { environment } from '../../../environments/environment';

export const SUPABASE_CLIENT = new InjectionToken<SupabaseClient>('SupabaseClient', {
  providedIn: 'root',
  factory: () =>
    createClient(environment.supabase.url, environment.supabase.publishableKey, {
      auth: {
        autoRefreshToken: true,
        detectSessionInUrl: true,
        persistSession: true,
      },
    }),
});
