import { Component, Input, OnInit, inject } from '@angular/core';
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
  TodoConfig 
} from '../../models/types';
import { HomeAssistantService } from '../../services/home-assistant.service';

@Component({
  selector: 'app-widget-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './widget-config.component.html',
  styleUrls: ['./widget-config.component.scss']
})
export class WidgetConfigComponent implements OnInit {
  // TrackBy function for badges in ngFor
  trackByBadgeLabel(index: number, badge: any) {
    return badge.label || badge.entityId || index;
  }
  // TrackBy function for entities in ngFor
  trackByEntityId(index: number, entity: any) {
    return entity.entity_id || index;
  }
  private readonly homeAssistantService = inject(HomeAssistantService);

  @Input() widget!: WidgetConfig;
  @Input() dashboard!: Dashboard | null;

  entities: any[] = [];
  loadingEntities = false;

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

  ngOnInit(): void {
    if (this.dashboard?.hasAccessToken && this.dashboard?.host) {
      this.loadEntities();
    }
  }

  loadEntities(): void {
    if (!this.dashboard) return;
    
    this.loadingEntities = true;
    this.homeAssistantService.getEntities(this.dashboard.id).subscribe({
      next: (entities) => {
        this.entities = entities.map(e => ({
          entity_id: e.entityId,
          friendly_name: e.friendlyName
        }));
        this.loadingEntities = false;
      },
      error: (err) => {
        console.error('Failed to fetch entities', err);
        // Fallback to placeholder data
        this.entities = [
          { entity_id: 'sensor.temperature', friendly_name: 'Temperature' },
          { entity_id: 'sensor.humidity', friendly_name: 'Humidity' },
          { entity_id: 'weather.home', friendly_name: 'Weather' },
          { entity_id: 'calendar.events', friendly_name: 'Events' },
          { entity_id: 'todo.shopping', friendly_name: 'Shopping List' }
        ];
        this.loadingEntities = false;
      }
    });
  }

  addBadge(): void {
    const config = this.headerConfig;
    if (config.badges) {
      config.badges.push({ label: 'New Badge' });
    } else {
      config.badges = [{ label: 'New Badge' }];
    }
  }

  removeBadge(index: number): void {
    const config = this.headerConfig;
    if (config.badges) {
      config.badges.splice(index, 1);
    }
  }
}
