import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, HassEntityState, WeatherConfig } from '../../models/types';

@Component({
  selector: 'app-widget-weather-forecast',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./weather-forecast-widget.component.scss'],
  template: `
    <div class="weather-forecast-widget">
      <ng-container *ngIf="!getEntityState(config.entityId)">
        <div class="empty-state">
          <i class="fa fa-cloud-sun-rain"></i>
          <p>Not configured</p>
        </div>
      </ng-container>
      <ng-container *ngIf="getEntityState(config.entityId)">
        <div class="forecast-content">
          <div class="forecast-header">{{ getEntityState(config.entityId)!.state }}</div>
          <ng-container *ngIf="getEntityState(config.entityId)!.attributes?.['forecast']">
            <div class="forecast-items">
              <ng-container *ngFor="let item of getForecastItems(config.entityId); trackBy: trackByItemId">
                <div class="forecast-item">
                  <small>{{ getItemDate(item) }}</small>
                  <div>{{ getItemCondition(item) }}</div>
                  <small>{{ getItemTemp(item) }}°</small>
                </div>
              </ng-container>
            </div>
          </ng-container>
        </div>
      </ng-container>
    </div>
  `
})
export class WeatherForecastWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() entityStates: Record<string, HassEntityState> | null = null;

  get config(): WeatherConfig {
    return (this.widget?.config || {}) as WeatherConfig;
  }

  getEntityState(entityId?: string) {
    if (!entityId || !this.entityStates) return null;
    return this.entityStates[entityId] ?? null;
  }

  getForecastItems(entityId?: string) {
    const state = this.getEntityState(entityId);
    if (!state?.attributes?.['forecast']) return [];
    const forecast = state.attributes['forecast'] as any[];
    return forecast.slice(0, 2).map((item, idx) => ({ ...item, id: idx }));
  }

  getItemDate(item: any) {
    if (!item.datetime) return '';
    const dt = item.datetime as string;
    return dt.substring(5, 10);
  }

  getItemCondition(item: any) {
    return item.condition || '';
  }

  getItemTemp(item: any) {
    return item.temperature || '';
  }
  trackByItemId(index: number, item: any) { return item.id || index; }
}
