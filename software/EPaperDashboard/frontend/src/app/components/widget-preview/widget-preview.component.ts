import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { 
  WidgetConfig, 
  ColorScheme, 
  HeaderConfig, 
  MarkdownConfig, 
  CalendarConfig, 
  WeatherConfig, 
  GraphConfig, 
  TodoConfig,
  HassEntityState 
} from '../../models/types';

@Component({
  selector: 'app-widget-preview',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="widget-preview">
      @switch (widget.type) {
        <!-- Header Widget -->
        @case ('header') {
          <div class="header-widget">
            <h3 class="header-title">{{ asHeaderConfig(widget.config).title }}</h3>
            @if (asHeaderConfig(widget.config).badges?.length) {
              <div class="badges">
                @for (badge of asHeaderConfig(widget.config).badges; track badge.label) {
                  @if (badge.entityId && getEntityState(badge.entityId)) {
                    <span class="badge">
                      @if (badge.icon) {
                        <i class="fa" [ngClass]="badge.icon"></i>
                      }
                      {{ badge.label }}: 
                      <span class="badge-value">{{ getEntityState(badge.entityId)!.state }}</span>
                    </span>
                  }
                }
              </div>
            }
          </div>
        }

        <!-- Markdown Widget -->
        @case ('markdown') {
          <div class="markdown-widget">
            <div class="markdown-content">{{ asMarkdownConfig(widget.config).content }}</div>
          </div>
        }

        <!-- Calendar Widget -->
        @case ('calendar') {
          <div class="calendar-widget">
            @if (!getEntityState(asCalendarConfig(widget.config).entityId)) {
              <div class="empty-state">
                <i class="fa fa-calendar"></i>
                <p>Not configured</p>
              </div>
            }
            @if (getEntityState(asCalendarConfig(widget.config).entityId)) {
              <div class="calendar-content">
                <h4>Calendar</h4>
                <div class="calendar-state">{{ getEntityState(asCalendarConfig(widget.config).entityId)?.state }}</div>
                @if (getEntityState(asCalendarConfig(widget.config).entityId)?.attributes?.['message']) {
                  <div class="calendar-message">
                    {{ getEntityState(asCalendarConfig(widget.config).entityId)?.attributes?.['message'] }}
                  </div>
                }
              </div>
            }
          </div>
        }

        <!-- Weather Widget -->
        @case ('weather') {
          <div class="weather-widget">
            @if (!getEntityState(asWeatherConfig(widget.config).entityId)) {
              <div class="empty-state">
                <i class="fa fa-cloud-sun"></i>
                <p>Not configured</p>
              </div>
            } @else {
              <div class="weather-content">
                <div class="weather-condition">{{ getEntityState(asWeatherConfig(widget.config).entityId)!.state }}</div>
                @if (getEntityState(asWeatherConfig(widget.config).entityId)!.attributes?.['temperature']) {
                  <div class="weather-temp">{{ getEntityState(asWeatherConfig(widget.config).entityId)!.attributes?.['temperature'] }}°</div>
                }
                @if (getEntityState(asWeatherConfig(widget.config).entityId)!.attributes?.['humidity']) {
                  <div class="weather-humidity">
                    <i class="fa fa-droplet"></i> {{ getEntityState(asWeatherConfig(widget.config).entityId)!.attributes?.['humidity'] }}%
                  </div>
                }
                @if (getEntityState(asWeatherConfig(widget.config).entityId)!.attributes?.['wind_speed']) {
                  <div class="weather-wind">
                    <i class="fa fa-wind"></i> {{ getEntityState(asWeatherConfig(widget.config).entityId)!.attributes?.['wind_speed'] }}
                  </div>
                }
              </div>
            }
          </div>
        }

        <!-- Weather Forecast Widget -->
        @case ('weather-forecast') {
          <div class="weather-forecast-widget">
            @if (!getEntityState(asWeatherConfig(widget.config).entityId)) {
              <div class="empty-state">
                <i class="fa fa-cloud-sun-rain"></i>
                <p>Not configured</p>
              </div>
            } @else {
              <div class="forecast-content">
                <div class="forecast-header">{{ getEntityState(asWeatherConfig(widget.config).entityId)!.state }}</div>
                @if (getEntityState(asWeatherConfig(widget.config).entityId)!.attributes?.['forecast']) {
                  <div class="forecast-items">
                    @for (item of getForecastItems(asWeatherConfig(widget.config).entityId); track item.id) {
                      <div class="forecast-item">
                        <small>{{ getItemDate(item) }}</small>
                        <div>{{ getItemCondition(item) }}</div>
                        <small>{{ getItemTemp(item) }}°</small>
                      </div>
                    }
                  </div>
                }
              </div>
            }
          </div>
        }

        <!-- Graph Widget -->
        @case ('graph') {
          <div class="graph-widget">
            @if (!getEntityState(asGraphConfig(widget.config).entityId)) {
              <div class="empty-state">
                <i class="fa fa-chart-line"></i>
                <p>Not configured</p>
              </div>
            } @else {
              <div class="graph-content">
                <div class="graph-label">{{ asGraphConfig(widget.config).label || getEntityState(asGraphConfig(widget.config).entityId)!.entityId }}</div>
                <div class="graph-value">{{ getEntityState(asGraphConfig(widget.config).entityId)!.state }}</div>
                <div class="graph-unit">
                  @if (getEntityState(asGraphConfig(widget.config).entityId)!.attributes?.['unit_of_measurement']) {
                    {{ getEntityState(asGraphConfig(widget.config).entityId)!.attributes?.['unit_of_measurement'] }}
                  }
                </div>
              </div>
            }
          </div>
        }

        <!-- Todo Widget -->
        @case ('todo') {
          <div class="todo-widget">
            @if (!getEntityState(asTodoConfig(widget.config).entityId)) {
              <div class="empty-state">
                <i class="fa fa-list-check"></i>
                <p>Not configured</p>
              </div>
            } @else {
              <div class="todo-content">
                <h4>Tasks</h4>
                <div class="todo-state">{{ getEntityState(asTodoConfig(widget.config).entityId)!.state }}</div>
                @if (getTodoItems(asTodoConfig(widget.config).entityId).length) {
                  <div class="todo-items">
                    @for (item of getTodoItems(asTodoConfig(widget.config).entityId); track item.id) {
                      <div class="todo-item">
                        @if (item.complete) {
                          <i class="fa fa-check-circle"></i>
                        } @else {
                          <i class="fa fa-circle"></i>
                        }
                        <span [class.completed]="item.complete">{{ item.summary }}</span>
                      </div>
                    }
                  </div>
                }
              </div>
            }
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .widget-preview {
      height: 100%;
      display: flex;
      flex-direction: column;
      align-items: stretch;
      justify-content: stretch;
      font-size: 0.9rem;
      overflow: hidden;
      padding: 4px;
    }

    .header-widget {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .header-title {
      margin: 0;
      font-size: 1.1rem;
      font-weight: bold;
      word-break: break-word;
    }

    .badges {
      display: flex;
      gap: 4px;
      flex-wrap: wrap;
    }

    .badge {
      padding: 2px 6px;
      border: 1px solid currentColor;
      border-radius: 3px;
      font-size: 0.7rem;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .badge-value {
      margin-left: 2px;
      opacity: 0.8;
    }

    .markdown-widget {
      overflow: hidden;
    }

    .markdown-content {
      font-size: 0.85rem;
      word-wrap: break-word;
      overflow: hidden;
      text-overflow: ellipsis;
      display: -webkit-box;
      -webkit-line-clamp: 3;
      -webkit-box-orient: vertical;
    }

    .calendar-widget,
    .weather-widget,
    .weather-forecast-widget,
    .graph-widget,
    .todo-widget {
      display: flex;
      flex-direction: column;
      gap: 4px;
      overflow: hidden;
    }

    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      height: 100%;
      gap: 4px;
      text-align: center;
    }

    .empty-state i {
      font-size: 1.5rem;
      opacity: 0.5;
    }

    .empty-state p {
      margin: 0;
      font-size: 0.75rem;
      color: #666;
    }

    .calendar-content,
    .forecast-content,
    .graph-content,
    .todo-content {
      display: flex;
      flex-direction: column;
      gap: 4px;
      overflow: hidden;
    }

    .calendar-content h4,
    .forecast-header,
    .todo-content h4 {
      margin: 0;
      font-size: 0.9rem;
      font-weight: bold;
    }

    .calendar-state {
      font-size: 0.85rem;
    }

    .calendar-message {
      font-size: 0.75rem;
      opacity: 0.8;
      overflow: hidden;
      text-overflow: ellipsis;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
    }

    .weather-content {
      display: flex;
      flex-direction: column;
      gap: 4px;
      align-items: center;
      text-align: center;
    }

    .weather-condition {
      font-size: 0.9rem;
      font-weight: bold;
      text-transform: capitalize;
    }

    .weather-temp {
      font-size: 1.4rem;
      font-weight: bold;
    }

    .weather-humidity,
    .weather-wind {
      font-size: 0.8rem;
    }

    .forecast-items {
      display: flex;
      gap: 4px;
      overflow-x: auto;
      font-size: 0.75rem;
    }

    .forecast-item {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 2px;
      min-width: 40px;
      padding: 2px;
      border: 1px solid #ccc;
      border-radius: 2px;
    }

    .graph-label {
      font-size: 0.8rem;
      opacity: 0.7;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .graph-value {
      font-size: 1.6rem;
      font-weight: bold;
      text-align: center;
    }

    .graph-unit {
      font-size: 0.75rem;
      opacity: 0.7;
      text-align: center;
    }

    .todo-items {
      display: flex;
      flex-direction: column;
      gap: 2px;
      font-size: 0.8rem;
      overflow: hidden;
    }

    .todo-item {
      display: flex;
      gap: 4px;
      align-items: center;
      overflow: hidden;
    }

    .todo-item i {
      font-size: 0.8rem;
      flex-shrink: 0;
    }

    .todo-item span {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .todo-item span.completed {
      text-decoration: line-through;
      opacity: 0.6;
    }

    p {
      margin: 0.25rem 0;
    }

    small {
      font-size: 0.75rem;
      opacity: 0.7;
    }
  `]
})
export class WidgetPreviewComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() entityStates: Record<string, HassEntityState> | null = null;

  asHeaderConfig(config: any): HeaderConfig {
    return config as HeaderConfig;
  }

  asMarkdownConfig(config: any): MarkdownConfig {
    return config as MarkdownConfig;
  }

  asCalendarConfig(config: any): CalendarConfig {
    return config as CalendarConfig;
  }

  asWeatherConfig(config: any): WeatherConfig {
    return config as WeatherConfig;
  }

  asGraphConfig(config: any): GraphConfig {
    return config as GraphConfig;
  }

  asTodoConfig(config: any): TodoConfig {
    return config as TodoConfig;
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

  getTodoItems(entityId?: string) {
    const state = this.getEntityState(entityId);
    if (!state?.attributes?.['todo_items']) return [];
    const items = state.attributes['todo_items'] as any[];
    return items.slice(0, 3).map((item, idx) => ({ 
      ...item, 
      id: idx,
      complete: item.complete || false,
      summary: item.summary || ''
    }));
  }
}
