import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, HeaderConfig, ColorScheme, HassEntityState } from '../../models/types';

@Component({
  selector: 'app-widget-header',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./header-widget.component.scss'],
  template: `
    <div class="header-widget" [class]="'align-' + (asHeaderConfig(widget.config).titleAlign || 'top-left')">
      <div class="title-section" [class.order-last]="!isIconOnLeft()">
        @if (isIconOnLeft()) {
          <img class="header-icon"
            [src]="asHeaderConfig(widget.config).iconUrl || '/icon.svg'"
            [style.width.px]="asHeaderConfig(widget.config).iconSize ?? 32"
            [style.height.px]="asHeaderConfig(widget.config).iconSize ?? 32"
            alt="App Icon"/>
        }
        <div class="title" [style.fontSize.px]="asHeaderConfig(widget.config).fontSize ?? 16">{{ asHeaderConfig(widget.config).title }}</div>
        @if (!isIconOnLeft()) {
          <img class="header-icon"
            [src]="asHeaderConfig(widget.config).iconUrl || '/icon.svg'"
            [style.width.px]="asHeaderConfig(widget.config).iconSize ?? 32"
            [style.height.px]="asHeaderConfig(widget.config).iconSize ?? 32"
            alt="App Icon"/>
        }
      </div>
      @if (visibleBadges().length) {
        <div class="badges-container">
          @for (badge of visibleBadges(); track $index) {
            <span class="badge" 
                  [style.fontSize.px]="asHeaderConfig(widget.config).fontSize ?? 16"
                  [style.color]="colorScheme.text">
              @if (badge.icon) {
                <i class="fa {{ badge.icon }}"></i>
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

  asHeaderConfig(config: any): HeaderConfig { return config as HeaderConfig; }

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
