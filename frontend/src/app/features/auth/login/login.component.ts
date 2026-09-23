import { CommonModule } from '@angular/common'
import { Component } from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { ActivatedRoute, Router } from '@angular/router'
import { finalize } from 'rxjs'
import { GoogleSignInButtonComponent } from '../../../core/auth/google-sign-in-button.component'
import { AuthService } from '../../../core/auth/auth.service'

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, GoogleSignInButtonComponent],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly formBuilder = new FormBuilder().nonNullable

  readonly loginForm = this.formBuilder.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  })

  isSubmitting = false
  errorMessage = ''

  constructor(
    private readonly auth: AuthService,
    private readonly router: Router,
    private readonly route: ActivatedRoute,
  ) {}

  get email() {
    return this.loginForm.controls.email
  }

  get password() {
    return this.loginForm.controls.password
  }

  signIn(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched()
      return
    }

    this.isSubmitting = true
    this.errorMessage = ''
    const credentials = this.loginForm.getRawValue()

    this.auth
      .login(credentials)
      .pipe(
        finalize(() => {
          this.isSubmitting = false
        }),
      )
      .subscribe({
        next: () => {
          const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl')
          void this.router.navigateByUrl(
            returnUrl?.startsWith('/') ? returnUrl : '/dashboard',
          )
        },
        error: (error: { status?: number }) => {
          this.errorMessage =
            error.status === 401
              ? 'Your email address or password is incorrect.'
              : 'We could not sign you in. Please try again.'
        },
      })
  }

  signInWithGoogle(idToken: string): void {
    if (!idToken || this.isSubmitting) {
      return
    }

    this.isSubmitting = true
    this.errorMessage = ''

    this.auth
      .loginWithGoogleIdToken(idToken)
      .pipe(
        finalize(() => {
          this.isSubmitting = false
        }),
      )
      .subscribe({
        next: () => {
          const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl')
          const target =
            returnUrl?.startsWith('/') && !returnUrl.startsWith('//')
              ? returnUrl
              : '/dashboard'

          void this.router.navigateByUrl(target)
        },
        error: (error: { status?: number; error?: { code?: string } }) => {
          if (
            error.status === 409 &&
            error.error?.code === 'google_account_link_required'
          ) {
            this.errorMessage =
              'This email already belongs to an ERP account. Use your existing sign-in method.'
            return
          }

          this.errorMessage =
            error.status === 401
              ? 'Google sign-in could not be verified. Please try again.'
              : 'We could not sign you in with Google. Please try again.'
        },
      })
  }

  handleGoogleUnavailable(message: string): void {
    this.errorMessage = message
  }

}
