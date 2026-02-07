import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { WidgetConfig, HeaderConfig, ColorScheme, HassEntityState, DashboardLayout } from '../../models/types';

@Component({
  selector: 'app-widget-header',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./header-widget.component.scss'],
  template: `
    <div class="header-widget" [class]="'align-' + (asHeaderConfig(widget.config).titleAlign || 'top-left')" [style.color]="getTitleColor()">
      <div class="title-section" [class.order-last]="!isIconOnLeft()">
        @if (isIconOnLeft() && inlineSvg) {
          <div class="header-icon"
            [innerHTML]="inlineSvg"
            [style.width.px]="asHeaderConfig(widget.config).iconSize ?? 32"
            [style.height.px]="asHeaderConfig(widget.config).iconSize ?? 32"
            [style.--accent-color]="getIconColor()"></div>
        }
        <div class="title" [style.fontSize.px]="getTitleFontSize()" [style.fontWeight]="getTitleFontWeight()" [style.color]="getTitleColor()">{{ asHeaderConfig(widget.config).title }}</div>
        @if (!isIconOnLeft() && inlineSvg) {
          <div class="header-icon"
            [innerHTML]="inlineSvg"
            [style.width.px]="asHeaderConfig(widget.config).iconSize ?? 32"
            [style.height.px]="asHeaderConfig(widget.config).iconSize ?? 32"
            [style.--accent-color]="getIconColor()"></div>
        }
      </div>
      @if (visibleBadges().length) {
        <div class="badges-container">
          @for (badge of visibleBadges(); track $index) {
            <span class="badge" 
                  [style.fontSize.px]="getTextFontSize()"
                  [style.fontWeight]="getTextFontWeight()"
                  [style.color]="getTextColor()">
              @if (badge.icon) {
                <i class="fa {{ badge.icon }}" [style.color]="getIconColor()"></i>
              }
              @if (badge.entityId) {
                {{ getEntityState(badge.entityId)?.state || '' }}
                @if (getEntityAttribute(badge.entityId, 'unit_of_measurement')) {
                  {{ getEntityAttribute(badge.entityId, 'unit_of_measurement') }}
                }
              }
            </span>
          }
        </div>
      }
    </div>
  `
})
export class HeaderWidgetComponent implements OnInit, OnChanges {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() entityStates: Record<string, HassEntityState> | null = null;
  @Input() designerSettings?: DashboardLayout;

  inlineSvg: SafeHtml | null = null;

  constructor(private sanitizer: DomSanitizer) { }

  ngOnInit(): void {
    this.loadSvg();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['widget'] || changes['colorScheme']) {
      this.loadSvg();
    }
  }

  private async loadSvg(): Promise<void> {
    try {
      const response = await fetch('/icon-tab-dynamic.svg');
      if (response.ok) {
        let svgText = await response.text();
        // Replace the default accent color in the SVG to prevent flicker
        const accentColor = this.getIconColor();
        svgText = svgText.replace(/--accent-color: #[0-9a-f]{6};/i, `--accent-color: ${accentColor};`);
        this.inlineSvg = this.sanitizer.bypassSecurityTrustHtml(svgText);
      }
    } catch (error) {
      // Silently fail
    }
  }

  asHeaderConfig(config: any): HeaderConfig { return config as HeaderConfig; }

  getTitleFontSize(): number {
    return this.designerSettings?.titleFontSize ?? 16;
  }

  getTextFontSize(): number {
    return this.designerSettings?.textFontSize ?? 14;
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

  isIconOnLeft(): boolean {
    const align = this.asHeaderConfig(this.widget.config).titleAlign || 'top-left';
    return align === 'top-left' || align === 'bottom-left';
  }

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
    // Show badges that have an icon or entityId set
    return cfg.badges.filter((b: any) => {
      if (!b) return false;
      if ((b.entityId && String(b.entityId).trim().length > 0) ||
        (b.icon && String(b.icon).trim().length > 0)) return true;
      return false;
    });
  }
}
