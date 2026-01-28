import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, HassEntityState, WeatherConfig } from '../../models/types';

@Component({
  selector: 'app-widget-weather',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./weather-widget.component.scss'],
  template: `
    <div class="weather-widget">
      <ng-container *ngIf="!getEntityState(config.entityId)">
        <div class="empty-state">
          <i class="fa fa-cloud-sun"></i>
          <p>Not configured</p>
        </div>
      </ng-container>
      <ng-container *ngIf="getEntityState(config.entityId)">
        <div class="weather-content">
          <div class="weather-condition">{{ getEntityState(config.entityId)!.state }}</div>
          <ng-container *ngIf="getEntityState(config.entityId)!.attributes?.['temperature']">
            <div class="weather-temp">{{ getEntityState(config.entityId)!.attributes?.['temperature'] }}°</div>
          </ng-container>
          <ng-container *ngIf="getEntityState(config.entityId)!.attributes?.['humidity']">
            <div class="weather-humidity"><i class="fa fa-droplet"></i> {{ getEntityState(config.entityId)!.attributes?.['humidity'] }}%</div>
          </ng-container>
          <ng-container *ngIf="getEntityState(config.entityId)!.attributes?.['wind_speed']">
            <div class="weather-wind"><i class="fa fa-wind"></i> {{ getEntityState(config.entityId)!.attributes?.['wind_speed'] }}</div>
          </ng-container>
        </div>
      </ng-container>
    </div>
  `
})
export class WeatherWidgetComponent {
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
}
