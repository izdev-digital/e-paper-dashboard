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
        <img *ngIf="isIconOnLeft()" class="header-icon"
          [src]="asHeaderConfig(widget.config).iconUrl || '/icon.svg'"
          [style.width.px]="asHeaderConfig(widget.config).iconSize ?? 32"
          [style.height.px]="asHeaderConfig(widget.config).iconSize ?? 32"
          alt="App Icon"/>
        <div class="title" [style.fontSize.px]="asHeaderConfig(widget.config).fontSize ?? 16">{{ asHeaderConfig(widget.config).title }}</div>
        <img *ngIf="!isIconOnLeft()" class="header-icon"
          [src]="asHeaderConfig(widget.config).iconUrl || '/icon.svg'"
          [style.width.px]="asHeaderConfig(widget.config).iconSize ?? 32"
          [style.height.px]="asHeaderConfig(widget.config).iconSize ?? 32"
          alt="App Icon"/>
      </div>
      <div class="badges-container" *ngIf="visibleBadges().length">
        <span class="badge" *ngFor="let badge of visibleBadges()">
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
    // Show badges that are confirmed OR have a label/entityId set (helpful for newly added badges)
    return cfg.badges.filter((b: any) => {
      if (!b) return false;
      if (b._confirmed === true) return true;
      if ((b.label && String(b.label).trim().length > 0) || (b.entityId && String(b.entityId).trim().length > 0)) return true;
      return false;
    });
  }
}
