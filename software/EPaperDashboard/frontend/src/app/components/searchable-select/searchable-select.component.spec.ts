import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import { SearchableSelectComponent } from './searchable-select.component';

describe('SearchableSelectComponent', () => {
  it('exposes combobox state and supports keyboard selection', async () => {
    await TestBed.configureTestingModule({
      imports: [SearchableSelectComponent],
    }).compileComponents();

    const fixture = TestBed.createComponent(SearchableSelectComponent);
    fixture.componentRef.setInput('ariaLabel', 'Assigned dashboard');
    fixture.componentRef.setInput('options', [
      { value: 'one', label: 'Dashboard one' },
      { value: 'two', label: 'Dashboard two' },
    ]);
    const selected = vi.fn();
    fixture.componentInstance.selectionChange.subscribe(selected);
    fixture.detectChanges();

    const trigger = fixture.nativeElement.querySelector('.ss-trigger') as HTMLButtonElement;
    expect(trigger.getAttribute('role')).toBe('combobox');
    expect(trigger.getAttribute('aria-label')).toBe('Assigned dashboard');

    trigger.click();
    fixture.detectChanges();
    expect(trigger.getAttribute('aria-expanded')).toBe('true');

    const search = fixture.nativeElement.querySelector('.ss-search') as HTMLInputElement;
    search.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
    search.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    fixture.detectChanges();

    expect(selected).toHaveBeenCalledWith('one');
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
  });
});
