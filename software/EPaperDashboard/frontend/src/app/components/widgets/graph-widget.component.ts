import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, HassEntityState, GraphConfig, DashboardLayout } from '../../models/types';

@Component({
  selector: 'app-widget-graph',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./graph-widget.component.scss'],
  template: `
    <div class="graph-widget" [style.color]="getTextColor()">
      @if (!getEntityState(config.entityId)) {
        <div class="empty-state">
          <i class="fa fa-chart-line" [style.color]="getIconColor()"></i>
          <p>Not configured</p>
        </div>
      }
      @if (getEntityState(config.entityId)) {
        <div class="graph-content">
          <div class="graph-label" [style.color]="getTitleColor()">{{ config.label || getEntityState(config.entityId)!.entityId }}</div>
          <div class="graph-value">{{ getEntityState(config.entityId)!.state }}</div>
          <div class="graph-unit">
            @if (getEntityState(config.entityId)!.attributes?.['unit_of_measurement']) {
              {{ getEntityState(config.entityId)!.attributes?.['unit_of_measurement'] }}
            }
          </div>
        </div>
      }
    </div>
  `
})
export class GraphWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() entityStates: Record<string, HassEntityState> | null = null;
  @Input() designerSettings?: DashboardLayout;

  get config(): GraphConfig { return (this.widget?.config || {}) as GraphConfig; }

  getEntityState(entityId?: string) {
    if (!entityId || !this.entityStates) return null;
    return this.entityStates[entityId] ?? null;
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
}
