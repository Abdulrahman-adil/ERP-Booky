import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export type FeedbackTone = 'success' | 'error' | 'info';

export interface FeedbackMessage {
  id: number;
  tone: FeedbackTone;
  message: string;
}

@Injectable({ providedIn: 'root' })
export class FeedbackService {
  private readonly soundStorageKey = 'erp.financial-sound-enabled';
  private readonly messagesSubject = new BehaviorSubject<FeedbackMessage[]>([]);
  private readonly soundEnabledSubject = new BehaviorSubject<boolean>(this.readSoundPreference());
  private audioContext?: AudioContext;
  private lastFinancialSoundAt = 0;
  private nextId = 0;

  readonly messages$ = this.messagesSubject.asObservable();
  readonly soundEnabled$ = this.soundEnabledSubject.asObservable();

  success(message: string): void {
    this.show('success', message);
  }

  error(message: string): void {
    this.show('error', message);
  }

  info(message: string): void {
    this.show('info', message);
  }

  dismiss(id: number): void {
    this.messagesSubject.next(this.messagesSubject.value.filter(message => message.id !== id));
  }

  setSoundEnabled(enabled: boolean): void {
    this.soundEnabledSubject.next(enabled);
    if (typeof localStorage !== 'undefined') localStorage.setItem(this.soundStorageKey, String(enabled));
  }

  financialSuccess(message: string): void {
    this.success(message);
    if (this.soundEnabledSubject.value) this.playFinancialConfirmation();
  }

  primeFinancialSound(): void {
    if (!this.soundEnabledSubject.value || typeof window === 'undefined') return;
    try {
      const context = this.getAudioContext();
      if (context?.state === 'suspended') void context.resume();
    } catch {}
  }

  private show(tone: FeedbackTone, message: string): void {
    const feedbackMessage = { id: ++this.nextId, tone, message };
    this.messagesSubject.next([...this.messagesSubject.value, feedbackMessage]);
    window.setTimeout(() => this.dismiss(feedbackMessage.id), 5000);
  }

  private readSoundPreference(): boolean {
    return typeof localStorage === 'undefined' || localStorage.getItem(this.soundStorageKey) !== 'false';
  }

  private playFinancialConfirmation(): void {
    try {
      if (Date.now() - this.lastFinancialSoundAt < 350) return;
      const context = this.getAudioContext();
      if (!context) return;
      this.lastFinancialSoundAt = Date.now();
      if (context.state === 'suspended') {
        void context.resume().then(() => this.scheduleFinancialConfirmation(context)).catch(() => undefined);
        return;
      }
      this.scheduleFinancialConfirmation(context);
    } catch {}
  }

  private getAudioContext(): AudioContext | undefined {
    const contextConstructor = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!contextConstructor) return undefined;
    this.audioContext ??= new contextConstructor();
    return this.audioContext;
  }

  private scheduleFinancialConfirmation(context: AudioContext): void {
    try {
      const gain = context.createGain();
      gain.gain.setValueAtTime(0.0001, context.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.028, context.currentTime + 0.012);
      gain.gain.exponentialRampToValueAtTime(0.0001, context.currentTime + 0.2);
      gain.connect(context.destination);

      [659.25, 783.99].forEach((frequency, index) => {
        const oscillator = context.createOscillator();
        const start = context.currentTime + (index * 0.045);
        oscillator.type = 'sine';
        oscillator.frequency.setValueAtTime(frequency, start);
        oscillator.connect(gain);
        oscillator.start(start);
        oscillator.stop(start + 0.12);
      });
      window.setTimeout(() => gain.disconnect(), 260);
    } catch {}
  }
}
