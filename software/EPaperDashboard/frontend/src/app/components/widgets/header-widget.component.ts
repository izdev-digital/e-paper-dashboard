import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, HeaderConfig, ColorScheme, HassEntityState, DashboardLayout } from '../../models/types';

@Component({
  selector: 'app-widget-header',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./header-widget.component.scss'],
  template: `
    <div class="header-widget" [class]="'align-' + (asHeaderConfig(widget.config).titleAlign || 'top-left')" [style.color]="getTitleColor()">
      <div class="title-section" [class.order-last]="!isIconOnLeft()">
        @if (isIconOnLeft()) {
          <img class="header-icon"
            [src]="asHeaderConfig(widget.config).iconUrl || '/icon.svg'"
            [style.width.px]="asHeaderConfig(widget.config).iconSize ?? 32"
            [style.height.px]="asHeaderConfig(widget.config).iconSize ?? 32"
            [style.color]="getIconColor()"
            alt="App Icon"/>
        }
        <div class="title" [style.fontSize.px]="getTitleFontSize()" [style.color]="getTitleColor()">{{ asHeaderConfig(widget.config).title }}</div>
        @if (!isIconOnLeft()) {
          <img class="header-icon"
            [src]="asHeaderConfig(widget.config).iconUrl || '/icon.svg'"
            [style.width.px]="asHeaderConfig(widget.config).iconSize ?? 32"
            [style.height.px]="asHeaderConfig(widget.config).iconSize ?? 32"
            [style.color]="getIconColor()"
            alt="App Icon"/>
        }
      </div>
      @if (visibleBadges().length) {
        <div class="badges-container">
          @for (badge of visibleBadges(); track $index) {
            <span class="badge" 
                  [style.fontSize.px]="getTextFontSize()"
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
export class HeaderWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() entityStates: Record<string, HassEntityState> | null = null;
  @Input() designerSettings?: DashboardLayout;

  asHeaderConfig(config: any): HeaderConfig { return config as HeaderConfig; }

  getTitleFontSize(): number {
    return this.designerSettings?.titleFontSize ?? 16;
  }

  getTextFontSize(): number {
    return this.designerSettings?.textFontSize ?? 14;
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
