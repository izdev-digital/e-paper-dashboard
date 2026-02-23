import {
  Component,
  Input,
  Output,
  EventEmitter,
  OnInit,
  OnChanges,
  SimpleChanges,
  ChangeDetectorRef,
  NgZone,
  ViewChild,
  ElementRef,
  OnDestroy,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import {
  WidgetConfig,
  HeaderConfig,
  BadgeConfig,
  ColorScheme,
  HassEntityState,
  DashboardLayout,
} from '../../models/types';

/** Module-level SVG text cache – fetched once across all component instances. */
const svgTextCache: { text: string | null; pending: Promise<string | null> | null } = {
  text: null,
  pending: null,
};

// ─── edit-mode types ──────────────────────────────────────────────────────────

type ResizeHandle = 'n' | 'ne' | 'e' | 'se' | 's' | 'sw' | 'w' | 'nw';
const ALL_HANDLES: ResizeHandle[] = ['n', 'ne', 'e', 'se', 's', 'sw', 'w', 'nw'];

interface EPos { x: number; y: number; w: number; h: number; }
interface Guide { orientation: 'h' | 'v'; position: number; }

// ─── component ────────────────────────────────────────────────────────────────

@Component({
  selector: 'app-widget-header',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./header-widget.component.scss'],
  template: `
    <div #host class="header-widget" [class.hw-editing]="internalEdit"
         (mousedown)="onHostMouseDown($event)">

      <!-- ── Guide lines (edit mode only) ─────────────────────────────── -->
      @if (internalEdit) {
        @for (g of activeGuides; track $index) {
          <div [class]="'hw-guide hw-guide-' + g.orientation"
               [style.left.%]="g.orientation === 'v' ? g.position : undefined"
               [style.top.%]="g.orientation === 'h' ? g.position : undefined">
          </div>
        }
      }

      <!-- ── Title (icon + text) ──────────────────────────────────────── -->
      <div class="title-section"
           [class.hw-edit-el]="internalEdit"
           [class.hw-el-selected]="internalEdit && selectedId === 'title'"
           [style.left.%]="getElPos('title').x"
           [style.top.%]="getElPos('title').y"
           [style.width.%]="getElPos('title').w"
           [style.height.%]="getElPos('title').h"
           [style.color]="getTitleColor()"
           (mousedown)="onElementMouseDown($event, 'title')">

        @if (isIconOnLeft() && inlineSvg) {
          <div class="header-icon"
               [innerHTML]="inlineSvg"
               [style.width.px]="cfg.iconSize ?? 32"
               [style.height.px]="cfg.iconSize ?? 32"
               [style.--accent-color]="getIconColor()"></div>
        }
        <div class="title"
             [style.fontSize.px]="getTitleFontSize()"
             [style.fontWeight]="getTitleFontWeight()">{{ cfg.title }}</div>
        @if (!isIconOnLeft() && inlineSvg) {
          <div class="header-icon"
               [innerHTML]="inlineSvg"
               [style.width.px]="cfg.iconSize ?? 32"
               [style.height.px]="cfg.iconSize ?? 32"
               [style.--accent-color]="getIconColor()"></div>
        }

        <!-- resize handles when selected in edit mode -->
        @if (internalEdit && selectedId === 'title') {
          @for (h of handles; track h) {
            <div class="hw-handle" [class]="'hw-handle-' + h"
                 (mousedown)="onHandleMouseDown($event, 'title', h)"></div>
          }
        }
      </div>

      <!-- ── Badges ──────────────────────────────────────────────────── -->
      @for (badge of visibleBadges(); track $index; let i = $index) {
        <span class="badge"
              [class.hw-edit-el]="internalEdit"
              [class.hw-el-selected]="internalEdit && selectedId === 'badge-' + i"
              [style.left.%]="getElPos('badge-' + i, badge, i).x"
              [style.top.%]="getElPos('badge-' + i, badge, i).y"
              [style.width.%]="getElPos('badge-' + i, badge, i).w"
              [style.height.%]="getElPos('badge-' + i, badge, i).h"
              [style.fontSize.px]="getTextFontSize()"
              [style.fontWeight]="getTextFontWeight()"
              [style.color]="getTextColor()"
              (mousedown)="onElementMouseDown($event, 'badge-' + i)">

          @if (badge.icon) {
            <i class="fa {{ badge.icon }}" [style.color]="getIconColor()"></i>
          }
          @if (badge.entityId) {
            <span class="badge-text">
              {{ getEntityState(badge.entityId)?.state || '' }}
              @if (getEntityAttribute(badge.entityId, 'unit_of_measurement')) {
                {{ getEntityAttribute(badge.entityId, 'unit_of_measurement') }}
              }
            </span>
          }

          <!-- resize handles when selected in edit mode -->
          @if (internalEdit && selectedId === 'badge-' + i) {
            @for (h of handles; track h) {
              <div class="hw-handle" [class]="'hw-handle-' + h"
                   (mousedown)="onHandleMouseDown($event, 'badge-' + i, h)"></div>
            }
          }
        </span>
      }

    </div>
  `,
})
export class HeaderWidgetComponent implements OnInit, OnChanges, OnDestroy {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() entityStates: Record<string, HassEntityState> | null = null;
  @Input() designerSettings?: DashboardLayout;
  /** When true, the widget becomes its own drag-and-drop layout editor. */
  @Input() internalEdit = false;
  @Output() internalLayoutChanged = new EventEmitter<HeaderConfig>();

  @ViewChild('host') hostRef!: ElementRef<HTMLDivElement>;

  inlineSvg: SafeHtml | null = null;

  // ─── edit-mode state ───────────────────────────────────────────────────────
  handles = ALL_HANDLES;
  /** Live positions during editing; keyed by 'title' | 'badge-0' | … */
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

  get cfg(): HeaderConfig { return this.widget.config as HeaderConfig; }

  constructor(
    private sanitizer: DomSanitizer,
    private cdr: ChangeDetectorRef,
    private zone: NgZone,
  ) {}

  ngOnInit(): void { this.loadSvg(); }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['colorScheme']) {
      this.applySvgColor();
    } else if (changes['widget']) {
      this.loadSvg();
    }
    // When edit mode is toggled on, snapshot current positions into editPositions
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

  // ─── position snapshot ────────────────────────────────────────────────────
  private syncEditPositions(): void {
    const m = new Map<string, EPos>();
    m.set('title', {
      x: this.cfg.titleX ?? this.defaultTitleX(),
      y: this.cfg.titleY ?? this.defaultTitleY(),
      w: this.cfg.titleW ?? 42,
      h: this.cfg.titleH ?? 50,
    });
    (this.cfg.badges ?? []).forEach((badge, i) => {
      m.set(`badge-${i}`, {
        x: badge.x ?? this.autoX(i),
        y: badge.y ?? this.autoY(i),
        w: badge.w ?? 22,
        h: badge.h ?? 30,
      });
    });
    this.editPositions = m;
    this.cdr.markForCheck();
  }

  // ─── position resolver ────────────────────────────────────────────────────
  /** Returns live edit position if editing, otherwise config position. */
  getElPos(id: string, badge?: BadgeConfig, i?: number): EPos {
    if (this.internalEdit && this.editPositions.has(id)) {
      return this.editPositions.get(id)!;
    }
    if (id === 'title') {
      return {
        x: this.cfg.titleX ?? this.defaultTitleX(),
        y: this.cfg.titleY ?? this.defaultTitleY(),
        w: this.cfg.titleW ?? 42,
        h: this.cfg.titleH ?? 50,
      };
    }
    // badge
    const idx = i ?? 0;
    return {
      x: badge?.x ?? this.autoX(idx),
      y: badge?.y ?? this.autoY(idx),
      w: badge?.w ?? 22,
      h: badge?.h ?? 30,
    };
  }

  // ─── mouse handlers ───────────────────────────────────────────────────────

  /** Click on the host background deselects the current element. */
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
    const rect    = host.getBoundingClientRect();
    const snapStep  = this.cfg.snapStep  ?? 2;
    const showGuides = this.cfg.showGuides ?? true;

    if (this.dragState) {
      const s   = this.dragState;
      const pos = this.editPositions.get(s.id);
      if (!pos) return;
      const dxPct = ((event.clientX - s.startMouseX) / rect.width)  * 100;
      const dyPct = ((event.clientY - s.startMouseY) / rect.height) * 100;

      let newX = s.startX + dxPct;
      let newY = s.startY + dyPct;

      // smart guides
      const guides: Guide[] = [];
      if (showGuides) {
        const others = Array.from(this.editPositions.entries())
          .filter(([k]) => k !== s.id).map(([, v]) => v);
        const snapped = this.computeSmartGuides(newX, newY, pos.w, pos.h, others, guides, snapStep);
        newX = snapped.x;
        newY = snapped.y;
      }
      this.activeGuides = guides;

      // grid snap
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
    const updated: HeaderConfig = { ...this.cfg };

    const tp = this.editPositions.get('title');
    if (tp) { updated.titleX = r(tp.x); updated.titleY = r(tp.y); updated.titleW = r(tp.w); updated.titleH = r(tp.h); }

    if (updated.badges) {
      updated.badges = updated.badges.map((badge, i) => {
        const bp = this.editPositions.get(`badge-${i}`);
        return bp ? { ...badge, x: r(bp.x), y: r(bp.y), w: r(bp.w), h: r(bp.h) } : badge;
      });
    }
    this.internalLayoutChanged.emit(updated);
  }

  // ─── SVG loading ──────────────────────────────────────────────────────────
  private async loadSvg(): Promise<void> {
    if (!svgTextCache.text) {
      if (!svgTextCache.pending) {
        svgTextCache.pending = fetch('/icon-tab-dynamic.svg')
          .then(r => r.ok ? r.text() : null)
          .catch(() => null)
          .then(text => { svgTextCache.text = text; svgTextCache.pending = null; return text; });
      }
      await svgTextCache.pending;
    }
    this.applySvgColor();
  }

  private applySvgColor(): void {
    const raw = svgTextCache.text;
    if (!raw) return;
    const accent  = this.getIconColor();
    const patched = raw.replace(/--accent-color:\s*#[0-9a-fA-F]{3,8};/gi, `--accent-color: ${accent};`);
    this.inlineSvg = this.sanitizer.bypassSecurityTrustHtml(patched);
    this.cdr.markForCheck();
  }

  // ─── default / auto positions ─────────────────────────────────────────────
  private defaultTitleX(): number { return (this.cfg.iconPosition ?? 'left') === 'right' ? 0 : 58; }
  private defaultTitleY(): number { return 0; }
  private autoX(i: number): number { return (i % 4) * 22; }
  private autoY(i: number): number { return Math.floor(i / 4) * 30; }

  // ─── style helpers ────────────────────────────────────────────────────────
  getTitleFontSize():   number { return this.designerSettings?.titleFontSize   ?? 16; }
  getTextFontSize():    number { return this.designerSettings?.textFontSize    ?? 14; }
  getTitleFontWeight(): number { return this.designerSettings?.titleFontWeight ?? 700; }
  getTextFontWeight():  number { return this.designerSettings?.textFontWeight  ?? 400; }

  getTitleColor(): string {
    return this.widget.colorOverrides?.widgetTitleTextColor
      || this.colorScheme?.widgetTitleTextColor || this.colorScheme?.text || 'currentColor';
  }
  getTextColor(): string {
    return this.widget.colorOverrides?.widgetTextColor
      || this.colorScheme?.widgetTextColor || this.colorScheme?.text || 'currentColor';
  }
  getIconColor(): string {
    return this.widget.colorOverrides?.iconColor
      || this.colorScheme?.iconColor || this.colorScheme?.accent || 'currentColor';
  }

  isIconOnLeft(): boolean {
    return (this.cfg.iconPosition ?? 'left') === 'left';
  }

  // ─── entity helpers ───────────────────────────────────────────────────────
  getEntityState(entityId?: string) {
    if (!entityId || !this.entityStates) return null;
    return this.entityStates[entityId] ?? null;
  }
  getEntityAttribute(entityId?: string, attr?: string) {
    const st = this.getEntityState(entityId);
    return (st?.attributes && attr) ? (st.attributes[attr] ?? null) : null;
  }
  visibleBadges(): BadgeConfig[] {
    return (this.cfg.badges ?? []).filter(b =>
      b && ((b.entityId && b.entityId.trim()) || (b.icon && b.icon.trim()))
    );
  }

  // ─── misc ─────────────────────────────────────────────────────────────────
  f(n: number): string { return n.toFixed(1); }
}
