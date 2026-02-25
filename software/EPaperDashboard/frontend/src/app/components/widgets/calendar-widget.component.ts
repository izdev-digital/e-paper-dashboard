import {
  Component,
  Input,
  OnChanges,
  SimpleChanges,
  ChangeDetectorRef,
  ViewChild,
  ElementRef,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  WidgetConfig,
  ColorScheme,
  HassEntityState,
  CalendarConfig,
  CalendarEventItemConfig,
  DEFAULT_CALENDAR_EVENT_ITEMS,
  defaultCalendarEventItemIcon,
  DashboardLayout,
} from '../../models/types';

@Component({
  selector: 'app-widget-calendar',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./calendar-widget.component.scss'],
  template: `
    <div #host class="calendar-widget"
         [style.--headerFontSize]="getHeaderFontSize() + 'px'"
         [style.--eventFontSize]="getEventFontSize() + 'px'"
         [style.--headerFontWeight]="getHeaderFontWeight()"
         [style.--eventFontWeight]="getEventFontWeight()"
         [style.--iconColor]="getIconColor()"
         [style.--titleColor]="getTitleColor()"
         [style.--textColor]="getTextColor()"
         [style.color]="getTextColor()">

      @if (!isDataFetched()) {
        <div class="preview-state">
          <i class="fa fa-calendar"></i>
          <p>Calendar</p>
        </div>
      }
      @if (isDataFetched()) {
        <div class="calendar-content">
          @if (widget.showTitle !== false) {
            <h4>{{ widget.titleOverride || 'Events' }}</h4>
          }
          @if (getUpcomingEvents(config.entityId).length > 0) {
            <div class="calendar-events"
                 [style.gap.px]="config.eventGap ?? 0">
            @for (ev of getUpcomingEvents(config.entityId); track trackByEvent($index, ev)) {
              <div class="calendar-event">
                @for (item of visibleItems(); track item.type) {
                  <div class="cw-item-row">
                    @switch (item.type) {
                      @case ('datetime') {
                        <span class="cw-value cw-with-icon">
                          <i class="fa {{ resolveIcon(item) }}" [style.color]="getIconColor()"></i>
                          <span class="cw-text">{{ formatEventDate(ev) }}</span>
                        </span>
                      }
                      @case ('title') {
                        <span class="cw-value cw-with-icon">
                          <i class="fa {{ resolveIcon(item) }}" [style.color]="getIconColor()"></i>
                          <span class="cw-text">{{ ev.summary || ev.title || ev.description || '-' }}</span>
                        </span>
                      }
                      @case ('location') {
                        @if (ev.location) {
                          <span class="cw-value cw-with-icon">
                            <i class="fa {{ resolveIcon(item) }}" [style.color]="getIconColor()"></i>
                            <span class="cw-text">{{ ev.location }}</span>
                          </span>
                        }
                      }
                      @case ('description') {
                        @if (ev.description) {
                          <span class="cw-value cw-with-icon">
                            <i class="fa {{ resolveIcon(item) }}" [style.color]="getIconColor()"></i>
                            <span class="cw-text">{{ ev.description }}</span>
                          </span>
                        }
                      }
                    }
                  </div>
                }
              </div>
            }
            </div>
          } @else {
            <div class="empty-state">
              <i class="fa fa-calendar-days"></i>
              <p>No upcoming events</p>
            </div>
          }
        </div>
      }
    </div>
  `
})
export class CalendarWidgetComponent implements OnChanges {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() entityStates: Record<string, HassEntityState> | null = null;
  @Input() calendarEventsByEntityId: Record<string, any[]> | undefined;
  @Input() designerSettings?: DashboardLayout;

  @ViewChild('host') hostRef!: ElementRef<HTMLDivElement>;

  get config(): CalendarConfig { return (this.widget?.config || {}) as CalendarConfig; }

  constructor(
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnChanges(_changes: SimpleChanges): void {}

  // ─── items ────────────────────────────────────────────────────────────────
  getItems(): CalendarEventItemConfig[] {
    return this.config.items?.length ? this.config.items : DEFAULT_CALENDAR_EVENT_ITEMS;
  }

  visibleItems(): CalendarEventItemConfig[] {
    return this.getItems().filter(i => i.visible !== false);
  }

  // ─── icon resolver ────────────────────────────────────────────────────────
  resolveIcon(item: CalendarEventItemConfig): string {
    return item.icon || defaultCalendarEventItemIcon(item.type);
  }

  // ─── style helpers ────────────────────────────────────────────────────────
  getHeaderFontSize(): number {
    return this.designerSettings?.titleFontSize ?? 15;
  }

  getEventFontSize(): number {
    return this.designerSettings?.textFontSize ?? 12;
  }

  getHeaderFontWeight(): number {
    return this.designerSettings?.titleFontWeight ?? 700;
  }

  getEventFontWeight(): number {
    return this.designerSettings?.textFontWeight ?? 400;
  }

  /**
   * Checks if calendar data has been fetched for the configured entity.
   */
  isDataFetched(): boolean {
    const entityId = this.config.entityId;
    if (!entityId) return false;

    if (this.calendarEventsByEntityId && entityId in this.calendarEventsByEntityId) {
      return true;
    }

    const state = this.getEntityState(entityId);
    if (!state || !state.attributes) return false;

    const attrs = state.attributes;
    return !!(
      attrs['events'] ||
      attrs['entries'] ||
      attrs['calendar_events'] ||
      attrs['upcoming_events'] ||
      attrs['data']
    );
  }

  getEntityState(entityId?: string) {
    if (!entityId || !this.entityStates) return null;
    return this.entityStates[entityId] ?? null;
  }

  getUpcomingEvents(entityId?: string) {
    if (!entityId) return [];

    if (this.calendarEventsByEntityId && this.calendarEventsByEntityId[entityId]) {
      const events = this.calendarEventsByEntityId[entityId];
      const max = Math.max(1, (this.config.maxEvents as number) || 7);
      return (Array.isArray(events) ? events : [])
        .filter(ev => this.isUpcomingEvent(ev))
        .slice(0, max);
    }

    const state = this.getEntityState(entityId);
    if (!state) return [];

    const attrs = state.attributes || {};
    const eventsList =
      attrs['events'] ||
      attrs['entries'] ||
      attrs['calendar_events'] ||
      attrs['upcoming_events'] ||
      attrs['data'] ||
      [];

    const max = Math.max(1, (this.config.maxEvents as number) || 7);
    return (Array.isArray(eventsList) ? eventsList : [])
      .filter(ev => this.isUpcomingEvent(ev))
      .slice(0, max);
  }

  private isUpcomingEvent(event: any): boolean {
    if (!event) return false;

    try {
      const startStr = event.start || event.start_time || event.begin || event.datetime || event.dtstart;
      const endStr = event.end || event.end_time || event.finish || event.end_datetime || event.dtend;

      if (!startStr) return false;

      const startDate = this.parseEventDate(startStr);
      if (!startDate) return false;

      const now = new Date();

      if (endStr) {
        const endDate = this.parseEventDate(endStr);
        if (endDate && endDate > now) {
          return true;
        }
      }

      return startDate >= now;
    } catch {
      return false;
    }
  }

  formatEventDate(ev: any): string {
    if (!ev) return '';

    const start = ev.start || ev.start_time || ev.begin || ev.datetime || ev.dtstart;
    if (!start) return '';

    try {
      const d = this.parseEventDate(start);
      if (!d) return String(start);

      if (typeof start === 'string' && start.length === 10 && /^\d{4}-\d{2}-\d{2}$/.test(start)) {
        return d.toLocaleDateString(navigator.language, {
          weekday: 'short',
          month: 'short',
          day: 'numeric'
        });
      }

      return d.toLocaleString(navigator.language, {
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
      });
    } catch (e) {
      return String(start);
    }
  }

  private parseEventDate(dateStr: any): Date | null {
    if (!dateStr) return null;

    try {
      if (typeof dateStr === 'object' && dateStr !== null) {
        if (dateStr instanceof Date) return dateStr;

        if (dateStr.isoformat) {
          return new Date(dateStr.isoformat);
        }

        if (dateStr.year && dateStr.month && dateStr.day) {
          const month = String(dateStr.month).padStart(2, '0');
          const day = String(dateStr.day).padStart(2, '0');
          const dateStr2 = `${dateStr.year}-${month}-${day}`;
          if (dateStr.hour !== undefined && dateStr.minute !== undefined) {
            const hour = String(dateStr.hour).padStart(2, '0');
            const minute = String(dateStr.minute).padStart(2, '0');
            return new Date(`${dateStr2}T${hour}:${minute}:00`);
          }
          return new Date(dateStr2);
        }
      }

      if (typeof dateStr === 'string') {
        return new Date(dateStr);
      }

      return null;
    } catch (e) {
      return null;
    }
  }

  trackByEvent(index: number, ev: any) {
    return ev.uid || ev.id || ev.summary || index;
  }

  getTitleColor(): string {
    if (this.widget.colorOverrides?.widgetTitleTextColor) {
      return this.widget.colorOverrides.widgetTitleTextColor;
    }
    return this.colorScheme?.widgetTitleTextColor || this.colorScheme?.text || 'currentColor';
  }

  getTextColor(): string {
    if (this.widget.colorOverrides?.widgetTextColor) {
      return this.widget.colorOverrides.widgetTextColor;
    }
    return this.colorScheme?.widgetTextColor || this.colorScheme?.text || 'currentColor';
  }

  getIconColor(): string {
    if (this.widget.colorOverrides?.iconColor) {
      return this.widget.colorOverrides.iconColor;
    }
    return this.colorScheme?.iconColor || this.colorScheme?.accent || 'currentColor';
  }
}
