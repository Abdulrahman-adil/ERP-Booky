import { Injectable } from '@angular/core';

import { environment } from '../../../environments/environment';

export interface GoogleCredentialResponse {
  credential: string;
  select_by: string;
}

export interface GoogleIdentityApi {
  accounts: {
    id: {
      initialize(configuration: {
        client_id: string;
        callback: (response: GoogleCredentialResponse) => void;
      }): void;
      renderButton(parent: HTMLElement, options: Record<string, string | number | boolean>): void;
    };
  };
}

declare global {
  interface Window {
    google?: GoogleIdentityApi;
  }
}

@Injectable({ providedIn: 'root' })
export class GoogleIdentityService {
  private readonly scriptUrl = 'https://accounts.google.com/gsi/client';
  private loadRequest?: Promise<GoogleIdentityApi>;

  load(): Promise<GoogleIdentityApi> {
    if (!environment.googleSignIn.enabled || !environment.googleSignIn.clientId.trim()) {
      return Promise.reject(new Error('Google sign-in is not configured.'));
    }

    if (typeof window !== 'undefined' && window.google?.accounts?.id) {
      return Promise.resolve(window.google);
    }

    if (!this.loadRequest) {
      this.loadRequest = new Promise<GoogleIdentityApi>((resolve, reject) => {
        if (typeof document === 'undefined') {
          reject(new Error('Google Identity Services requires a browser.'));
          return;
        }

        const existingScript = document.querySelector<HTMLScriptElement>(`script[src="${this.scriptUrl}"]`);
        const script = existingScript ?? document.createElement('script');
        const timeout = window.setTimeout(() => reject(new Error('Google Identity Services did not load.')), 10_000);

        const completeLoad = () => {
          window.clearTimeout(timeout);
          if (window.google?.accounts?.id) {
            resolve(window.google);
          } else {
            reject(new Error('Google Identity Services did not load.'));
          }
        };

        script.addEventListener('load', completeLoad, { once: true });
        script.addEventListener('error', () => {
          window.clearTimeout(timeout);
          reject(new Error('Google Identity Services failed to load.'));
        }, { once: true });

        if (!existingScript) {
          script.src = this.scriptUrl;
          script.async = true;
          script.defer = true;
          document.head.appendChild(script);
        }
      }).catch((error: unknown) => {
        this.loadRequest = undefined;
        return Promise.reject(error);
      });
    }

    return this.loadRequest;
  }
}
