import { AfterViewInit, Component, ElementRef, EventEmitter, Input, Output, ViewChild } from '@angular/core';

import { environment } from '../../../environments/environment';
import { GoogleIdentityService } from './google-identity.service';

@Component({
  selector: 'app-google-sign-in-button',
  standalone: true,
  template: '<div #buttonHost></div>'
})
export class GoogleSignInButtonComponent implements AfterViewInit {
  @Input() clientId = environment.googleSignIn.clientId;
  @Output() credentialReceived = new EventEmitter<string>();
  @Output() unavailable = new EventEmitter<string>();
  @ViewChild('buttonHost', { static: true }) private readonly buttonHost!: ElementRef<HTMLElement>;

  constructor(private readonly googleIdentity: GoogleIdentityService) {}

  async ngAfterViewInit(): Promise<void> {
    if (!environment.googleSignIn.enabled || !this.clientId.trim()) {
      return;
    }

    try {
      const google = await this.googleIdentity.load();
      google.accounts.id.initialize({
        client_id: this.clientId.trim(),
        callback: (response) => {
          if (response.credential) {
            this.credentialReceived.emit(response.credential);
          }
        }
      });
      google.accounts.id.renderButton(this.buttonHost.nativeElement, {
        type: 'standard',
        theme: 'outline',
        size: 'large',
        text: 'signin_with'
      });
    } catch (error: unknown) {
      this.unavailable.emit(error instanceof Error ? error.message : 'Google sign-in is unavailable.');
    }
  }
}
