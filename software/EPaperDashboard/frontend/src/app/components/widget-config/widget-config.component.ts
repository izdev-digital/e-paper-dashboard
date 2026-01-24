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
export class WidgetConfigComponent implements OnChanges {
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

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['dashboard'] && this.dashboard?.hasAccessToken && this.dashboard?.host) {
      this.loadEntities();
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
