import {
  Component,
  Input,
  Output,
  EventEmitter,
  ElementRef,
  ViewChild,
  OnChanges,
  SimpleChanges,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  NgZone,
  OnDestroy,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HeaderConfig, BadgeConfig, ColorScheme } from '../../models/types';

// ─── internal types ───────────────────────────────────────────────────────────

export interface EditorElement {
  id: string;
  type: 'title' | 'badge';
  badgeIndex?: number;
  /** % of container [0–100] */
  x: number;
  y: number;
  w: number;
  h: number;
  label: string;
}

interface Guide {
  orientation: 'h' | 'v';
  /** % position of the guide line (left for 'v', top for 'h') */
  position: number;
}

type ResizeHandle = 'n' | 'ne' | 'e' | 'se' | 's' | 'sw' | 'w' | 'nw';

const ALL_HANDLES: ResizeHandle[] = ['n', 'ne', 'e', 'se', 's', 'sw', 'w', 'nw'];

// ─── component ───────────────────────────────────────────────────────────────

@Component({
  selector: 'app-header-grid-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <!--
      Two layout modes:
      • standalone (embedded=false) – rendered in the config side-panel, fixed-height canvas.
      • embedded   (embedded=true)  – rendered as an absolute overlay inside the header widget;
                                      canvas fills 100% of the host.
    -->
    <div class="hge-wrap" [class.hge-embedded]="embedded">

      <!-- ── toolbar ─────────────────────────────────────────────────────── -->
      <div class="hge-toolbar" [class.hge-toolbar-float]="embedded">
        <label class="hge-toggle">
          <input type="checkbox" [(ngModel)]="showGuides" />
          Guides
        </label>
        <span class="hge-sep">|</span>
        <label class="hge-toggle">
          Snap&nbsp;
          <select [(ngModel)]="snapStep">
            <option [ngValue]="0">Off</option>
            <option [ngValue]="1">1%</option>
            <option [ngValue]="2">2%</option>
            <option [ngValue]="5">5%</option>
          </select>
        </label>
        @if (!embedded) {
          <span class="hge-hint">Drag to move · handles to resize</span>
        }
      </div>

      <!-- ── canvas ──────────────────────────────────────────────────────── -->
      <div #canvas class="hge-canvas"
           [class.is-dragging]="isDragging"
           (mousedown)="onCanvasBgMouseDown($event)">

        <!-- smart guide lines -->
        @for (g of activeGuides; track $index) {
          <div [class]="'hge-guide hge-guide-' + g.orientation"
               [style.left.%]="g.orientation === 'v' ? g.position : undefined"
               [style.top.%]="g.orientation === 'h' ? g.position : undefined">
          </div>
        }

        <!-- elements -->
        @for (el of elements; track el.id) {
          <div class="hge-el"
               [class.hge-el-title]="el.type === 'title'"
               [class.hge-el-badge]="el.type === 'badge'"
               [class.hge-el-selected]="selectedId === el.id"
               [style.left.%]="el.x"
               [style.top.%]="el.y"
               [style.width.%]="el.w"
               [style.height.%]="el.h"
               (mousedown)="onElementMouseDown($event, el)">
            <span class="hge-el-label">{{ el.label }}</span>
            @if (selectedId === el.id) {
              @for (h of handles; track h) {
                <div class="hge-handle" [class]="'hge-handle-' + h"
                     (mousedown)="onHandleMouseDown($event, el, h)"></div>
              }
            }
          </div>
        }

        <!-- position readout – floats inside canvas when embedded -->
        @if (embedded && selectedElement) {
          <div class="hge-readout hge-readout-float">
            {{ selectedElement.label }}&nbsp;
            x:{{ f(selectedElement.x) }}%
            y:{{ f(selectedElement.y) }}%
            w:{{ f(selectedElement.w) }}%
            h:{{ f(selectedElement.h) }}%
          </div>
        }

      </div><!-- /canvas -->

      <!-- position readout – below canvas in standalone mode -->
      @if (!embedded && selectedElement) {
        <div class="hge-readout">
          {{ selectedElement.label }}&nbsp;&mdash;&nbsp;
          x:&nbsp;{{ f(selectedElement.x) }}%&nbsp;
          y:&nbsp;{{ f(selectedElement.y) }}%&nbsp;
          w:&nbsp;{{ f(selectedElement.w) }}%&nbsp;
          h:&nbsp;{{ f(selectedElement.h) }}%
        </div>
      }

    </div>
  `,
  styles: [`
    /* ── wrapper ── */
    .hge-wrap {
      display: flex; flex-direction: column; gap: 6px;
    }
    /* embedded: fill the host element completely */
    .hge-wrap.hge-embedded {
      position: absolute; inset: 0; gap: 0;
    }

    /* ── toolbar ── */
    .hge-toolbar {
      display: flex; align-items: center; gap: 8px; flex-wrap: wrap;
      font-size: 11px; line-height: 1.4; flex-shrink: 0;
    }
    .hge-toolbar select {
      font-size: 11px; padding: 1px 3px;
      border: 1px solid #ccc; border-radius: 3px;
    }
    /* floating toolbar sits on top of the canvas in embedded mode */
    .hge-toolbar-float {
      position: relative; z-index: 30;
      padding: 3px 6px;
      background: rgba(255, 255, 255, 0.82);
      backdrop-filter: blur(4px);
      border-bottom: 1px solid rgba(0,0,0,0.08);
    }
    .hge-sep { opacity: 0.35; }
    .hge-hint { margin-left: auto; opacity: 0.45; font-size: 10px; white-space: nowrap; }
    .hge-toggle { display: flex; align-items: center; gap: 4px; user-select: none; cursor: pointer; }

    /* ── canvas ── */
    .hge-canvas {
      position: relative;
      width: 100%;
      height: 140px;          /* standalone only – overridden in embedded mode */
      overflow: hidden;
      user-select: none;
      cursor: default;
      background-image: radial-gradient(circle, rgba(0,0,0,0.15) 1px, transparent 1px);
      background-size: 5% 5%;
    }
    /* in embedded mode the canvas fills the remaining space */
    .hge-embedded .hge-canvas {
      flex: 1; height: auto;
    }
    /* standalone: visible border so the canvas stands out in the config panel */
    :host:not(.hge-embedded-host) .hge-canvas {
      background-color: #f5f5f5;
      border: 1px solid #d0d0d0;
      border-radius: 4px;
    }
    .hge-canvas.is-dragging { cursor: grabbing; }

    /* ── guide lines ── */
    .hge-guide {
      position: absolute; background: #0077ff;
      pointer-events: none; z-index: 20; opacity: 0.7;
    }
    .hge-guide-v { top: 0; bottom: 0; width: 1px; transform: translateX(-50%); }
    .hge-guide-h { left: 0; right: 0; height: 1px; transform: translateY(-50%); }

    /* ── elements ── */
    .hge-el {
      position: absolute; box-sizing: border-box;
      border: 2px dashed rgba(0, 0, 0, 0.22); border-radius: 3px;
      background: rgba(255,255,255,0.55);
      display: flex; align-items: center; justify-content: center;
      overflow: hidden; cursor: grab; z-index: 2;
      transition: box-shadow 80ms;
    }
    .hge-el:hover { border-color: rgba(0, 100, 255, 0.45); }
    .hge-el-selected {
      border: 2px solid #0077ff !important;
      box-shadow: 0 0 0 2px rgba(0,119,255,0.2); z-index: 5;
    }
    .hge-el-title { background: rgba(210, 225, 255, 0.72); }
    .hge-el-badge { background: rgba(210, 255, 220, 0.72); }
    .hge-el-label {
      font-size: 10px; line-height: 1; pointer-events: none;
      overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 100%;
    }

    /* ── resize handles ── */
    .hge-handle {
      position: absolute; width: 8px; height: 8px;
      background: #0077ff; border: 1.5px solid #fff; border-radius: 2px; z-index: 10;
    }
    .hge-handle-n  { top:-4px;    left:50%;  transform:translateX(-50%); cursor:n-resize;  }
    .hge-handle-ne { top:-4px;    right:-4px;                             cursor:ne-resize; }
    .hge-handle-e  { top:50%;     right:-4px; transform:translateY(-50%); cursor:e-resize;  }
    .hge-handle-se { bottom:-4px; right:-4px;                             cursor:se-resize; }
    .hge-handle-s  { bottom:-4px; left:50%;  transform:translateX(-50%); cursor:s-resize;  }
    .hge-handle-sw { bottom:-4px; left:-4px;                             cursor:sw-resize; }
    .hge-handle-w  { top:50%;     left:-4px;  transform:translateY(-50%); cursor:w-resize;  }
    .hge-handle-nw { top:-4px;    left:-4px;                             cursor:nw-resize; }

    /* ── readout ── */
    .hge-readout { font-size: 11px; opacity: 0.6; font-variant-numeric: tabular-nums; }
    /* floating readout sits at the bottom of the canvas in embedded mode */
    .hge-readout-float {
      position: absolute; bottom: 4px; left: 6px; right: 6px; z-index: 30;
      background: rgba(255,255,255,0.80); backdrop-filter: blur(4px);
      border-radius: 3px; padding: 2px 6px;
      font-size: 10px; pointer-events: none;
    }
  `],
})
export class HeaderGridEditorComponent implements OnChanges, OnDestroy {
  @Input() headerConfig!: HeaderConfig;
  @Input() colorScheme?: ColorScheme;
  /** When true the component fills its host absolutely (used inside the header widget on the canvas). */
  @Input() embedded = false;
  @Output() configChanged = new EventEmitter<HeaderConfig>();

  @ViewChild('canvas') canvasRef!: ElementRef<HTMLDivElement>;

  elements: EditorElement[] = [];
  selectedId: string | null = null;
  activeGuides: Guide[] = [];
  handles = ALL_HANDLES;
  showGuides = true;
  snapStep: number = 2; // %

  get isDragging(): boolean { return !!(this.dragState || this.resizeState); }
  get selectedElement(): EditorElement | undefined {
    return this.elements.find(e => e.id === this.selectedId);
  }

  // ─── drag state ───────────────────────────────────────────────────────────
  private dragState: {
    elementId: string;
    startMouseX: number; startMouseY: number;
    startElemX: number; startElemY: number;
  } | null = null;

  private resizeState: {
    elementId: string;
    handle: ResizeHandle;
    startMouseX: number; startMouseY: number;
    startElem: { x: number; y: number; w: number; h: number };
  } | null = null;

  private readonly boundMouseMove = this.onDocMouseMove.bind(this);
  private readonly boundMouseUp   = this.onDocMouseUp.bind(this);

  constructor(private cdr: ChangeDetectorRef, private zone: NgZone) {}

  // ─── lifecycle ───────────────────────────────────────────────────────────
  ngOnChanges(changes: SimpleChanges): void {
    if (changes['headerConfig']) {
      this.syncFromConfig();
    }
  }

  ngOnDestroy(): void {
    document.removeEventListener('mousemove', this.boundMouseMove);
    document.removeEventListener('mouseup',   this.boundMouseUp);
  }

  // ─── element sync ────────────────────────────────────────────────────────
  private syncFromConfig(): void {
    if (!this.headerConfig) return;
    const cfg = this.headerConfig;
    const els: EditorElement[] = [];

    // Title element
    els.push({
      id: 'title', type: 'title',
      x: cfg.titleX ?? this.defaultTitleX(),
      y: cfg.titleY ?? this.defaultTitleY(),
      w: cfg.titleW ?? 42,
      h: cfg.titleH ?? 50,
      label: cfg.title || 'Title',
    });

    // Badge elements
    (cfg.badges ?? []).forEach((badge, i) => {
      els.push({
        id: `badge-${i}`, type: 'badge', badgeIndex: i,
        x: badge.x ?? this.autoX(i),
        y: badge.y ?? this.autoY(i),
        w: badge.w ?? 22,
        h: badge.h ?? 30,
        label: badge.entityId || badge.icon || `Badge ${i + 1}`,
      });
    });

    this.elements = els;
    this.cdr.markForCheck();
  }

  private defaultTitleX(): number {
    return (this.headerConfig?.iconPosition ?? 'left') === 'right' ? 0 : 58;
  }
  private defaultTitleY(): number {
    return 0;
  }
  private autoX(i: number): number { return (i % 4) * 22; }
  private autoY(i: number): number { return Math.floor(i / 4) * 30; }

  // ─── canvas background click (deselect) ──────────────────────────────────
  onCanvasBgMouseDown(event: MouseEvent): void {
    if (event.target === this.canvasRef?.nativeElement) {
      this.selectedId = null;
      this.cdr.markForCheck();
    }
  }

  // ─── element drag start ──────────────────────────────────────────────────
  onElementMouseDown(event: MouseEvent, el: EditorElement): void {
    event.preventDefault();
    event.stopPropagation();
    this.selectedId = el.id;
    this.dragState = {
      elementId: el.id,
      startMouseX: event.clientX, startMouseY: event.clientY,
      startElemX: el.x, startElemY: el.y,
    };
    this.attachDocListeners();
    this.cdr.markForCheck();
  }

  // ─── resize handle drag start ─────────────────────────────────────────────
  onHandleMouseDown(event: MouseEvent, el: EditorElement, handle: ResizeHandle): void {
    event.preventDefault();
    event.stopPropagation();
    this.selectedId = el.id;
    this.resizeState = {
      elementId: el.id, handle,
      startMouseX: event.clientX, startMouseY: event.clientY,
      startElem: { x: el.x, y: el.y, w: el.w, h: el.h },
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
    const canvas = this.canvasRef?.nativeElement;
    if (!canvas) return;

    const rect   = canvas.getBoundingClientRect();
    const state  = this.dragState ?? this.resizeState;
    if (!state)  return;

    const dxPct = ((event.clientX - state.startMouseX) / rect.width ) * 100;
    const dyPct = ((event.clientY - state.startMouseY) / rect.height) * 100;

    if (this.dragState) {
      this.applyDrag(dxPct, dyPct);
    } else if (this.resizeState) {
      this.applyResize(dxPct, dyPct);
    }

    // update view inside Angular's zone
    this.zone.run(() => this.cdr.markForCheck());
  }

  private applyDrag(dxPct: number, dyPct: number): void {
    const s  = this.dragState!;
    const el = this.elements.find(e => e.id === s.elementId);
    if (!el) return;

    let newX = s.startElemX + dxPct;
    let newY = s.startElemY + dyPct;

    // smart guides first (overrides raw position)
    const guides: Guide[] = [];
    if (this.showGuides) {
      const others = this.elements.filter(e => e.id !== el.id);
      const snapped = this.computeSmartGuides(newX, newY, el.w, el.h, others, guides);
      newX = snapped.x;
      newY = snapped.y;
    }
    this.activeGuides = guides;

    // snap to grid
    if (this.snapStep > 0) {
      newX = Math.round(newX / this.snapStep) * this.snapStep;
      newY = Math.round(newY / this.snapStep) * this.snapStep;
    }

    // clamp inside canvas
    el.x = Math.max(0, Math.min(100 - el.w, newX));
    el.y = Math.max(0, Math.min(100 - el.h, newY));
  }

  private applyResize(dxPct: number, dyPct: number): void {
    const s  = this.resizeState!;
    const el = this.elements.find(e => e.id === s.elementId);
    if (!el) return;

    const h = s.handle;
    let { x, y, w, h: ht } = s.startElem;
    const MIN = Math.max(this.snapStep, 4);

    if (h.includes('e')) w  = Math.max(MIN, s.startElem.w  + dxPct);
    if (h.includes('s')) ht = Math.max(MIN, s.startElem.h  + dyPct);
    if (h.includes('w')) { w  = Math.max(MIN, s.startElem.w  - dxPct); x = s.startElem.x + s.startElem.w  - w; }
    if (h.includes('n')) { ht = Math.max(MIN, s.startElem.h  - dyPct); y = s.startElem.y + s.startElem.h - ht; }

    // snap
    if (this.snapStep > 0) {
      x  = Math.round(x  / this.snapStep) * this.snapStep;
      y  = Math.round(y  / this.snapStep) * this.snapStep;
      w  = Math.round(w  / this.snapStep) * this.snapStep || this.snapStep;
      ht = Math.round(ht / this.snapStep) * this.snapStep || this.snapStep;
    }

    // clamp
    x  = Math.max(0, x);
    y  = Math.max(0, y);
    w  = Math.min(100 - x, w);
    ht = Math.min(100 - y, ht);

    el.x = x; el.y = y; el.w = w; el.h = ht;
    this.activeGuides = [];
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

  // ─── smart guides ────────────────────────────────────────────────────────
  /**
   * Compare the 3 key edges (leading, center, trailing) of the dragged element
   * against all edges of every other element. If within threshold, snap and
   * emit a guide line.  Returns new {x, y} after all snapping.
   */
  private computeSmartGuides(
    x: number, y: number, w: number, h: number,
    others: EditorElement[],
    guidesOut: Guide[],
  ): { x: number; y: number } {
    // Threshold = snap step or at least 2%
    const thr = Math.max(this.snapStep, 2);
    let outX = x, outY = y;
    const seenV = new Set<number>(), seenH = new Set<number>();

    for (const other of others) {
      // vertical guide candidates (x-axis)
      const dragXEdges  = [x, x + w / 2, x + w];
      const otherXEdges = [other.x, other.x + other.w / 2, other.x + other.w];
      for (let di = 0; di < dragXEdges.length; di++) {
        for (const oe of otherXEdges) {
          if (Math.abs(dragXEdges[di] - oe) < thr) {
            outX = x + (oe - dragXEdges[di]);
            if (!seenV.has(oe)) { guidesOut.push({ orientation: 'v', position: oe }); seenV.add(oe); }
          }
        }
      }
      // horizontal guide candidates (y-axis)
      const dragYEdges  = [y, y + h / 2, y + h];
      const otherYEdges = [other.y, other.y + other.h / 2, other.y + other.h];
      for (let di = 0; di < dragYEdges.length; di++) {
        for (const oe of otherYEdges) {
          if (Math.abs(dragYEdges[di] - oe) < thr) {
            outY = y + (oe - dragYEdges[di]);
            if (!seenH.has(oe)) { guidesOut.push({ orientation: 'h', position: oe }); seenH.add(oe); }
          }
        }
      }
    }
    return { x: outX, y: outY };
  }

  // ─── emit updated config ─────────────────────────────────────────────────
  private emitConfig(): void {
    const round1 = (n: number) => Math.round(n * 10) / 10;
    const updated: HeaderConfig = { ...this.headerConfig };

    const titleEl = this.elements.find(e => e.id === 'title');
    if (titleEl) {
      updated.titleX = round1(titleEl.x);
      updated.titleY = round1(titleEl.y);
      updated.titleW = round1(titleEl.w);
      updated.titleH = round1(titleEl.h);
    }

    if (updated.badges) {
      updated.badges = updated.badges.map((badge, i) => {
        const badgeEl = this.elements.find(e => e.id === `badge-${i}`);
        if (!badgeEl) return badge;
        return { ...badge, x: round1(badgeEl.x), y: round1(badgeEl.y), w: round1(badgeEl.w), h: round1(badgeEl.h) };
      });
    }

    this.configChanged.emit(updated);
  }

  // ─── helpers ─────────────────────────────────────────────────────────────
  f(n: number): string { return n.toFixed(1); }
}
