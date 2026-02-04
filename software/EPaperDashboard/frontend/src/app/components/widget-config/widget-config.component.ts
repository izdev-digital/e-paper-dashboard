import { Component, Input, OnChanges, SimpleChanges, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  WidgetConfig,
  Dashboard,
  HeaderConfig,
  MarkdownConfig,
  CalendarConfig,
  WeatherConfig,
  GraphConfig,
  TodoConfig,
  AppIconConfig,
  RssFeedConfig,
  ColorScheme
} from '../../models/types';
import { HomeAssistantService, HassEntity } from '../../services/home-assistant.service';

@Component({
  selector: 'app-widget-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './widget-config.component.html',
  styleUrls: ['./widget-config.component.scss']
})
export class WidgetConfigComponent implements OnChanges {
  // TrackBy function for badges in ngFor
  // TrackBy function for badges in ngFor — use index to avoid re-rendering while editing
  trackByBadgeLabel(index: number, badge: any) {
    return index;
  }
  // TrackBy function for entities in ngFor
  trackByEntityId(index: number, entity: any) {
    return entity.entity_id || index;
  }
  private readonly homeAssistantService = inject(HomeAssistantService);


  @Input() widget!: WidgetConfig;
  @Input() dashboard!: Dashboard | null;
  @Input() availableEntities: HassEntity[] = [];
  @Input() entitiesLoading: boolean = false;
  @Input() colorScheme?: ColorScheme;

  entities = signal<any[]>([]);
  loadingEntities = signal(false);
  entityFetchError: string | null = null;

  get headerConfig(): HeaderConfig {
    return this.widget.config as HeaderConfig;
  }

  get markdownConfig(): MarkdownConfig {
    return this.widget.config as MarkdownConfig;
  }

  get calendarConfig(): CalendarConfig {
    return this.widget.config as CalendarConfig;
  }

  get weatherConfig(): WeatherConfig {
    return this.widget.config as WeatherConfig;
  }

  get weatherForecastConfig(): WeatherConfig {
    return this.widget.config as WeatherConfig;
  }

  get graphConfig(): GraphConfig {
    return this.widget.config as GraphConfig;
  }

  get todoConfig(): TodoConfig {
    return this.widget.config as TodoConfig;
  }

  get appIconConfig(): AppIconConfig {
    return this.widget.config as AppIconConfig;
  }

  get rssFeedConfig(): RssFeedConfig {
    return this.widget.config as RssFeedConfig;
  }

  ngOnChanges(changes: SimpleChanges): void {
    // If availableEntities input is provided, use it instead of fetching
    if (changes['availableEntities']) {
      const mapped = this.availableEntities.map(e => ({
        entity_id: e.entityId,
        friendly_name: e.friendlyName
      }));
      this.entities.set(mapped);
      this.loadingEntities.set(false);
      this.entityFetchError = null;
    }

    if (changes['entitiesLoading']) {
      this.loadingEntities.set(this.entitiesLoading);
    }

    // Fallback: only fetch if no availableEntities provided
    if (changes['dashboard'] && this.dashboard?.hasAccessToken && this.dashboard?.host && this.availableEntities.length === 0) {
      this.loadEntities();
    }
  }

  getFilteredEntities(): any[] {
    const allEntities = this.entities();
    console.log('[Widget Config] Total entities available:', allEntities.length, 'for widget type:', this.widget.type);

    // Filter entities based on widget type
    switch (this.widget.type) {
      case 'todo':
        return allEntities.filter(e => e.entity_id?.startsWith('todo.'));
      case 'calendar':
        return allEntities.filter(e => e.entity_id?.startsWith('calendar.'));
      case 'weather':
      case 'weather-forecast':
        return allEntities.filter(e => e.entity_id?.startsWith('weather.'));
      case 'rss-feed':
        // Feedreader creates event entities with names like event.feed_name_latest_feed
        // Show all event entities and let user select the appropriate feedreader entity
        return allEntities.filter(e => e.entity_id?.startsWith('event.'));
      case 'graph':
        // Graph can work with any entity that has numeric state values
        // Include sensors, counters, numbers, climate (temperature), light (brightness), etc.
        const filtered = allEntities.filter(e => {
          const id = e.entity_id?.toLowerCase() || '';
          return (
            id.startsWith('sensor.') ||
            id.startsWith('binary_sensor.') ||
            id.startsWith('input_number.') ||
            id.startsWith('number.') ||
            id.startsWith('counter.') ||
            id.startsWith('climate.') ||
            id.startsWith('light.') ||
            id.startsWith('cover.') ||
            id.startsWith('fan.') ||
            id.startsWith('humidifier.') ||
            id.startsWith('water_heater.') ||
            id.startsWith('weather.') ||
            id.startsWith('person.') ||
            id.startsWith('device_tracker.') ||
            id.startsWith('sun.') ||
            id.startsWith('zone.')
          );
        });
        console.log('[Widget Config] Filtered graph entities:', filtered.length);
        return filtered;
      default:
        return allEntities;
    }
  }

  loadEntities(): void {
    if (!this.dashboard) return;
    this.loadingEntities.set(true);
    this.entityFetchError = null;
    this.homeAssistantService.getEntities(this.dashboard.id).subscribe({
      next: (entities) => {
        const mapped = entities.map(e => ({
          entity_id: e.entityId,
          friendly_name: e.friendlyName
        }));
        this.entities.set(mapped);
        this.loadingEntities.set(false);
      },
      error: (err) => {
        this.entityFetchError = (err?.error?.message || err?.message || err?.toString() || 'Unknown error');
        this.entities.set([]);
        this.loadingEntities.set(false);
      }
    });
  }

  addGraphSeries(): void {
    if (!this.graphConfig.series) {
      this.graphConfig.series = [];
    }
    this.graphConfig.series.push({ entityId: '', label: '', color: this.getDefaultGraphColor(this.graphConfig.series.length) });
  }

  removeGraphSeries(index: number): void {
    if (this.graphConfig.series) {
      this.graphConfig.series.splice(index, 1);
    }
  }

  trackByGraphSeries(index: number, series: any) {
    return index;
  }

  private getDefaultGraphColor(index: number): string {
    const colors = ['#ff0000', '#00ff00', '#0000ff', '#ffff00', '#ff00ff', '#00ffff'];
    return colors[index % colors.length];
  }

  getColorName(hex: string): string {
    if (!hex) return 'Auto';
    
    const colorMap: Record<string, string> = {
      '#000000': 'Black',
      '#ffffff': 'White',
      '#ff0000': 'Red',
      '#00ff00': 'Green',
      '#0000ff': 'Blue',
      '#ffff00': 'Yellow',
      '#ff00ff': 'Magenta',
      '#00ffff': 'Cyan',
      '#808080': 'Gray',
      '#ffa500': 'Orange',
      '#800080': 'Purple',
      '#ffc0cb': 'Pink',
      '#a52a2a': 'Brown',
      '#808000': 'Olive',
      '#800000': 'Maroon',
      '#008000': 'Dark Green',
      '#000080': 'Navy'
    };

    const lowerHex = hex.toLowerCase();
    return colorMap[lowerHex] || hex;
  }

  addBadge(): void {
    const config = this.headerConfig;
    if (config.badges) {
      config.badges.push({ entityId: undefined, icon: undefined });
    } else {
      config.badges = [{ entityId: undefined, icon: undefined }];
    }
  }

  removeBadge(index: number): void {
    const config = this.headerConfig;
    if (config.badges) {
      config.badges.splice(index, 1);
    }
  }
}
