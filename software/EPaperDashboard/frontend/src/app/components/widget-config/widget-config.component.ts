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
  RssFeedConfig
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
        // Graph can work with sensor, binary_sensor, etc.
        return allEntities.filter(e => 
          e.entity_id?.startsWith('sensor.') || 
          e.entity_id?.startsWith('binary_sensor.') ||
          e.entity_id?.startsWith('input_number.')
        );
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
        console.error('Failed to fetch entities', err);
        this.entityFetchError = (err?.error?.message || err?.message || err?.toString() || 'Unknown error');
        this.entities.set([]);
        this.loadingEntities.set(false);
      }
    });
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
