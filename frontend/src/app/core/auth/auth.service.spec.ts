import { TestBed } from '@angular/core/testing';
import { Session } from '@supabase/supabase-js';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SUPABASE_CLIENT } from '../supabase/supabase-client';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  const session = {
    access_token: 'jwt-token',
    user: { id: 'user-1', email: 'pilot@tankx.test' },
  } as unknown as Session;

  const authApi = {
    getSession: vi.fn(),
    onAuthStateChange: vi.fn(),
    signInWithPassword: vi.fn(),
    signOut: vi.fn(),
    signUp: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    authApi.getSession.mockResolvedValue({ data: { session: null }, error: null });
    authApi.onAuthStateChange.mockReturnValue({ data: { subscription: { unsubscribe: vi.fn() } } });

    TestBed.configureTestingModule({
      providers: [
        AuthService,
        { provide: SUPABASE_CLIENT, useValue: { auth: authApi } },
      ],
    });
  });

  it('registers a user with email and password in Supabase', async () => {
    authApi.signUp.mockResolvedValue({ data: { session }, error: null });
    const service = TestBed.inject(AuthService);

    const result = await service.signUp({
      email: 'pilot@tankx.test',
      password: 'password123',
    });

    expect(authApi.signUp).toHaveBeenCalledWith({
      email: 'pilot@tankx.test',
      password: 'password123',
    });
    expect(result.requiresEmailConfirmation).toBe(false);
    expect(service.isAuthenticated()).toBe(true);
  });

  it('stores the session after a successful login', async () => {
    authApi.signInWithPassword.mockResolvedValue({ data: { session }, error: null });
    const service = TestBed.inject(AuthService);

    await service.signIn('pilot@tankx.test', 'password123');

    expect(service.user()?.id).toBe('user-1');
    expect(service.accessToken()).toBe('jwt-token');
  });

  it('clears the session when the user logs out', async () => {
    authApi.signInWithPassword.mockResolvedValue({ data: { session }, error: null });
    authApi.signOut.mockResolvedValue({ error: null });
    const service = TestBed.inject(AuthService);
    await service.signIn('pilot@tankx.test', 'password123');

    await service.signOut();

    expect(service.isAuthenticated()).toBe(false);
  });
});
