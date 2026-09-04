import { Injectable, computed, inject, signal } from '@angular/core';
import { Session } from '@supabase/supabase-js';
import { SUPABASE_CLIENT } from '../supabase/supabase-client';

export interface SignUpCredentials {
  email: string;
  password: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly supabase = inject(SUPABASE_CLIENT);
  private readonly sessionSignal = signal<Session | null>(null);
  private initialization: Promise<void> | null = null;

  readonly session = this.sessionSignal.asReadonly();
  readonly user = computed(() => this.sessionSignal()?.user ?? null);
  readonly accessToken = computed(() => this.sessionSignal()?.access_token ?? null);
  readonly isAuthenticated = computed(() => this.user() !== null);

  initialize(): Promise<void> {
    if (this.initialization) {
      return this.initialization;
    }

    this.initialization = this.loadSession();
    return this.initialization;
  }

  async signUp(credentials: SignUpCredentials): Promise<{ requiresEmailConfirmation: boolean }> {
    const { data, error } = await this.supabase.auth.signUp({
      email: credentials.email.trim(),
      password: credentials.password,
    });

    if (error) {
      throw error;
    }

    this.sessionSignal.set(data.session);
    return { requiresEmailConfirmation: data.session === null };
  }

  async signIn(email: string, password: string): Promise<void> {
    const { data, error } = await this.supabase.auth.signInWithPassword({
      email: email.trim(),
      password,
    });

    if (error) {
      throw error;
    }

    this.sessionSignal.set(data.session);
  }

  async signOut(): Promise<void> {
    const { error } = await this.supabase.auth.signOut();
    if (error) {
      throw error;
    }

    this.sessionSignal.set(null);
  }

  private async loadSession(): Promise<void> {
    const { data, error } = await this.supabase.auth.getSession();
    if (error) {
      throw error;
    }

    this.sessionSignal.set(data.session);
    this.supabase.auth.onAuthStateChange((_event, session) => {
      this.sessionSignal.set(session);
    });
  }
}
