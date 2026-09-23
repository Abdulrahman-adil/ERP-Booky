import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export type ConfirmationTone = 'primary' | 'danger' | 'warning';

export interface ConfirmationDialog {
  title: string;
  message: string;
  confirmLabel: string;
  tone?: ConfirmationTone;
}

@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  private readonly dialogSubject = new BehaviorSubject<ConfirmationDialog | null>(null);
  private resolver?: (confirmed: boolean) => void;

  readonly dialog$ = this.dialogSubject.asObservable();

  confirm(dialog: ConfirmationDialog): Promise<boolean> {
    this.resolve(false);
    this.dialogSubject.next({ ...dialog, tone: dialog.tone ?? 'primary' });
    return new Promise<boolean>(resolve => this.resolver = resolve);
  }

  resolve(confirmed: boolean): void {
    const resolver = this.resolver;
    this.resolver = undefined;
    this.dialogSubject.next(null);
    resolver?.(confirmed);
  }
}
