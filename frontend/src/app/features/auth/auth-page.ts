import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ProfileService } from '../../core/profile/profile.service';

@Component({
  selector: 'app-auth-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './auth-page.html',
  styleUrl: './auth-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthPage {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly formBuilder = inject(FormBuilder);
  private readonly profiles = inject(ProfileService);

  protected readonly isRegister = this.route.snapshot.data['mode'] === 'register';
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly title = computed(() => (this.isRegister ? 'Create your pilot' : 'Welcome back'));

  protected readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  protected async submit(): Promise<void> {
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const { email, password } = this.form.getRawValue();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.errorMessage.set('Please complete all fields with valid information.');
      return;
    }

    this.loading.set(true);
    try {
      if (this.isRegister) {
        const result = await this.auth.signUp({ email, password });
        if (result.requiresEmailConfirmation) {
          this.successMessage.set('Account created. Check your email to confirm your account.');
          this.form.reset();
          return;
        }
      } else {
        await this.auth.signIn(email, password);
      }

      const profileStatus = await this.profiles.checkProfileStatus();
      if (profileStatus.hasProfile) {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/lobby';
        await this.router.navigateByUrl(returnUrl);
      } else {
        await this.router.navigateByUrl('/onboarding');
      }
    } catch (error) {
      this.errorMessage.set(error instanceof Error ? error.message : 'Authentication failed.');
    } finally {
      this.loading.set(false);
    }
  }
}
