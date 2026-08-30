import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

export interface EntitySourceOption {
  entity_id: string;
  friendly_name?: string;
  domain?: string;
  device_class?: string;
  unit_of_measurement?: string;
  state?: string;
}

@Component({
  selector: 'app-entity-source-selector',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="mb-3">
      <label class="form-label fw-semibold small">Data Source</label>
      @if (loading) {
        <div class="text-muted small"><i class="fa fa-spinner fa-spin me-1"></i>Loading entities…</div>
      } @else if (entities.length === 0) {
        <div class="alert alert-warning mb-0">
          <i class="fa fa-exclamation-triangle"></i> No compatible entities available.
        </div>
      } @else {
        @if (entities.length > 8) {
          <input class="form-control form-control-sm mb-2" type="search" placeholder="Filter entities…"
            [ngModel]="query()" (ngModelChange)="query.set($event)" />
        }
        <select class="form-select" [ngModel]="value" (ngModelChange)="valueChange.emit($event)">
          <option value="">Select entity…</option>
          @for (entity of filteredEntities(); track entity.entity_id) {
            <option [value]="entity.entity_id">{{ formatEntity(entity) }}</option>
          }
        </select>
      }
    </div>
    <hr class="my-2" />
  `,
})
export class EntitySourceSelectorComponent {
  @Input() value = '';
  @Input() entities: EntitySourceOption[] = [];
  @Input() loading = false;
  @Output() valueChange = new EventEmitter<string>();

  readonly query = signal('');

  filteredEntities(): EntitySourceOption[] {
    const query = this.query().trim().toLowerCase();
    if (!query) return this.entities;
    return this.entities.filter(entity => this.formatEntity(entity).toLowerCase().includes(query));
  }

  formatEntity(entity: EntitySourceOption): string {
    const name = entity.friendly_name || entity.entity_id;
    const details = [entity.domain, entity.device_class, entity.unit_of_measurement]
      .filter(Boolean)
      .join(', ');
    const state = entity.state != null && entity.state !== '' ? ` · ${entity.state}` : '';
    return `${name}${details ? ` (${details})` : ''}${state}`;
  }
}
