import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, HassEntityState, CalendarConfig } from '../../models/types';

@Component({
  selector: 'app-widget-calendar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="calendar-widget">
      <ng-container *ngIf="!getEntityState(config.entityId)">
        <div class="empty-state">
          <i class="fa fa-calendar"></i>
          <p>Not configured</p>
        </div>
      </ng-container>
      <ng-container *ngIf="getEntityState(config.entityId)">
        <div class="calendar-content">
          <h4>Events</h4>
          <ng-container *ngIf="getEvents(config.entityId).length > 0; else noEvents">
            <div *ngFor="let ev of getEvents(config.entityId); trackBy: trackByEvent" class="calendar-event">
              <div class="calendar-state">{{ formatEventDate(ev) }}</div>
              <div class="calendar-message">{{ ev.summary || ev.title || ev.description || '-' }}</div>
            </div>
          </ng-container>
          <ng-template #noEvents>
            <div class="empty-state">
              <i class="fa fa-calendar-days"></i>
              <p>No upcoming events</p>
            </div>
          </ng-template>
        </div>
      </ng-container>
    </div>
  `
})
export class CalendarWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() entityStates: Record<string, HassEntityState> | null = null;

  get config(): CalendarConfig { return (this.widget?.config || {}) as CalendarConfig; }

  getEntityState(entityId?: string) {
    if (!entityId || !this.entityStates) return null;
    return this.entityStates[entityId] ?? null;
  }

  getEvents(entityId?: string) {
    const state = this.getEntityState(entityId);
    if (!state) return [];
    const attrs = state.attributes || {};
    const list = attrs['events'] || attrs['entries'] || attrs['calendar'] || attrs['data'] || [];
    const max = Math.max(1, (this.config.maxEvents as number) || 3);
    return (Array.isArray(list) ? list : []).slice(0, max);
  }

  formatEventDate(ev: any) {
    if (!ev) return '';
    const start = ev.start || ev.start_time || ev.begin || ev.datetime || ev.dtstart;
    if (!start) return '';
    try {
      const d = new Date(start);
      return d.toLocaleString();
    } catch (e) {
      return String(start);
    }
  }

  trackByEvent(index: number, ev: any) { return ev.uid || ev.id || index; }
}
