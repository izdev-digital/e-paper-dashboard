import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, HassEntityState, GraphConfig, DashboardLayout } from '../../models/types';

@Component({
  selector: 'app-widget-graph',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./graph-widget.component.scss'],
  template: `
    <div class="graph-widget">
      @if (!getEntityState(config.entityId)) {
        <div class="empty-state">
          <i class="fa fa-chart-line"></i>
          <p>Not configured</p>
        </div>
      }
      @if (getEntityState(config.entityId)) {
        <div class="graph-content">
          <div class="graph-label">{{ config.label || getEntityState(config.entityId)!.entityId }}</div>
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
}
