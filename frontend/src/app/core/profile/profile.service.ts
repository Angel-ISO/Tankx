import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';

export interface Profile {
  id: string;
  displayName: string;
  tankType: string;
  createdAt: string;
}

export interface ProfileStatus {
  hasProfile: boolean;
}

@Injectable({ providedIn: 'root' })
export class ProfileService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly profileSignal = signal<Profile | null>(null);
  private readonly profilesUrl = `${environment.backendUrl}/tankx/Profiles`;
  private profileRequest: { userId: string; promise: Promise<Profile> } | null = null;

  readonly profile = this.profileSignal.asReadonly();

  async checkProfileStatus(): Promise<ProfileStatus> {
    const headers = this.authorizationHeaders();
    const status = await firstValueFrom(
      this.http.get<ProfileStatus>(`${this.profilesUrl}/status`, { headers }),
    );
    return status;
  }

  async getOrCreate(): Promise<Profile> {
    const currentUserId = this.auth.user()?.id;
    const cachedProfile = this.profile();
    if (currentUserId && cachedProfile?.id === currentUserId) {
      return cachedProfile;
    }

    if (currentUserId && this.profileRequest?.userId === currentUserId) {
      return this.profileRequest.promise;
    }

    const headers = this.authorizationHeaders();
    const request = this.loadOrCreate(headers);
    if (currentUserId) {
      this.profileRequest = { userId: currentUserId, promise: request };
    }

    try {
      return await request;
    } finally {
      if (this.profileRequest?.promise === request) {
        this.profileRequest = null;
      }
    }
  }

  private async loadOrCreate(headers: HttpHeaders): Promise<Profile> {
    try {
      const profile = await firstValueFrom(
        this.http.get<Profile>(`${this.profilesUrl}/me`, { headers }),
      );
      this.profileSignal.set(profile);
      return profile;
    } catch (error) {
      if (!(error instanceof HttpErrorResponse) || error.status !== 404) {
        throw error;
      }
    }

    let profile: Profile;
    try {
      profile = await firstValueFrom(
        this.http.post<Profile>(
          this.profilesUrl,
          { displayName: this.displayNameForCurrentUser() },
          { headers },
        ),
      );
    } catch (error) {
      if (!(error instanceof HttpErrorResponse) || error.status !== 409) {
        throw error;
      }

      profile = await firstValueFrom(
        this.http.get<Profile>(`${this.profilesUrl}/me`, { headers }),
      );
    }

    this.profileSignal.set(profile);
    return profile;
  }

  private authorizationHeaders(): HttpHeaders {
    const token = this.auth.accessToken();
    if (!token) {
      throw new Error('An authenticated session is required to load the profile.');
    }

    return new HttpHeaders({ Authorization: `Bearer ${token}` });
  }

  async createProfile(displayName: string, tankType: string): Promise<Profile> {
    const headers = this.authorizationHeaders();
    const profile = await firstValueFrom(
      this.http.post<Profile>(
        this.profilesUrl,
        { displayName, tankType },
        { headers },
      ),
    );
    this.profileSignal.set(profile);
    return profile;
  }

  private displayNameForCurrentUser(): string {
    const user = this.auth.user();
    const metadataName = user?.user_metadata?.['username'];
    if (typeof metadataName === 'string' && metadataName.trim().length >= 3) {
      return metadataName.trim().slice(0, 20);
    }

    const emailName = user?.email?.split('@')[0].trim() ?? '';
    const fallback = emailName.length >= 3 ? emailName : `pilot-${user?.id.slice(0, 8) ?? 'tankx'}`;
    return fallback.slice(0, 20);
  }
}
