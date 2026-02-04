import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, HassEntityState, WeatherForecastConfig, DashboardLayout } from '../../models/types';

interface ForecastItem {
  id: number;
  time: string;
  condition: string;
  temp: string;
  tempHigh?: string;
  tempLow?: string;
  precip?: string;
  wind?: string;
}

@Component({
  selector: 'app-widget-weather-forecast',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./weather-forecast-widget.component.scss'],
  template: `
    <div 
      class="weather-forecast-widget"
      [class.compact]="isCompactMode()"
      [class.tiny]="isTinyMode()"
      [class.height-1]="this.widget.position.h === 1"
      [class.height-2]="this.widget.position.h === 2"
      [class.height-3plus]="this.widget.position.h >= 3"
      [style.--titleFontSize]="getTitleFontSize() + 'px'"
      [style.--textFontSize]="getTextFontSize() + 'px'"
      [style.--smallFontSize]="getSmallFontSize() + 'px'"
      [style.--titleColor]="getTitleColor()"
      [style.--textColor]="getTextColor()"
      [style.--iconColor]="getIconColor()"
      [style.color]="getTextColor()">
      
      @if (!isDataFetched()) {
        <div class="preview-state">
          <i class="fa fa-cloud-sun-rain"></i>
          <p>Forecast</p>
        </div>
      }
      
      @if (isDataFetched()) {
        @if (!isTinyMode()) {
          <div class="forecast-header">
            Forecast
          </div>
        }
        
        @if (getForecastItems().length > 0) {
          <div class="forecast-items" [class.hourly]="getForecastMode() === 'hourly'">
            @for (item of getForecastItems(); track item.id) {
              <div class="forecast-item">
                <div class="item-time">{{ item.time }}</div>
                @if (!isTinyMode()) {
                  <div class="item-condition">{{ item.condition }}</div>
                }
                
                @switch (getForecastMode()) {
                  @case ('hourly') {
                    <div class="item-temp">{{ item.temp }}{{ getTemperatureUnit() }}</div>
                  }
                  @case ('daily') {
                    <div class="item-temps">
                      <span>{{ item.tempHigh }}{{ getTemperatureUnit() }}</span>
                      <span>{{ item.tempLow }}{{ getTemperatureUnit() }}</span>
                    </div>
                  }
                  @case ('weekly') {
                    <div class="item-temps">
                      <span>{{ item.tempHigh }}{{ getTemperatureUnit() }}</span>
                      <span>{{ item.tempLow }}{{ getTemperatureUnit() }}</span>
                    </div>
                  }
                }
              </div>
            }
          </div>
        }
      }
    </div>
  `
})
export class WeatherForecastWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() entityStates: Record<string, HassEntityState> | null = null;
  @Input() designerSettings?: DashboardLayout;

  get config(): WeatherForecastConfig {
    return (this.widget?.config || {}) as WeatherForecastConfig;
  }

  getEntityState(entityId?: string) {
    if (!entityId || !this.entityStates) return null;
    return this.entityStates[entityId] ?? null;
  }

  /**
   * Checks if forecast data has been fetched for the configured entity.
   */
  isDataFetched(): boolean {
    const entityId = this.config.entityId;
    if (!entityId) return false;

    const state = this.getEntityState(entityId);
    if (!state || !state.attributes) return false;

    // Check if we have forecast data in attributes
    return !!state.attributes['forecast'];
  }

  getWindSpeedUnit(): string {
    const state = this.getEntityState(this.config.entityId);
    return state?.attributes?.['wind_speed_unit'] || '';
  }

  getTemperatureUnit(): string {
    const state = this.getEntityState(this.config.entityId);
    return state?.attributes?.['temperature_unit'] || '°C';
  }

  getForecastMode(): 'hourly' | 'daily' | 'weekly' {
    return this.config.forecastMode || 'daily';
  }

  getForecastItems(): ForecastItem[] {
    const state = this.getEntityState(this.config.entityId);
    if (!state?.attributes?.['forecast']) return [];
    
    let forecast = state.attributes['forecast'] as any[];
    const maxItems = this.getMaxForecastItems();
    const mode = this.getForecastMode();

    // Log raw forecast data for debugging
    if (forecast.length > 0) {
      console.debug('Raw forecast from Home Assistant:', {
        mode,
        count: forecast.length,
        firstItem: forecast[0],
        datetimeField: forecast[0]?.datetime || 'NOT FOUND'
      });
    }

    // For hourly mode, filter to show at least 1 hour apart
    if (mode === 'hourly') {
      forecast = this.filterHourlyForecast(forecast);
    }

    const items = forecast.slice(0, maxItems).map((item, idx) => ({
      id: idx,
      time: this.formatTime(item.datetime, mode, idx, forecast),
      condition: this.formatCondition(item.condition),
      temp: this.roundTemp(item.temperature),
      tempHigh: this.roundTemp(item.temperature),
      tempLow: this.roundTemp(item.templow),
      precip: item.precipitation_probability ? String(Math.round(Number(item.precipitation_probability))) : undefined,
      wind: item.wind_speed ? this.roundWind(item.wind_speed) : undefined
    }));

    return items;
  }

  private filterHourlyForecast(forecast: any[]): any[] {
    if (forecast.length === 0) return forecast;
    
    const filtered = [forecast[0]];
    
    for (let i = 1; i < forecast.length; i++) {
      const lastDate = new Date(filtered[filtered.length - 1].datetime);
      const currentDate = new Date(forecast[i].datetime);
      
      // Calculate difference in minutes
      const diffMinutes = (currentDate.getTime() - lastDate.getTime()) / (1000 * 60);
      
      // Only include if at least 60 minutes apart
      if (diffMinutes >= 60) {
        filtered.push(forecast[i]);
      }
    }
    
    // Log the filtering result for debugging
    console.debug(
      `Weather forecast filtering: ${forecast.length} items from Home Assistant → ${filtered.length} items after 1-hour spacing`,
      {
        original: forecast.slice(0, 3).map(f => f.datetime),
        filtered: filtered.slice(0, 3).map(f => f.datetime)
      }
    );
    
    return filtered;
  }

  private getMaxForecastItems(): number {
    if (this.config.maxItems !== undefined && this.config.maxItems > 0) {
      return this.config.maxItems;
    }

    const widgetWidth = this.widget?.position?.w ?? 1;
    const widgetHeight = this.widget?.position?.h ?? 1;
    const mode = this.getForecastMode();

    // Minimal mode (1x1): no items
    if (this.isMinimalMode()) return 0;
    
    // Tiny vertical (any width, height = 1): limit items due to space
    if (widgetHeight === 1) {
      if (mode === 'hourly') return Math.min(4, widgetWidth * 2);
      if (mode === 'daily') return Math.min(2, widgetWidth);
      if (mode === 'weekly') return 1;
      return 2;
    }

    // Compact height (height = 2): moderate items
    if (widgetHeight === 2) {
      if (mode === 'hourly') {
        if (widgetWidth === 1) return 3;
        if (widgetWidth === 2) return 5;
        return 7;
      }
      if (mode === 'daily') {
        if (widgetWidth === 1) return 2;
        if (widgetWidth === 2) return 3;
        return 4;
      }
      if (mode === 'weekly') {
        if (widgetWidth === 1) return 1;
        if (widgetWidth === 2) return 2;
        return 3;
      }
    }

    // Normal height (height >= 3): full items
    if (mode === 'hourly') {
      if (widgetWidth === 1) return 4;
      if (widgetWidth === 2) return 6;
      return 8;
    }

    if (mode === 'daily') {
      if (widgetWidth === 1) return 2;
      if (widgetWidth === 2) return 4;
      return 5;
    }

    if (mode === 'weekly') {
      if (widgetWidth === 1) return 1;
      if (widgetWidth === 2) return 2;
      return 4;
    }

    return 3;
  }

  private formatTime(datetime: string, mode: string, idx: number, forecast: any[]): string {
    if (!datetime) return '';
    
    let date: Date;
    
    // Parse the datetime string carefully to handle timezone issues
    // Home Assistant typically returns ISO format like "2026-02-04T14:30:00" or with timezone
    if (datetime.includes('Z') || datetime.includes('+') || datetime.includes('-00:')) {
      // Has explicit timezone indicator, use as-is
      date = new Date(datetime);
    } else {
      // No timezone indicator - treat as local time by parsing components manually
      // This prevents JavaScript from interpreting it as UTC
      const match = datetime.match(/(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})/);
      if (match) {
        const [, year, month, day, hours, minutes, seconds] = match;
        date = new Date(
          parseInt(year),
          parseInt(month) - 1,
          parseInt(day),
          parseInt(hours),
          parseInt(minutes),
          parseInt(seconds)
        );
      } else {
        // Fallback to standard parsing
        date = new Date(datetime);
      }
    }
    
    if (mode === 'hourly') {
      const hours = String(date.getHours()).padStart(2, '0');
      const minutes = String(date.getMinutes()).padStart(2, '0');
      return `${hours}:${minutes}`;
    }
    
    if (mode === 'weekly') {
      const days = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
      return days[date.getDay()];
    }
    
    // For daily forecast, show just the day number
    return String(date.getDate());
  }

  private formatCondition(condition: string): string {
    if (!condition) return '';
    
    const conditionMap: Record<string, string> = {
      'clear-night': 'Clear',
      'cloudy': 'Cloudy',
      'fog': 'Fog',
      'hail': 'Hail',
      'lightning': 'Storm',
      'lightning-rainy': 'Stormy',
      'partlycloudy': 'Pt. Cloudy',
      'pouring': 'Pouring',
      'rainy': 'Rainy',
      'snowy': 'Snowy',
      'snowy-rainy': 'Snowy Rain',
      'sunny': 'Sunny',
      'windy': 'Windy',
      'windy-variant': 'Windy',
      'exceptional': 'Exceptional'
    };
    
    return conditionMap[condition.toLowerCase()] || condition;
  }

  private roundTemp(temp: any): string {
    if (temp === undefined || temp === null) return '';
    return String(Math.round(Number(temp)));
  }

  private roundWind(wind: any): string {
    if (wind === undefined || wind === null) return '';
    return String(Math.round(Number(wind) * 10) / 10);
  }

  getTitleFontSize(): number {
    return this.designerSettings?.titleFontSize ?? 15;
  }

  getTextFontSize(): number {
    return this.designerSettings?.textFontSize ?? 12;
  }

  getSmallFontSize(): number {
    return Math.round((this.designerSettings?.textFontSize ?? 12) * 0.75);
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

  isTinyMode(): boolean {
    const widgetWidth = this.widget?.position?.w ?? 1;
    const widgetHeight = this.widget?.position?.h ?? 1;
    
    // Minimal content on very small widgets
    return widgetWidth <= 2 || widgetHeight === 1;
  }

  isCompactMode(): boolean {
    const widgetWidth = this.widget?.position?.w ?? 1;
    const widgetHeight = this.widget?.position?.h ?? 1;
    
    // Tight spacing but show most data
    return (widgetWidth < 2 && widgetHeight <= 2) || (widgetHeight === 2 && widgetWidth <= 2);
  }

  isMinimalMode(): boolean {
    const widgetWidth = this.widget?.position?.w ?? 1;
    const widgetHeight = this.widget?.position?.h ?? 1;
    
    return widgetWidth === 1 && widgetHeight === 1;
  }
}
