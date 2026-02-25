import {
  Component,
  Input,
  Output,
  EventEmitter,
  OnChanges,
  SimpleChanges,
  ChangeDetectorRef,
  NgZone,
  ViewChild,
  ElementRef,
  OnDestroy,
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

// ─── edit-mode types ──────────────────────────────────────────────────────────
type ResizeHandle = 'n' | 'ne' | 'e' | 'se' | 's' | 'sw' | 'w' | 'nw';
const ALL_HANDLES: ResizeHandle[] = ['n', 'ne', 'e', 'se', 's', 'sw', 'w', 'nw'];
interface EPos { x: number; y: number; w: number; h: number; }
interface Guide { orientation: 'h' | 'v'; position: number; }

@Component({
  selector: 'app-widget-calendar',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./calendar-widget.component.scss'],
  template: `
    <div #host class="calendar-widget"
         [class.cw-editing]="internalEdit"
         [style.--headerFontSize]="getHeaderFontSize() + 'px'"
         [style.--eventFontSize]="getEventFontSize() + 'px'"
         [style.--headerFontWeight]="getHeaderFontWeight()"
         [style.--eventFontWeight]="getEventFontWeight()"
         [style.--iconColor]="getIconColor()"
         [style.--titleColor]="getTitleColor()"
         [style.--textColor]="getTextColor()"
         [style.color]="getTextColor()">

      <!-- ── Edit mode: single event entry template ────────────────────── -->
      @if (internalEdit) {
        <div class="cw-edit-container"
             [style.height.px]="editContainerHeight"
             (mousedown)="onHostMouseDown($event)">
          @for (g of activeGuides; track $index) {
            <div [class]="'cw-guide cw-guide-' + g.orientation"
                 [style.left.%]="g.orientation === 'v' ? g.position : undefined"
                 [style.top.%]="g.orientation === 'h' ? g.position : undefined">
            </div>
          }

          @for (item of visibleItems(); track item.type; let i = $index) {
            <div class="cw-item cw-edit-el"
                 [class.cw-el-selected]="selectedId === item.type"
                 [style.left.%]="getElPos(item.type, item, i).x"
                 [style.top.%]="getElPos(item.type, item, i).y"
                 [style.width.%]="getElPos(item.type, item, i).w"
                 [style.height.%]="getElPos(item.type, item, i).h"
                 (mousedown)="onElementMouseDown($event, item.type)">

              @switch (item.type) {
                @case ('datetime') {
                  <span class="cw-value cw-with-icon"
                        [style.fontSize.px]="getEventFontSize()"
                        [style.fontWeight]="getEventFontWeight()">
                    <i class="fa {{ resolveIcon(item) }}" [style.color]="getIconColor()"></i>
                    <span class="cw-text">Feb 25, 10:00 AM</span>
                  </span>
                }
                @case ('title') {
                  <span class="cw-value cw-with-icon"
                        [style.fontSize.px]="getEventFontSize()"
                        [style.fontWeight]="getEventFontWeight()">
                    <i class="fa {{ resolveIcon(item) }}" [style.color]="getIconColor()"></i>
                    <span class="cw-text">Sample Event Title</span>
                  </span>
                }
                @case ('location') {
                  <span class="cw-value cw-with-icon"
                        [style.fontSize.px]="getEventFontSize()"
                        [style.fontWeight]="getEventFontWeight()">
                    <i class="fa {{ resolveIcon(item) }}" [style.color]="getIconColor()"></i>
                    <span class="cw-text">Conference Room A</span>
                  </span>
                }
                @case ('description') {
                  <span class="cw-value cw-with-icon"
                        [style.fontSize.px]="getEventFontSize()"
                        [style.fontWeight]="getEventFontWeight()">
                    <i class="fa {{ resolveIcon(item) }}" [style.color]="getIconColor()"></i>
                    <span class="cw-text">Weekly team standup meeting</span>
                  </span>
                }
              }

              @if (selectedId === item.type) {
                @for (h of handles; track h) {
                  <div class="cw-handle" [class]="'cw-handle-' + h"
                       (mousedown)="onHandleMouseDown($event, item.type, h)"></div>
                }
              }
            </div>
          }

          <!-- Bottom-edge handle to resize event entry height -->
          <div class="cw-container-handle-s"
               (mousedown)="onContainerResizeMouseDown($event)"></div>
        </div>
      }

      <!-- ── Normal mode ───────────────────────────────────────────────── -->
      @if (!internalEdit) {
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
                <div class="calendar-event"
                     [style.height.px]="config.eventHeight || null"
                     [style.flex]="config.eventHeight ? '0 0 auto' : null">
                  @for (item of visibleItems(); track item.type; let idx = $index) {
                    <div class="cw-item"
                         [style.left.%]="getElPos(item.type, item, idx).x"
                         [style.top.%]="getElPos(item.type, item, idx).y"
                         [style.width.%]="getElPos(item.type, item, idx).w"
                         [style.height.%]="getElPos(item.type, item, idx).h">
                      @switch (item.type) {
                        @case ('datetime') {
                          <span class="cw-value cw-with-icon"
                                [style.fontSize.px]="getEventFontSize()"
                                [style.fontWeight]="getEventFontWeight()">
                            <i class="fa {{ resolveIcon(item) }}" [style.color]="getIconColor()"></i>
                            <span class="cw-text">{{ formatEventDate(ev) }}</span>
                          </span>
                        }
                        @case ('title') {
                          <span class="cw-value cw-with-icon"
                                [style.fontSize.px]="getEventFontSize()"
                                [style.fontWeight]="getEventFontWeight()">
                            <i class="fa {{ resolveIcon(item) }}" [style.color]="getIconColor()"></i>
                            <span class="cw-text">{{ ev.summary || ev.title || ev.description || '-' }}</span>
                          </span>
                        }
                        @case ('location') {
                          @if (ev.location) {
                            <span class="cw-value cw-with-icon"
                                  [style.fontSize.px]="getEventFontSize()"
                                  [style.fontWeight]="getEventFontWeight()">
                              <i class="fa {{ resolveIcon(item) }}" [style.color]="getIconColor()"></i>
                              <span class="cw-text">{{ ev.location }}</span>
                            </span>
                          }
                        }
                        @case ('description') {
                          @if (ev.description) {
                            <span class="cw-value cw-with-icon"
                                  [style.fontSize.px]="getEventFontSize()"
                                  [style.fontWeight]="getEventFontWeight()">
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
      }
    </div>
  `
})
export class CalendarWidgetComponent implements OnChanges, OnDestroy {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() entityStates: Record<string, HassEntityState> | null = null;
  @Input() calendarEventsByEntityId: Record<string, any[]> | undefined;
  @Input() designerSettings?: DashboardLayout;
  /** When true, the widget becomes its own drag-and-drop layout editor for event entries. */
  @Input() internalEdit = false;
  @Output() internalLayoutChanged = new EventEmitter<CalendarConfig>();

  @ViewChild('host') hostRef!: ElementRef<HTMLDivElement>;

  // ─── edit-mode state ───────────────────────────────────────────────────────
  handles = ALL_HANDLES;
  editPositions = new Map<string, EPos>();
  selectedId: string | null = null;
  activeGuides: Guide[] = [];

  dragState: {
    id: string;
    startMouseX: number; startMouseY: number;
    startX: number; startY: number;
  } | null = null;

  resizeState: {
    id: string;
    handle: ResizeHandle;
    startMouseX: number; startMouseY: number;
    startPos: EPos;
  } | null = null;

  /** State for dragging the container bottom edge to adjust eventHeight */
  containerResizeState: {
    startMouseY: number;
    startHeight: number;
  } | null = null;

  /** Live container height during resize; falls back to config value */
  editContainerHeight: number | null = null;

  private readonly boundMouseMove = this.onDocMouseMove.bind(this);
  private readonly boundMouseUp   = this.onDocMouseUp.bind(this);

  get config(): CalendarConfig { return (this.widget?.config || {}) as CalendarConfig; }

  constructor(
    private cdr: ChangeDetectorRef,
    private zone: NgZone,
  ) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['internalEdit']) {
      if (this.internalEdit) {
        this.syncEditPositions();
        this.editContainerHeight = this.config.eventHeight || null;
      } else {
        this.editPositions.clear();
        this.selectedId = null;
        this.activeGuides = [];
        this.editContainerHeight = null;
      }
    }
  }

  ngOnDestroy(): void {
    document.removeEventListener('mousemove', this.boundMouseMove);
    document.removeEventListener('mouseup',   this.boundMouseUp);
  }

  // ─── items ────────────────────────────────────────────────────────────────
  getItems(): CalendarEventItemConfig[] {
    return this.config.items?.length ? this.config.items : DEFAULT_CALENDAR_EVENT_ITEMS;
  }

  visibleItems(): CalendarEventItemConfig[] {
    return this.getItems().filter(i => i.visible !== false);
  }

  // ─── position snapshot ────────────────────────────────────────────────────
  private syncEditPositions(): void {
    const m = new Map<string, EPos>();
    const items = this.getItems();
    const defaults = DEFAULT_CALENDAR_EVENT_ITEMS;
    items.forEach((item, i) => {
      const id = item.type;
      const def = defaults.find(d => d.type === item.type) ?? defaults[0];
      m.set(id, {
        x: item.x ?? def.x ?? 0,
        y: item.y ?? def.y ?? 0,
        w: item.w ?? def.w ?? 100,
        h: item.h ?? def.h ?? 25,
      });
    });
    this.editPositions = m;
    this.cdr.markForCheck();
  }

  // ─── position resolver ────────────────────────────────────────────────────
  getElPos(id: string, item?: CalendarEventItemConfig, i?: number): EPos {
    if (this.internalEdit && this.editPositions.has(id)) {
      return this.editPositions.get(id)!;
    }
    const def = DEFAULT_CALENDAR_EVENT_ITEMS.find(d => d.type === item?.type) ?? DEFAULT_CALENDAR_EVENT_ITEMS[0];
    return {
      x: item?.x ?? def.x ?? 0,
      y: item?.y ?? def.y ?? 0,
      w: item?.w ?? def.w ?? 100,
      h: item?.h ?? def.h ?? 25,
    };
  }

  // ─── mouse handlers ───────────────────────────────────────────────────────
  onHostMouseDown(event: MouseEvent): void {
    if (!this.internalEdit) return;
    const container = this.hostRef?.nativeElement?.querySelector('.cw-edit-container');
    if (event.target === container) {
      this.selectedId = null;
      this.cdr.markForCheck();
    }
  }

  onElementMouseDown(event: MouseEvent, id: string): void {
    if (!this.internalEdit) return;
    event.preventDefault();
    event.stopPropagation();
    this.selectedId = id;
    this.dragState = {
      id,
      startMouseX: event.clientX, startMouseY: event.clientY,
      startX: this.editPositions.get(id)?.x ?? 0,
      startY: this.editPositions.get(id)?.y ?? 0,
    };
    this.attachDocListeners();
    this.cdr.markForCheck();
  }

  onHandleMouseDown(event: MouseEvent, id: string, handle: ResizeHandle): void {
    event.preventDefault();
    event.stopPropagation();
    this.selectedId = id;
    const pos = this.editPositions.get(id)!;
    this.resizeState = {
      id, handle,
      startMouseX: event.clientX, startMouseY: event.clientY,
      startPos: { ...pos },
    };
    this.attachDocListeners();
    this.cdr.markForCheck();
  }

  /** Start dragging the bottom edge of the edit container to adjust eventHeight */
  onContainerResizeMouseDown(event: MouseEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.selectedId = null;
    const container = this.hostRef?.nativeElement?.querySelector('.cw-edit-container') as HTMLElement;
    if (!container) return;
    this.containerResizeState = {
      startMouseY: event.clientY,
      startHeight: container.getBoundingClientRect().height,
    };
    this.attachDocListeners();
    this.cdr.markForCheck();
  }

  // ─── document-level mouse events ─────────────────────────────────────────
  private attachDocListeners(): void {
    this.zone.runOutsideAngular(() => {
      document.addEventListener('mousemove', this.boundMouseMove);
      document.addEventListener('mouseup',   this.boundMouseUp);
    });
  }

  private onDocMouseMove(event: MouseEvent): void {
    const host = this.hostRef?.nativeElement?.querySelector('.cw-edit-container') as HTMLElement;
    if (!host) return;
    const rect     = host.getBoundingClientRect();
    const snapStep = this.config.snapStep ?? 2;
    const showGuides = this.config.showGuides ?? true;

    if (this.dragState) {
      const s   = this.dragState;
      const pos = this.editPositions.get(s.id);
      if (!pos) return;
      const dxPct = ((event.clientX - s.startMouseX) / rect.width)  * 100;
      const dyPct = ((event.clientY - s.startMouseY) / rect.height) * 100;

      let newX = s.startX + dxPct;
      let newY = s.startY + dyPct;

      const guides: Guide[] = [];
      if (showGuides) {
        const others = Array.from(this.editPositions.entries())
          .filter(([k]) => k !== s.id).map(([, v]) => v);
        const snapped = this.computeSmartGuides(newX, newY, pos.w, pos.h, others, guides, snapStep);
        newX = snapped.x;
        newY = snapped.y;
      }
      this.activeGuides = guides;

      if (snapStep > 0) {
        newX = Math.round(newX / snapStep) * snapStep;
        newY = Math.round(newY / snapStep) * snapStep;
      }

      pos.x = Math.max(0, Math.min(100 - pos.w, newX));
      pos.y = Math.max(0, Math.min(100 - pos.h, newY));
    }

    if (this.resizeState) {
      const s  = this.resizeState;
      const pos = this.editPositions.get(s.id);
      if (!pos) return;
      const dxPct = ((event.clientX - s.startMouseX) / rect.width)  * 100;
      const dyPct = ((event.clientY - s.startMouseY) / rect.height) * 100;
      const { x: ox, y: oy, w: ow, h: oh } = s.startPos;
      const h = s.handle;
      const MIN = Math.max(snapStep, 4);

      let x = ox, y = oy, w = ow, ht = oh;
      if (h.includes('e')) w  = Math.max(MIN, ow + dxPct);
      if (h.includes('s')) ht = Math.max(MIN, oh + dyPct);
      if (h.includes('w')) { w  = Math.max(MIN, ow - dxPct); x = ox + ow - w; }
      if (h.includes('n')) { ht = Math.max(MIN, oh - dyPct); y = oy + oh - ht; }

      if (snapStep > 0) {
        x  = Math.round(x  / snapStep) * snapStep;
        y  = Math.round(y  / snapStep) * snapStep;
        w  = Math.round(w  / snapStep) * snapStep || snapStep;
        ht = Math.round(ht / snapStep) * snapStep || snapStep;
      }

      pos.x = Math.max(0, x);
      pos.y = Math.max(0, y);
      pos.w = Math.min(100 - pos.x, w);
      pos.h = Math.min(100 - pos.y, ht);
      this.activeGuides = [];
    }

    if (this.containerResizeState) {
      const s = this.containerResizeState;
      const dy = event.clientY - s.startMouseY;
      this.editContainerHeight = Math.max(20, Math.round(s.startHeight + dy));
    }

    this.zone.run(() => this.cdr.markForCheck());
  }

  private onDocMouseUp(_event: MouseEvent): void {
    document.removeEventListener('mousemove', this.boundMouseMove);
    document.removeEventListener('mouseup',   this.boundMouseUp);

    const wasDragging = !!(this.dragState || this.resizeState || this.containerResizeState);
    this.dragState   = null;
    this.resizeState = null;
    this.containerResizeState = null;
    this.activeGuides = [];

    if (wasDragging) {
      this.zone.run(() => {
        this.emitConfig();
        this.cdr.markForCheck();
      });
    }
  }

  // ─── smart guides ─────────────────────────────────────────────────────────
  private computeSmartGuides(
    x: number, y: number, w: number, h: number,
    others: EPos[], guidesOut: Guide[], snapStep: number,
  ): { x: number; y: number } {
    const thr = Math.max(snapStep, 2);
    let outX = x, outY = y;
    const seenV = new Set<number>(), seenH = new Set<number>();

    for (const o of others) {
      const dxEdges  = [x, x + w / 2, x + w];
      const oxEdges  = [o.x, o.x + o.w / 2, o.x + o.w];
      for (let di = 0; di < dxEdges.length; di++) {
        for (const oe of oxEdges) {
          if (Math.abs(dxEdges[di] - oe) < thr) {
            outX = x + (oe - dxEdges[di]);
            if (!seenV.has(oe)) { guidesOut.push({ orientation: 'v', position: oe }); seenV.add(oe); }
          }
        }
      }
      const dyEdges  = [y, y + h / 2, y + h];
      const oyEdges  = [o.y, o.y + o.h / 2, o.y + o.h];
      for (let di = 0; di < dyEdges.length; di++) {
        for (const oe of oyEdges) {
          if (Math.abs(dyEdges[di] - oe) < thr) {
            outY = y + (oe - dyEdges[di]);
            if (!seenH.has(oe)) { guidesOut.push({ orientation: 'h', position: oe }); seenH.add(oe); }
          }
        }
      }
    }
    return { x: outX, y: outY };
  }

  // ─── emit updated config ──────────────────────────────────────────────────
  private emitConfig(): void {
    const r = (n: number) => Math.round(n * 10) / 10;
    const updated: CalendarConfig = { ...this.config };
    const items = [...this.getItems()];

    items.forEach((item) => {
      const id = item.type;
      const bp = this.editPositions.get(id);
      if (bp) {
        item.x = r(bp.x);
        item.y = r(bp.y);
        item.w = r(bp.w);
        item.h = r(bp.h);
      }
    });

    updated.items = items;
    // persist container height as eventHeight
    if (this.editContainerHeight && this.editContainerHeight > 0) {
      updated.eventHeight = this.editContainerHeight;
    }
    this.internalLayoutChanged.emit(updated);
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
