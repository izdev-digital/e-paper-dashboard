import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, HassEntityState, WeatherConfig, DashboardLayout } from '../../models/types';

@Component({
  selector: 'app-widget-weather',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./weather-widget.component.scss'],
  template: `
    <div 
      class="weather-widget"
      [class.compact]="isCompactMode()"
      [class.horizontal]="isHorizontalLayout()"
      [class.vertical-compact]="isVerticalCompactMode()"
      [style.--titleFontSize]="getTitleFontSize() + 'px'"
      [style.--textFontSize]="getTextFontSize() + 'px'"
      [style.--titleColor]="getTitleColor()"
      [style.--textColor]="getTextColor()"
      [style.--iconColor]="getIconColor()"
      [style.color]="getTextColor()">
      @if (!isDataFetched()) {
        <div class="preview-state">
          <i class="fa fa-cloud-sun"></i>
          <p>Weather</p>
        </div>
      }
      @if (getEntityState(config.entityId) && isDataFetched()) {
        @if (isMinimalMode()) {
          <!-- Minimal mode for 1x1 widget - only show temperature -->
          <div class="weather-content minimal">
            @if (getEntityState(config.entityId)!.attributes?.['temperature']) {
              <div class="weather-temp">{{ getEntityState(config.entityId)!.attributes?.['temperature'] }}°</div>
            } @else {
              <div class="weather-condition">{{ getEntityState(config.entityId)!.state }}</div>
            }
          </div>
        } @else if (isHorizontalLayout()) {
          <!-- Horizontal layout for small height, large width -->
          <div class="weather-content horizontal">
            <div class="weather-condition">{{ getEntityState(config.entityId)!.state }}</div>
            @if (getEntityState(config.entityId)!.attributes?.['temperature']) {
              <div class="weather-temp">{{ getEntityState(config.entityId)!.attributes?.['temperature'] }}°</div>
            }
            @if (getEntityState(config.entityId)!.attributes?.['humidity'] || getEntityState(config.entityId)!.attributes?.['wind_speed']) {
              <div class="weather-attributes-horizontal">
                @if (getEntityState(config.entityId)!.attributes?.['humidity']) {
                  <div class="weather-attribute-horizontal" title="Humidity">
                    <i class="fa fa-droplet"></i>
                    <span>{{ getEntityState(config.entityId)!.attributes?.['humidity'] }}%</span>
                  </div>
                }
                @if (getEntityState(config.entityId)!.attributes?.['wind_speed']) {
                  <div class="weather-attribute-horizontal" title="Wind Speed">
                    <i class="fa fa-wind"></i>
                    <span>{{ getEntityState(config.entityId)!.attributes?.['wind_speed'] }}</span>
                  </div>
                }
              </div>
            }
          </div>
        } @else {
          <!-- Vertical layout for normal or narrow widths -->
          <div class="weather-content">
            @if (!isVerticalCompactMode()) {
              <h4 class="weather-title">Weather</h4>
            }
            <div class="weather-condition">{{ getEntityState(config.entityId)!.state }}</div>
            @if (getEntityState(config.entityId)!.attributes?.['temperature']) {
              <div class="weather-temp">{{ getEntityState(config.entityId)!.attributes?.['temperature'] }}°</div>
            }
            @if (getEntityState(config.entityId)!.attributes?.['humidity'] || getEntityState(config.entityId)!.attributes?.['wind_speed']) {
              @if (!isVerticalCompactMode()) {
                <div class="weather-attributes">
                  @if (getEntityState(config.entityId)!.attributes?.['humidity']) {
                    <div class="weather-attribute">
                      <i class="fa fa-droplet"></i>
                      <span>{{ getEntityState(config.entityId)!.attributes?.['humidity'] }}%</span>
                    </div>
                  }
                  @if (getEntityState(config.entityId)!.attributes?.['wind_speed']) {
                    <div class="weather-attribute">
                      <i class="fa fa-wind"></i>
                      <span>{{ getEntityState(config.entityId)!.attributes?.['wind_speed'] }}</span>
                    </div>
                  }
                </div>
              } @else {
                <div class="weather-attributes-compact">
                  @if (getEntityState(config.entityId)!.attributes?.['humidity']) {
                    <div class="weather-attribute-compact" title="Humidity">
                      <i class="fa fa-droplet"></i>
                      <span>{{ getEntityState(config.entityId)!.attributes?.['humidity'] }}%</span>
                    </div>
                  }
                  @if (getEntityState(config.entityId)!.attributes?.['wind_speed']) {
                    <div class="weather-attribute-compact" title="Wind Speed">
                      <i class="fa fa-wind"></i>
                      <span>{{ getEntityState(config.entityId)!.attributes?.['wind_speed'] }}</span>
                    </div>
                  }
                </div>
              }
            }
          </div>
        }
      }
    </div>
  `
})
export class WeatherWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() entityStates: Record<string, HassEntityState> | null = null;
  @Input() designerSettings?: DashboardLayout;

  get config(): WeatherConfig {
    return (this.widget?.config || {}) as WeatherConfig;
  }

  /**
   * Checks if weather data has been fetched for the configured entity.
   */
  isDataFetched(): boolean {
    const entityId = this.config.entityId;
    if (!entityId) return false;

    const state = this.getEntityState(entityId);
    if (!state || !state.attributes) return false;

    // Check if we have actual weather data - temperature is the key indicator
    const attrs = state.attributes;
    return attrs['temperature'] !== undefined && attrs['temperature'] !== null;
  }

  getEntityState(entityId?: string) {
    if (!entityId || !this.entityStates) return null;
    return this.entityStates[entityId] ?? null;
  }

  getTextFontSize(): number {
    return this.designerSettings?.textFontSize ?? 12;
  }

  getTitleFontSize(): number {
    return this.designerSettings?.titleFontSize ?? 15;
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

  isVerticalCompactMode(): boolean {
    // Use vertical compact layout when widget width is narrow (< 2 columns)
    const widgetWidth = this.widget?.position?.w ?? 1;
    return widgetWidth < 2;
  }

  isHorizontalLayout(): boolean {
    // Use horizontal layout when width is sufficient (>= 2) but height is small (< 2)
    const widgetWidth = this.widget?.position?.w ?? 1;
    const widgetHeight = this.widget?.position?.h ?? 1;
    return widgetWidth >= 2 && widgetHeight < 2;
  }

  isMinimalMode(): boolean {
    // Show only temperature when widget is very small (1x1 or 1x any height)
    const widgetWidth = this.widget?.position?.w ?? 1;
    const widgetHeight = this.widget?.position?.h ?? 1;
    return widgetWidth === 1 && widgetHeight === 1;
  }

  isCompactMode(): boolean {
    // Use compact layout when widget is small in either dimension
    return this.isVerticalCompactMode() || this.isHorizontalLayout();
  }
}
