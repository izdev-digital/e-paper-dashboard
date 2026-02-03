import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, HassEntityState, WeatherConfig } from '../../models/types';

@Component({
  selector: 'app-widget-weather',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./weather-widget.component.scss'],
  template: `
    <div class="weather-widget" [style.color]="getTextColor()">
      @if (!getEntityState(config.entityId)) {
        <div class="empty-state">
          <i class="fa fa-cloud-sun" [style.color]="getIconColor()"></i>
          <p>Not configured</p>
        </div>
      }
      @if (getEntityState(config.entityId)) {
        <div class="weather-content">
          <div class="weather-condition">{{ getEntityState(config.entityId)!.state }}</div>
          @if (getEntityState(config.entityId)!.attributes?.['temperature']) {
            <div class="weather-temp">{{ getEntityState(config.entityId)!.attributes?.['temperature'] }}°</div>
          }
          @if (getEntityState(config.entityId)!.attributes?.['humidity']) {
            <div class="weather-humidity"><i class="fa fa-droplet" [style.color]="getIconColor()"></i> {{ getEntityState(config.entityId)!.attributes?.['humidity'] }}%</div>
          }
          @if (getEntityState(config.entityId)!.attributes?.['wind_speed']) {
            <div class="weather-wind"><i class="fa fa-wind" [style.color]="getIconColor()"></i> {{ getEntityState(config.entityId)!.attributes?.['wind_speed'] }}</div>
          }
        </div>
      }
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
