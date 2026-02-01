import { Component, Input, AfterViewInit, OnChanges, SimpleChanges, ViewChild, ElementRef, ViewChildren, QueryList, HostListener, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, HeaderConfig, ColorScheme, HassEntityState } from '../../models/types';

@Component({
  selector: 'app-widget-header',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./header-widget.component.scss'],
  template: `
    <div class="header-widget">
      <div class="title-section">
        <img class="header-icon"
          [src]="asHeaderConfig(widget.config).iconUrl || '/icon.svg'"
          [style.width.px]="asHeaderConfig(widget.config).iconSize ?? 32"
          [style.height.px]="asHeaderConfig(widget.config).iconSize ?? 32"
          alt="App Icon"/>
        <div class="title" [style.fontSize.px]="asHeaderConfig(widget.config).fontSize ?? 16">{{ asHeaderConfig(widget.config).title }}</div>
      </div>
      <div class="badges-section" #badgesContainer *ngIf="visibleBadges().length">
        <span class="badge" #badgeElem *ngFor="let badge of visibleBadges(); let i = index"
          [style.display]="i < visibleCount ? 'inline-flex' : 'none'">
          {{ badge.entityId ? (getEntityState(badge.entityId)?.state || badge.label) : badge.label }}
          <span class="badge-value" *ngIf="getEntityAttribute(badge.entityId, 'unit_of_measurement')">
            {{ getEntityAttribute(badge.entityId, 'unit_of_measurement') }}
          </span>
        </span>
      </div>
    </div>
  `
})
export class HeaderWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() entityStates: Record<string, HassEntityState> | null = null;
  @ViewChild('badgesContainer', { static: false }) badgesContainer?: ElementRef<HTMLElement>;
  @ViewChildren('badgeElem') badgeElems?: QueryList<ElementRef<HTMLElement>>;

  visibleCount = 0;

  constructor(private cdr: ChangeDetectorRef) {}

  asHeaderConfig(config: any): HeaderConfig { return config as HeaderConfig; }

  getEntityState(entityId?: string) {
    if (!entityId || !this.entityStates) return null;
    return this.entityStates[entityId] ?? null;
  }

  getEntityAttribute(entityId?: string, attr?: string) {
    const st = this.getEntityState(entityId);
    if (!st || !st.attributes || !attr) return null;
    return st.attributes[attr] ?? null;
  }

  visibleBadges() {
    const cfg = this.asHeaderConfig(this.widget.config);
    if (!cfg || !cfg.badges) return [];
    // Show badges that are confirmed OR have a label/entityId set (helpful for newly added badges)
    return cfg.badges.filter((b: any) => {
      if (!b) return false;
      if (b._confirmed === true) return true;
      if ((b.label && String(b.label).trim().length > 0) || (b.entityId && String(b.entityId).trim().length > 0)) return true;
      return false;
    });
  }

  ngAfterViewInit(): void {
    // compute once DOM is ready
    setTimeout(() => this.updateVisibleCount(), 0);
    this.badgeElems?.changes.subscribe(() => {
      setTimeout(() => this.updateVisibleCount(), 0);
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['widget']) {
      // recalc when widget changes
      setTimeout(() => this.updateVisibleCount(), 0);
    }
  }

  @HostListener('window:resize')
  onResize() {
    // recalc on resize
    setTimeout(() => this.updateVisibleCount(), 50);
  }

  private updateVisibleCount() {
    const cfg = this.asHeaderConfig(this.widget.config);
    if (!cfg || !cfg.badges || cfg.badges.length === 0 || !this.badgesContainer) {
      this.visibleCount = 0;
      this.cdr.markForCheck();
      return;
    }
    const container = this.badgesContainer.nativeElement;
    const containerWidth = container.clientWidth;
    
    // If no badge elements yet, wait for next render
    if (!this.badgeElems || this.badgeElems.length === 0) {
      this.visibleCount = 0;
      this.cdr.markForCheck();
      return;
    }

    // Calculate how many badges can fit
    let used = 0;
    let count = 0;
    const gap = 8; // gap between badges
    const elems = this.badgeElems.toArray();
    
    for (let i = 0; i < elems.length; i++) {
      const el = elems[i].nativeElement;
      // Temporarily show to measure
      el.style.display = 'inline-flex';
      const w = el.getBoundingClientRect().width;
      const nextUsed = used === 0 ? w : used + gap + w;
      
      if (nextUsed <= containerWidth) {
        used = nextUsed;
        count++;
      } else {
        // Hide if it doesn't fit
        el.style.display = 'none';
        break;
      }
    }
    
    this.visibleCount = count;
    this.cdr.markForCheck();
  }
}
