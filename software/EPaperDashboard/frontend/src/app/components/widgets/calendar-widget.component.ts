import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, HassEntityState, CalendarConfig, DashboardLayout } from '../../models/types';

@Component({
  selector: 'app-widget-calendar',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./calendar-widget.component.scss'],
  template: `
    <div class="calendar-widget" [style.--headerFontSize]="getHeaderFontSize() + 'px'" [style.--eventFontSize]="getEventFontSize() + 'px'" [style.--iconColor]="getIconColor()" [style.--titleColor]="getTitleColor()" [style.--textColor]="getTextColor()" [style.color]="getTextColor()">
      @if (!getEntityState(config.entityId)) {
        <div class="empty-state">
          <i class="fa fa-calendar"></i>
          <p>Not configured</p>
        </div>
      }
      @if (getEntityState(config.entityId)) {
        <div class="calendar-content">
          <h4>Events</h4>
          @if (getUpcomingEvents(config.entityId).length > 0) {
            @for (ev of getUpcomingEvents(config.entityId); track trackByEvent($index, ev)) {
              <div class="calendar-event">
                <div class="event-datetime">
                  <i class="fa fa-clock"></i>
                  <span>{{ formatEventDate(ev) }}</span>
                </div>
                <div class="event-title">{{ ev.summary || ev.title || ev.description || '-' }}</div>
                @if (ev.location) {
                  <div class="event-location">
                    <i class="fa fa-map-marker-alt"></i>
                    <span>{{ ev.location }}</span>
                  </div>
                }
              </div>
            }
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
export class CalendarWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() entityStates: Record<string, HassEntityState> | null = null;
  @Input() calendarEventsByEntityId: Record<string, any[]> | undefined;
  @Input() designerSettings?: DashboardLayout;

  get config(): CalendarConfig { return (this.widget?.config || {}) as CalendarConfig; }

  getHeaderFontSize(): number {
    return this.designerSettings?.titleFontSize ?? 15;
  }

  getEventFontSize(): number {
    return this.designerSettings?.textFontSize ?? 12;
  }

  getEntityState(entityId?: string) {
    if (!entityId || !this.entityStates) return null;
    return this.entityStates[entityId] ?? null;
  }

  /**
   * Extracts upcoming events from calendar events data.
   * Events are fetched from the parent component via the API.
   * Falls back to entity state attributes for compatibility.
   */
  getUpcomingEvents(entityId?: string) {
    if (!entityId) return [];
    
    // First try to get events from the calendarEventsByEntityId input
    if (this.calendarEventsByEntityId && this.calendarEventsByEntityId[entityId]) {
      const events = this.calendarEventsByEntityId[entityId];
      const max = Math.max(1, (this.config.maxEvents as number) || 7);
      return (Array.isArray(events) ? events : [])
        .filter(ev => this.isUpcomingEvent(ev))
        .slice(0, max);
    }

    // Fallback to entity state attributes for backward compatibility
    const state = this.getEntityState(entityId);
    if (!state) return [];
    
    const attrs = state.attributes || {};
    
    // Try multiple attribute names for events (different integrations use different names)
    const eventsList = 
      attrs['events'] || 
      attrs['entries'] || 
      attrs['calendar_events'] ||
      attrs['upcoming_events'] ||
      attrs['data'] || 
      [];

    const max = Math.max(1, (this.config.maxEvents as number) || 7);
    
    // Ensure we have an array and filter to upcoming events
    const events = (Array.isArray(eventsList) ? eventsList : [])
      .filter(ev => this.isUpcomingEvent(ev))
      .slice(0, max);

    return events;
  }

  /**
   * Checks if an event is upcoming or currently ongoing (not fully in the past).
   * Handles various date formats from different calendar integrations.
   */
  private isUpcomingEvent(event: any): boolean {
    if (!event) return false;

    try {
      const startStr = event.start || event.start_time || event.begin || event.datetime || event.dtstart;
      const endStr = event.end || event.end_time || event.finish || event.end_datetime || event.dtend;
      
      if (!startStr) return false;

      // Parse the dates
      const startDate = this.parseEventDate(startStr);
      if (!startDate) return false;

      const now = new Date();
      
      // Include events that are currently happening (started in the past, haven't ended yet)
      if (endStr) {
        const endDate = this.parseEventDate(endStr);
        if (endDate && endDate > now) {
          return true; // Event is ongoing or starts in the future
        }
      }
      
      // Include events starting from now onwards
      return startDate >= now;
    } catch {
      return false;
    }
  }

  /**
   * Formats an event date for display with proper handling of all-day events.
   * Shows date and time for regular events, just date for all-day events.
   */
  formatEventDate(ev: any): string {
    if (!ev) return '';

    const start = ev.start || ev.start_time || ev.begin || ev.datetime || ev.dtstart;
    if (!start) return '';

    try {
      const d = this.parseEventDate(start);
      if (!d) return String(start);

      // Check if this is an all-day event (date-only format: YYYY-MM-DD)
      if (typeof start === 'string' && start.length === 10 && /^\d{4}-\d{2}-\d{2}$/.test(start)) {
        return d.toLocaleDateString(navigator.language, {
          weekday: 'short',
          month: 'short',
          day: 'numeric'
        });
      }

      // Regular event with time
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

  /**
   * Parses various date/time formats that Home Assistant calendar entities can provide.
   */
  private parseEventDate(dateStr: any): Date | null {
    if (!dateStr) return null;

    try {
      // If it's already an object with date properties
      if (typeof dateStr === 'object' && dateStr !== null) {
        if (dateStr instanceof Date) return dateStr;
        
        // Try to create from ISO string if available
        if (dateStr.isoformat) {
          return new Date(dateStr.isoformat);
        }
        
        // Try to create from components
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

      // Handle string dates
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
