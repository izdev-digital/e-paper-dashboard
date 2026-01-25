import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, HeaderConfig, ColorScheme, HassEntityState } from '../../models/types';

@Component({
  selector: 'app-widget-header',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="header-widget">
      <h3 class="header-title">{{ asHeaderConfig(widget.config).title }}</h3>
      <div class="badges" *ngIf="asHeaderConfig(widget.config).badges?.length">
        <span class="badge" *ngFor="let badge of asHeaderConfig(widget.config).badges; let i = index">
          <ng-container *ngIf="badge.entityId && getEntityState(badge.entityId)">
            {{ getEntityState(badge.entityId)?.state }}
            <span class="badge-value" *ngIf="getEntityState(badge.entityId)?.attributes?.['unit_of_measurement']">
              {{ getEntityState(badge.entityId)?.attributes?.['unit_of_measurement'] }}
            </span>
          </ng-container>
          <ng-container *ngIf="!badge.entityId || !getEntityState(badge.entityId)">
            {{ badge.label }}
          </ng-container>
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

  getEntityState(entityId?: string) {
    if (!entityId || !this.entityStates) return null;
    return this.entityStates[entityId] ?? null;
  }
}
