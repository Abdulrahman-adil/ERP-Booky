import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';

export interface SearchSelectOption {
  id: string;
  label: string;
  keywords?: string;
}

@Component({
  selector: 'app-search-select',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './search-select.component.html',
  styleUrl: './search-select.component.scss'
})
export class SearchSelectComponent {
  @Input() options: SearchSelectOption[] = [];
  @Input() value = '';
  @Input() placeholder = 'Search and select';
  @Input() emptyLabel = 'No selection';
  @Input() disabled = false;
  @Output() readonly valueChange = new EventEmitter<string>();

  query = '';
  isOpen = false;
  activeIndex = -1;

  get filteredOptions(): SearchSelectOption[] {
    const term = this.query.trim().toLocaleLowerCase();
    if (!term) return this.options.slice(0, 80);
    return this.options.filter(option => `${option.label} ${option.keywords || ''}`.toLocaleLowerCase().includes(term)).slice(0, 80);
  }

  get displayValue(): string {
    if (this.isOpen) return this.query;
    return this.options.find(option => option.id === this.value)?.label || '';
  }

  open(): void {
    if (this.disabled) return;
    this.isOpen = true;
    this.query = '';
    this.activeIndex = this.filteredOptions.findIndex(option => option.id === this.value);
  }

  close(): void {
    window.setTimeout(() => {
      this.isOpen = false;
      this.query = '';
      this.activeIndex = -1;
    }, 120);
  }

  search(value: string): void {
    this.query = value;
    this.isOpen = true;
    this.activeIndex = this.filteredOptions.length ? 0 : -1;
  }

  select(option: SearchSelectOption): void {
    this.valueChange.emit(option.id);
    this.isOpen = false;
    this.query = '';
    this.activeIndex = -1;
  }

  clear(): void {
    this.valueChange.emit('');
    this.query = '';
    this.isOpen = false;
  }

  keydown(event: KeyboardEvent): void {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.isOpen = true;
      this.activeIndex = Math.min(this.filteredOptions.length - 1, this.activeIndex + 1);
      return;
    }
    if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeIndex = Math.max(0, this.activeIndex - 1);
      return;
    }
    if (event.key === 'Enter' && this.activeIndex >= 0) {
      event.preventDefault();
      const option = this.filteredOptions[this.activeIndex];
      if (option) this.select(option);
      return;
    }
    if (event.key === 'Escape') {
      event.preventDefault();
      this.isOpen = false;
      this.query = '';
      return;
    }
    if (event.key === 'Tab') this.isOpen = false;
  }
}
