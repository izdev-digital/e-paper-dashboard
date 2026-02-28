import { Component, input, output, signal, computed, ElementRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface SelectOption {
  value: string;
  label: string;
}

@Component({
  selector: 'app-searchable-select',
  standalone: true,
  imports: [CommonModule],
  styles: [`
    :host {
      position: relative;
      display: inline-block;
    }

    .ss-trigger {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
      cursor: pointer;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      padding: 0.25rem 0.5rem;
      font-size: 0.85rem;
      line-height: 1.5;
      border: 1px solid var(--bs-border-color);
      border-radius: 0.375rem;
      background-color: var(--bs-body-bg);
      color: var(--bs-body-color);
      transition: border-color 0.15s ease;
    }

    .ss-trigger:hover {
      border-color: var(--bs-primary);
    }

    .ss-trigger-text {
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .ss-chevron {
      flex-shrink: 0;
      font-size: 0.65rem;
      transition: transform 0.15s ease;
    }

    .ss-chevron.open {
      transform: rotate(180deg);
    }

    .ss-dropdown {
      position: absolute;
      top: 100%;
      left: 0;
      z-index: 1050;
      min-width: 100%;
      max-height: 280px;
      margin-top: 2px;
      background: var(--bs-body-bg);
      border: 1px solid var(--bs-border-color);
      border-radius: 0.375rem;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }

    .ss-search {
      padding: 0.375rem 0.5rem;
      border: none;
      border-bottom: 1px solid var(--bs-border-color);
      outline: none;
      font-size: 0.85rem;
      background: var(--bs-body-bg);
      color: var(--bs-body-color);
    }

    .ss-search::placeholder {
      color: var(--bs-secondary-color);
    }

    .ss-options {
      overflow-y: auto;
      max-height: 230px;
    }

    .ss-option {
      padding: 0.375rem 0.5rem;
      cursor: pointer;
      font-size: 0.85rem;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      color: var(--bs-body-color);
    }

    .ss-option:hover {
      background: var(--bs-secondary-bg);
    }

    .ss-option.selected {
      background: var(--bs-primary);
      color: white;
    }

    .ss-option.selected:hover {
      background: var(--bs-primary);
      opacity: 0.9;
    }

    .ss-empty {
      padding: 0.5rem;
      text-align: center;
      font-size: 0.85rem;
      color: var(--bs-secondary-color);
    }
  `],
  template: `
    <div class="ss-trigger"
      (click)="toggle($event)">
      <span class="ss-trigger-text" [class.text-muted]="!value()">
        {{ selectedLabel() }}
      </span>
      <i class="fa-solid fa-chevron-down ss-chevron" [class.open]="isOpen()"></i>
    </div>
    @if (isOpen()) {
      <div class="ss-dropdown" (click)="$event.stopPropagation()">
        <input
          class="ss-search"
          type="text"
          [placeholder]="searchPlaceholder()"
          [value]="searchTerm()"
          (input)="onSearchInput($event)">
        <div class="ss-options">
          @if (showEmptyOption()) {
            <div class="ss-option"
              [class.selected]="!value()"
              (click)="select('')">
              {{ emptyLabel() }}
            </div>
          }
          @for (option of filteredOptions(); track option.value) {
            <div class="ss-option"
              [class.selected]="option.value === value()"
              (click)="select(option.value)">
              {{ option.label }}
            </div>
          }
          @if (filteredOptions().length === 0 && searchTerm()) {
            <div class="ss-empty">No matches found</div>
          }
        </div>
      </div>
    }
  `
})
export class SearchableSelectComponent {
  private readonly elementRef = inject(ElementRef);
  private onDocumentClickBound = this.onDocumentClick.bind(this);

  readonly options = input<SelectOption[]>([]);
  readonly value = input<string>('');
  readonly emptyLabel = input<string>('— None —');
  readonly showEmptyOption = input<boolean>(true);
  readonly searchPlaceholder = input<string>('Search...');
  readonly selectionChange = output<string>();

  readonly isOpen = signal(false);
  readonly searchTerm = signal('');

  readonly selectedLabel = computed(() => {
    const val = this.value();
    if (!val) return this.emptyLabel();
    const option = this.options().find(o => o.value === val);
    return option?.label ?? val;
  });

  readonly filteredOptions = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    const opts = this.options();
    if (!term) return opts;
    return opts.filter(o => o.label.toLowerCase().includes(term));
  });

  onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchTerm.set(value);
  }

  toggle(event: MouseEvent): void {
    event.stopPropagation();
    if (this.isOpen()) {
      this.close();
    } else {
      this.open();
    }
  }

  select(value: string): void {
    this.selectionChange.emit(value);
    this.close();
  }

  private open(): void {
    this.searchTerm.set('');
    this.isOpen.set(true);
    document.addEventListener('click', this.onDocumentClickBound, true);
    setTimeout(() => {
      const input = this.elementRef.nativeElement.querySelector('.ss-search');
      input?.focus();
    });
  }

  private close(): void {
    this.isOpen.set(false);
    this.searchTerm.set('');
    document.removeEventListener('click', this.onDocumentClickBound, true);
  }

  private onDocumentClick(event: MouseEvent): void {
    if (!this.elementRef.nativeElement.contains(event.target as Node)) {
      this.close();
    }
  }
}
