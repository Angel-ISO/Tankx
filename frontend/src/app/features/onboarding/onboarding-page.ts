import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ProfileService } from '../../core/profile/profile.service';

@Component({
  selector: 'app-onboarding-page',
  imports: [ReactiveFormsModule],
  templateUrl: './onboarding-page.html',
  styleUrl: './onboarding-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OnboardingPage {
  private readonly router = inject(Router);
  private readonly formBuilder = inject(FormBuilder);
  private readonly profiles = inject(ProfileService);

  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  // Tipos de tanque disponibles
  protected readonly tankTypes = [
    { name: 'tank_green', displayName: 'Green Tank' },
    { name: 'tank_blue', displayName: 'Blue Tank' },
    { name: 'tank_red', displayName: 'Red Tank' },
    { name: 'tank_dark', displayName: 'Dark Tank' },
    { name: 'tank_sand', displayName: 'Sand Tank' },
    { name: 'tank_bigRed', displayName: 'Big Red Tank' },
    { name: 'tank_darkLarge', displayName: 'Dark Large Tank' },
    { name: 'tank_huge', displayName: 'Huge Tank' },
  ];

  protected readonly form = this.formBuilder.nonNullable.group({
    displayName: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(20)]],
    tankType: ['tank_green'],
  });

  protected selectedTankIndex = signal<number | null>(0); 

  protected selectTank(index: number): void {
    this.selectedTankIndex.set(index);
    const tankType = this.tankTypes[index].name;
    this.form.patchValue({ tankType });
  }

  protected async submit(): Promise<void> {
    this.errorMessage.set(null);

    const { displayName, tankType } = this.form.getRawValue();
    if (this.form.invalid || displayName.trim().length < 3) {
      this.form.markAllAsTouched();
      this.errorMessage.set('Please complete all fields with valid information.');
      return;
    }

    this.loading.set(true);
    try {
      await this.profiles.createProfile(displayName.trim(), tankType);
      await this.router.navigateByUrl('/lobby');
    } catch (error) {
      this.errorMessage.set(error instanceof Error ? error.message : 'Failed to create profile.');
    } finally {
      this.loading.set(false);
    }
  }
}