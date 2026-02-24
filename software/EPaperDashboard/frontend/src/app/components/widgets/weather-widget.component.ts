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
  WeatherConfig,
  WeatherItemConfig,
  DEFAULT_WEATHER_ITEMS,
  defaultWeatherItemIcon,
  DashboardLayout,
} from '../../models/types';

// ─── edit-mode types ──────────────────────────────────────────────────────────

type ResizeHandle = 'n' | 'ne' | 'e' | 'se' | 's' | 'sw' | 'w' | 'nw';
const ALL_HANDLES: ResizeHandle[] = ['n', 'ne', 'e', 'se', 's', 'sw', 'w', 'nw'];

interface EPos { x: number; y: number; w: number; h: number; }
interface Guide { orientation: 'h' | 'v'; position: number; }

@Component({
  selector: 'app-widget-weather',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./weather-widget.component.scss'],
  template: `
    <div #host class="weather-widget" [class.ww-editing]="internalEdit"
         [style.--titleFontSize]="getTitleFontSize() + 'px'"
         [style.--textFontSize]="getTextFontSize() + 'px'"
         [style.--titleFontWeight]="getTitleFontWeight()"
         [style.--textFontWeight]="getTextFontWeight()"
         [style.--titleColor]="getTitleColor()"
         [style.--textColor]="getTextColor()"
         [style.--iconColor]="getIconColor()"
         [style.color]="getTextColor()"
         (mousedown)="onHostMouseDown($event)">

      <!-- ── Guide lines (edit mode only) ─────────────────────────────── -->
      @if (internalEdit) {
        @for (g of activeGuides; track $index) {
          <div [class]="'ww-guide ww-guide-' + g.orientation"
               [style.left.%]="g.orientation === 'v' ? g.position : undefined"
               [style.top.%]="g.orientation === 'h' ? g.position : undefined">
          </div>
        }
      }

      @if (!isDataFetched() && !internalEdit) {
        <div class="preview-state">
          <i class="fa fa-cloud-sun"></i>
          <p>Weather</p>
        </div>
      }

      @if (isDataFetched() || internalEdit) {
        @for (item of visibleItems(); track item.type + (item.attributeKey ?? ''); let i = $index) {
          <div class="ww-item"
               [class.ww-edit-el]="internalEdit"
               [class.ww-el-selected]="internalEdit && selectedId === itemId(item, i)"
               [style.left.%]="getElPos(itemId(item, i), item, i).x"
               [style.top.%]="getElPos(itemId(item, i), item, i).y"
               [style.width.%]="getElPos(itemId(item, i), item, i).w"
               [style.height.%]="getElPos(itemId(item, i), item, i).h"
               (mousedown)="onElementMouseDown($event, itemId(item, i))">

            @switch (item.type) {
              @case ('title') {
                <span class="ww-title"
                      [style.fontSize.px]="getTitleFontSize()"
                      [style.fontWeight]="getTitleFontWeight()"
                      [style.color]="getTitleColor()">
                  {{ widget.titleOverride || 'Weather' }}
                </span>
              }
              @case ('temperature') {
                <span class="ww-value ww-with-icon"
                      [style.fontSize.px]="getTextFontSize()"
                      [style.fontWeight]="getTextFontWeight()"
                      [style.color]="getTextColor()">
                  <i class="fa {{ resolveIcon(item) }}" [style.color]="getIconColor()"></i>
                  {{ getAttr('temperature') ? getAttr('temperature') + '°' : (internalEdit ? '21°' : '') }}
                </span>
              }
              @case ('condition') {
                <span class="ww-value ww-with-icon"
                      [style.fontSize.px]="getTextFontSize()"
                      [style.fontWeight]="getTextFontWeight()"
                      [style.color]="getTextColor()">
                  <i class="fa {{ resolveIcon(item) }}" [style.color]="getIconColor()"></i>
                  {{ getState() || (internalEdit ? 'Sunny' : '') }}
                </span>
              }
              @case ('pressure') {
                <span class="ww-value ww-with-icon"
                      [style.fontSize.px]="getTextFontSize()"
                      [style.fontWeight]="getTextFontWeight()"
                      [style.color]="getTextColor()">
                  <i class="fa {{ resolveIcon(item) }}" [style.color]="getIconColor()"></i>
                  {{ getAttr('pressure') || (internalEdit ? '1013' : '') }}
                </span>
              }
              @case ('attribute') {
                <span class="ww-value ww-with-icon"
                      [style.fontSize.px]="getTextFontSize()"
                      [style.fontWeight]="getTextFontWeight()"
                      [style.color]="getTextColor()">
                  <i class="fa {{ resolveIcon(item) }}" [style.color]="getIconColor()"></i>
                  {{ getAttr(item.attributeKey) ?? (internalEdit ? (item.label || item.attributeKey || 'Attr') : '') }}
                  @if (getAttr(item.attributeKey) && item.attributeKey === 'humidity') {
                    %
                  }
                </span>
              }
            }

            <!-- resize handles when selected in edit mode -->
            @if (internalEdit && selectedId === itemId(item, i)) {
              @for (h of handles; track h) {
                <div class="ww-handle" [class]="'ww-handle-' + h"
                     (mousedown)="onHandleMouseDown($event, itemId(item, i), h)"></div>
              }
            }
          </div>
        }
      }
    </div>
  `,
})
export class WeatherWidgetComponent implements OnChanges, OnDestroy {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() entityStates: Record<string, HassEntityState> | null = null;
  @Input() designerSettings?: DashboardLayout;
  /** When true, the widget becomes its own drag-and-drop layout editor. */
  @Input() internalEdit = false;
  @Output() internalLayoutChanged = new EventEmitter<WeatherConfig>();

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

  private readonly boundMouseMove = this.onDocMouseMove.bind(this);
  private readonly boundMouseUp   = this.onDocMouseUp.bind(this);

  get config(): WeatherConfig {
    return (this.widget?.config || {}) as WeatherConfig;
  }

  constructor(
    private cdr: ChangeDetectorRef,
    private zone: NgZone,
  ) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['internalEdit']) {
      if (this.internalEdit) {
        this.syncEditPositions();
      } else {
        this.editPositions.clear();
        this.selectedId = null;
        this.activeGuides = [];
      }
    }
  }

  ngOnDestroy(): void {
    document.removeEventListener('mousemove', this.boundMouseMove);
    document.removeEventListener('mouseup',   this.boundMouseUp);
  }

  // ─── items ────────────────────────────────────────────────────────────────
  getItems(): WeatherItemConfig[] {
    return this.config.items?.length ? this.config.items : DEFAULT_WEATHER_ITEMS;
  }

  visibleItems(): WeatherItemConfig[] {
    return this.getItems().filter(i => i.visible !== false);
  }

  itemId(item: WeatherItemConfig, index: number): string {
    return item.type === 'attribute' ? `attribute-${item.attributeKey ?? index}` : item.type;
  }

  // ─── data helpers ─────────────────────────────────────────────────────────
  isDataFetched(): boolean {
    const entityId = this.config.entityId;
    if (!entityId) return false;
    const state = this.getEntityState(entityId);
    if (!state || !state.attributes) return false;
    return state.attributes['temperature'] !== undefined && state.attributes['temperature'] !== null;
  }

  getEntityState(entityId?: string) {
    if (!entityId || !this.entityStates) return null;
    return this.entityStates[entityId] ?? null;
  }

  getState(): string {
    return this.getEntityState(this.config.entityId)?.state ?? '';
  }

  getAttr(key?: string): string | null {
    if (!key) return null;
    const state = this.getEntityState(this.config.entityId);
    const val = state?.attributes?.[key];
    return val !== undefined && val !== null ? String(val) : null;
  }

  // ─── position snapshot ────────────────────────────────────────────────────
  private syncEditPositions(): void {
    const m = new Map<string, EPos>();
    const items = this.getItems();
    const defaults = DEFAULT_WEATHER_ITEMS;
    items.forEach((item, i) => {
      const id = this.itemId(item, i);
      const def = defaults.find(d => d.type === item.type) ?? defaults[0];
      m.set(id, {
        x: item.x ?? def.x ?? 0,
        y: item.y ?? def.y ?? 0,
        w: item.w ?? def.w ?? 100,
        h: item.h ?? def.h ?? 20,
      });
    });
    this.editPositions = m;
    this.cdr.markForCheck();
  }

  // ─── position resolver ────────────────────────────────────────────────────
  getElPos(id: string, item?: WeatherItemConfig, i?: number): EPos {
    if (this.internalEdit && this.editPositions.has(id)) {
      return this.editPositions.get(id)!;
    }
    const def = DEFAULT_WEATHER_ITEMS.find(d => d.type === item?.type) ?? DEFAULT_WEATHER_ITEMS[0];
    return {
      x: item?.x ?? def.x ?? 0,
      y: item?.y ?? def.y ?? 0,
      w: item?.w ?? def.w ?? 100,
      h: item?.h ?? def.h ?? 20,
    };
  }

  // ─── mouse handlers ───────────────────────────────────────────────────────
  onHostMouseDown(event: MouseEvent): void {
    if (!this.internalEdit) return;
    if (event.target === this.hostRef?.nativeElement) {
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

  // ─── document-level mouse events ─────────────────────────────────────────
  private attachDocListeners(): void {
    this.zone.runOutsideAngular(() => {
      document.addEventListener('mousemove', this.boundMouseMove);
      document.addEventListener('mouseup',   this.boundMouseUp);
    });
  }

  private onDocMouseMove(event: MouseEvent): void {
    const host = this.hostRef?.nativeElement;
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

    this.zone.run(() => this.cdr.markForCheck());
  }

  private onDocMouseUp(_event: MouseEvent): void {
    document.removeEventListener('mousemove', this.boundMouseMove);
    document.removeEventListener('mouseup',   this.boundMouseUp);

    const wasDragging = !!(this.dragState || this.resizeState);
    this.dragState   = null;
    this.resizeState = null;
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
    const updated: WeatherConfig = { ...this.config };
    const items = [...this.getItems()];

    items.forEach((item, i) => {
      const id = this.itemId(item, i);
      const bp = this.editPositions.get(id);
      if (bp) {
        item.x = r(bp.x);
        item.y = r(bp.y);
        item.w = r(bp.w);
        item.h = r(bp.h);
      }
    });

    updated.items = items;
    this.internalLayoutChanged.emit(updated);
  }

  // ─── style helpers ────────────────────────────────────────────────────────
  getTextFontSize(): number {
    return this.designerSettings?.textFontSize ?? 12;
  }

  getTitleFontSize(): number {
    return this.designerSettings?.titleFontSize ?? 15;
  }

  getTitleFontWeight(): number {
    return this.designerSettings?.titleFontWeight ?? 700;
  }

  getTextFontWeight(): number {
    return this.designerSettings?.textFontWeight ?? 400;
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

  /** Resolve the icon for a weather item: use custom icon if set, otherwise fall back to default. */
  resolveIcon(item: WeatherItemConfig): string {
    return item.icon || defaultWeatherItemIcon(item.type, item.attributeKey);
  }
}
