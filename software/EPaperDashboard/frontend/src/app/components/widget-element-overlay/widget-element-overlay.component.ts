import {
  ChangeDetectorRef,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  EditableWidgetElementGeometry,
  RenderRectangle,
  WidgetRenderGeometry,
} from '../../services/dashboard-render-preview.service';
import { EditableElementChange } from '../../models/widget-element-layout';

type ResizeHandle = 'n' | 's' | 'e' | 'w' | 'ne' | 'nw' | 'se' | 'sw';
type Guide = { orientation: 'horizontal' | 'vertical'; position: number };

interface PointerInteraction {
  type: 'drag' | 'resize';
  element: EditableWidgetElementGeometry;
  handle?: ResizeHandle;
  pointerId: number;
  startClientX: number;
  startClientY: number;
  startPosition: RenderRectangle;
}

@Component({
  selector: 'app-widget-element-overlay',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './widget-element-overlay.component.html',
  styleUrls: ['./widget-element-overlay.component.scss'],
})
export class WidgetElementOverlayComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) geometry!: WidgetRenderGeometry;
  @Input() snapStep = 2;
  @Input() showGuides = true;
  @Output() elementChange = new EventEmitter<EditableElementChange>();

  readonly handles: readonly ResizeHandle[] = ['n', 's', 'e', 'w', 'nw', 'ne', 'sw', 'se'];
  selectedId: string | null = null;
  guides: Guide[] = [];

  private positions = new Map<string, RenderRectangle>();
  private interaction: PointerInteraction | null = null;
  private readonly pointerMove = (event: PointerEvent) => this.onPointerMove(event);
  private readonly pointerUp = (event: PointerEvent) => this.onPointerUp(event);

  constructor(
    private readonly host: ElementRef<HTMLElement>,
    private readonly cdr: ChangeDetectorRef,
  ) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['geometry'] && !this.interaction) {
      this.positions = new Map(
        (this.geometry?.elements ?? []).map(element => [element.id, { ...element.position }]),
      );
      if (this.selectedId && !this.positions.has(this.selectedId)) this.selectedId = null;
    }
  }

  ngOnDestroy(): void {
    this.detachListeners();
  }

  elementStyle(element: EditableWidgetElementGeometry): Record<string, string> {
    const position = this.getPosition(element);
    const content = this.geometry.contentBounds;
    const widget = this.geometry.bounds;
    return {
      left: `${content.x - widget.x + position.x / 100 * content.width}px`,
      top: `${content.y - widget.y + position.y / 100 * content.height}px`,
      width: `${position.width / 100 * content.width}px`,
      height: `${position.height / 100 * content.height}px`,
    };
  }

  guideStyle(guide: Guide): Record<string, string> {
    const content = this.geometry.contentBounds;
    const widget = this.geometry.bounds;
    if (guide.orientation === 'vertical') {
      return {
        left: `${content.x - widget.x + guide.position / 100 * content.width}px`,
        top: `${content.y - widget.y}px`,
        height: `${content.height}px`,
      };
    }
    return {
      left: `${content.x - widget.x}px`,
      top: `${content.y - widget.y + guide.position / 100 * content.height}px`,
      width: `${content.width}px`,
    };
  }

  onElementPointerDown(event: PointerEvent, element: EditableWidgetElementGeometry): void {
    if (event.button !== 0 || !element.movable) return;
    event.preventDefault();
    event.stopPropagation();
    this.selectedId = element.id;
    this.beginInteraction(event, element, 'drag');
  }

  onHandlePointerDown(
    event: PointerEvent,
    element: EditableWidgetElementGeometry,
    handle: ResizeHandle,
  ): void {
    if (!element.resizable) return;
    event.preventDefault();
    event.stopPropagation();
    this.selectedId = element.id;
    this.beginInteraction(event, element, 'resize', handle);
  }

  private beginInteraction(
    event: PointerEvent,
    element: EditableWidgetElementGeometry,
    type: 'drag' | 'resize',
    handle?: ResizeHandle,
  ): void {
    this.interaction = {
      type,
      element,
      handle,
      pointerId: event.pointerId,
      startClientX: event.clientX,
      startClientY: event.clientY,
      startPosition: { ...this.getPosition(element) },
    };
    document.addEventListener('pointermove', this.pointerMove);
    document.addEventListener('pointerup', this.pointerUp);
    document.addEventListener('pointercancel', this.pointerUp);
    this.cdr.markForCheck();
  }

  private onPointerMove(event: PointerEvent): void {
    const interaction = this.interaction;
    if (!interaction || interaction.pointerId !== event.pointerId) return;
    event.preventDefault();

    const hostRect = this.host.nativeElement.getBoundingClientRect();
    const contentBounds = this.geometry.contentBounds;
    if (hostRect.width <= 0 || hostRect.height <= 0 ||
        contentBounds.width <= 0 || contentBounds.height <= 0) return;
    const scaleX = this.geometry.bounds.width / hostRect.width;
    const scaleY = this.geometry.bounds.height / hostRect.height;
    const dx = (event.clientX - interaction.startClientX) * scaleX
      / contentBounds.width * 100;
    const dy = (event.clientY - interaction.startClientY) * scaleY
      / contentBounds.height * 100;
    const start = interaction.startPosition;

    const next = interaction.type === 'drag'
      ? this.movePosition(interaction.element.id, start, dx, dy)
      : this.resizePosition(start, dx, dy, interaction.handle!);
    this.positions.set(interaction.element.id, next);
    this.cdr.detectChanges();
  }

  private onPointerUp(event: PointerEvent): void {
    const interaction = this.interaction;
    if (!interaction || interaction.pointerId !== event.pointerId) return;
    const position = { ...this.getPosition(interaction.element) };
    this.interaction = null;
    this.guides = [];
    this.detachListeners();
    if (this.hasPositionChanged(interaction.startPosition, position)) {
      this.elementChange.emit({ element: interaction.element, position });
    }
    this.cdr.markForCheck();
  }

  private movePosition(
    id: string,
    start: RenderRectangle,
    dx: number,
    dy: number,
  ): RenderRectangle {
    let x = start.x + dx;
    let y = start.y + dy;
    this.guides = [];

    if (this.showGuides) {
      const snapped = this.snapToElements(id, x, y, start.width, start.height);
      x = snapped.x;
      y = snapped.y;
    }
    x = this.snap(x);
    y = this.snap(y);

    return {
      ...start,
      x: Math.max(0, Math.min(100 - start.width, x)),
      y: Math.max(0, Math.min(100 - start.height, y)),
    };
  }

  private resizePosition(
    start: RenderRectangle,
    dx: number,
    dy: number,
    handle: ResizeHandle,
  ): RenderRectangle {
    const minimum = Math.max(this.snapStep, 4);
    let { x, y, width, height } = start;
    if (handle.includes('e')) width = Math.max(minimum, start.width + dx);
    if (handle.includes('s')) height = Math.max(minimum, start.height + dy);
    if (handle.includes('w')) {
      width = Math.max(minimum, start.width - dx);
      x = start.x + start.width - width;
    }
    if (handle.includes('n')) {
      height = Math.max(minimum, start.height - dy);
      y = start.y + start.height - height;
    }

    x = Math.max(0, this.snap(x));
    y = Math.max(0, this.snap(y));
    width = Math.min(100 - x, Math.max(minimum, this.snap(width)));
    height = Math.min(100 - y, Math.max(minimum, this.snap(height)));
    this.guides = [];
    return { x, y, width, height };
  }

  private snapToElements(
    id: string,
    x: number,
    y: number,
    width: number,
    height: number,
  ): { x: number; y: number } {
    const threshold = Math.max(this.snapStep, 2);
    let resultX = x;
    let resultY = y;
    const vertical = new Set<number>();
    const horizontal = new Set<number>();

    for (const element of this.geometry.elements) {
      if (element.id === id) continue;
      const other = this.getPosition(element);
      const movingX = [x, x + width / 2, x + width];
      const otherX = [other.x, other.x + other.width / 2, other.x + other.width];
      for (const source of movingX) {
        for (const target of otherX) {
          if (Math.abs(source - target) < threshold) {
            resultX = x + target - source;
            vertical.add(target);
          }
        }
      }
      const movingY = [y, y + height / 2, y + height];
      const otherY = [other.y, other.y + other.height / 2, other.y + other.height];
      for (const source of movingY) {
        for (const target of otherY) {
          if (Math.abs(source - target) < threshold) {
            resultY = y + target - source;
            horizontal.add(target);
          }
        }
      }
    }

    this.guides = [
      ...Array.from(vertical, position => ({ orientation: 'vertical' as const, position })),
      ...Array.from(horizontal, position => ({ orientation: 'horizontal' as const, position })),
    ];
    return { x: resultX, y: resultY };
  }

  private snap(value: number): number {
    return this.snapStep > 0 ? Math.round(value / this.snapStep) * this.snapStep : value;
  }

  private getPosition(element: EditableWidgetElementGeometry): RenderRectangle {
    return this.positions.get(element.id) ?? element.position;
  }

  private hasPositionChanged(first: RenderRectangle, second: RenderRectangle): boolean {
    return first.x !== second.x
      || first.y !== second.y
      || first.width !== second.width
      || first.height !== second.height;
  }

  private detachListeners(): void {
    document.removeEventListener('pointermove', this.pointerMove);
    document.removeEventListener('pointerup', this.pointerUp);
    document.removeEventListener('pointercancel', this.pointerUp);
  }
}
