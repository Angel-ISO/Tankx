import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { ProfileService } from '../profile/profile.service';

export const authGuard: CanActivateFn = async (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  await auth.initialize();
  return auth.isAuthenticated()
    ? true
    : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

export const guestGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const profileService = inject(ProfileService);
  const router = inject(Router);

  await auth.initialize();
  
  if (!auth.isAuthenticated()) {
    return true;
  }

  try {
    const profileStatus = await profileService.checkProfileStatus();
    if (profileStatus.hasProfile) {
      return router.createUrlTree(['/lobby']);
    } else {
      return router.createUrlTree(['/onboarding']);
    }
  } catch (error) {
    console.error('Error checking profile status:', error);
    return router.createUrlTree(['/lobby']);
  }
};

export const profileGuard: CanActivateFn = async (_route, state) => {
  const auth = inject(AuthService);
  const profileService = inject(ProfileService);
  const router = inject(Router);

  await auth.initialize();
  
  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  }

  try {
    const profileStatus = await profileService.checkProfileStatus();
    if (profileStatus.hasProfile) {
      return true;
    } else {
      return router.createUrlTree(['/onboarding'], { queryParams: { returnUrl: state.url } });
    }
  } catch (error) {
    console.error('Error checking profile status:', error);
    return true;
  }
};
