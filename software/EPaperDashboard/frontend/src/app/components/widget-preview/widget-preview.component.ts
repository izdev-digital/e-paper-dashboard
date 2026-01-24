import { Component, Input } from '@angular/core';
import type { TodoItem } from '../../services/home-assistant.service';
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
      <ng-container [ngSwitch]="widget.type">
        <!-- Display Widget -->

        <ng-container *ngSwitchCase="'display'">
          <div class="display-widget" [ngStyle]="{'font-size.px': asDisplayConfig(widget.config).fontSize || 18, 'color': asDisplayConfig(widget.config).color || colorScheme.foreground}">
            {{ asDisplayConfig(widget.config).text }}
          </div>
        </ng-container>

        <ng-container *ngSwitchCase="'app-icon'">
          <div class="app-icon-widget" style="display:flex;align-items:center;justify-content:center;height:100%;">
            <img [src]="asAppIconConfig(widget.config).iconUrl || '/icon.svg'" [style.width.px]="asAppIconConfig(widget.config).size || 48" [style.height.px]="asAppIconConfig(widget.config).size || 48" alt="App Icon" style="object-fit:contain;" />
          </div>
        </ng-container>

        <ng-container *ngSwitchCase="'image'">
          <div class="image-widget" style="width:100%;height:100%;display:flex;align-items:center;justify-content:center;">
            <img [src]="asImageConfig(widget.config).imageUrl" alt="Image" [style.object-fit]="asImageConfig(widget.config).fit || 'contain'" style="max-width:100%;max-height:100%;" />
          </div>
        </ng-container>

        <!-- Header Widget -->
        <ng-container *ngSwitchCase="'header'">
          <div class="header-widget">
            <h3 class="header-title">{{ asHeaderConfig(widget.config).title }}</h3>
            <div class="badges" *ngIf="asHeaderConfig(widget.config).badges?.length">
              <span class="badge" *ngFor="let badge of asHeaderConfig(widget.config).badges; trackBy: trackByBadgeLabel">
                <ng-container *ngIf="badge.entityId && getEntityState(badge.entityId)">
                  {{ getEntityState(badge.entityId)?.state }}
                  <span class="badge-value" *ngIf="getEntityState(badge.entityId)?.attributes?.['unit_of_measurement']">
                    {{ getEntityState(badge.entityId)?.attributes?.['unit_of_measurement'] }}
                  </span>
                </ng-container>
                <ng-container *ngIf="!badge.entityId || !getEntityState(badge.entityId)">
                  {{ badge.label }}
                </ng-container>
              </span>
            </div>
          </div>
        </ng-container>

        <!-- Weather Widget -->
        <ng-container *ngSwitchCase="'weather'">
          <div class="weather-widget">
            <ng-container *ngIf="!getEntityState(asWeatherConfig(widget.config).entityId)">
              <div class="empty-state">
                <i class="fa fa-cloud-sun"></i>
                <p>Not configured</p>
              </div>
            </ng-container>
            <ng-container *ngIf="getEntityState(asWeatherConfig(widget.config).entityId)">
              <div class="weather-content">
                <div class="weather-condition">{{ getEntityState(asWeatherConfig(widget.config).entityId)!.state }}</div>
                <ng-container *ngIf="getEntityState(asWeatherConfig(widget.config).entityId)!.attributes?.['temperature']">
                  <div class="weather-temp">{{ getEntityState(asWeatherConfig(widget.config).entityId)!.attributes?.['temperature'] }}°</div>
                </ng-container>
                <ng-container *ngIf="getEntityState(asWeatherConfig(widget.config).entityId)!.attributes?.['humidity']">
                  <div class="weather-humidity">
                    <i class="fa fa-droplet"></i> {{ getEntityState(asWeatherConfig(widget.config).entityId)!.attributes?.['humidity'] }}%
                  </div>
                </ng-container>
                <ng-container *ngIf="getEntityState(asWeatherConfig(widget.config).entityId)!.attributes?.['wind_speed']">
                  <div class="weather-wind">
                    <i class="fa fa-wind"></i> {{ getEntityState(asWeatherConfig(widget.config).entityId)!.attributes?.['wind_speed'] }}
                  </div>
                </ng-container>
              </div>
            </ng-container>
          </div>
        </ng-container>

        <!-- Weather Forecast Widget -->
        <ng-container *ngSwitchCase="'weather-forecast'">
          <div class="weather-forecast-widget">
            <ng-container *ngIf="!getEntityState(asWeatherConfig(widget.config).entityId)">
              <div class="empty-state">
                <i class="fa fa-cloud-sun-rain"></i>
                <p>Not configured</p>
              </div>
            </ng-container>
            <ng-container *ngIf="getEntityState(asWeatherConfig(widget.config).entityId)">
              <div class="forecast-content">
                <div class="forecast-header">{{ getEntityState(asWeatherConfig(widget.config).entityId)!.state }}</div>
                <ng-container *ngIf="getEntityState(asWeatherConfig(widget.config).entityId)!.attributes?.['forecast']">
                  <div class="forecast-items">
                    <ng-container *ngFor="let item of getForecastItems(asWeatherConfig(widget.config).entityId); trackBy: trackByItemId">
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
        </ng-container>

        <!-- Graph Widget -->
        <ng-container *ngSwitchCase="'graph'">
          <div class="graph-widget">
            <ng-container *ngIf="!getEntityState(asGraphConfig(widget.config).entityId)">
              <div class="empty-state">
                <i class="fa fa-chart-line"></i>
                <p>Not configured</p>
              </div>
            </ng-container>
            <ng-container *ngIf="getEntityState(asGraphConfig(widget.config).entityId)">
              <div class="graph-content">
                <div class="graph-label">{{ asGraphConfig(widget.config).label || getEntityState(asGraphConfig(widget.config).entityId)!.entityId }}</div>
                <div class="graph-value">{{ getEntityState(asGraphConfig(widget.config).entityId)!.state }}</div>
                <div class="graph-unit">
                  <ng-container *ngIf="getEntityState(asGraphConfig(widget.config).entityId)!.attributes?.['unit_of_measurement']">
                    {{ getEntityState(asGraphConfig(widget.config).entityId)!.attributes?.['unit_of_measurement'] }}
                  </ng-container>
                </div>
              </div>
            </ng-container>
          </div>
        </ng-container>

        <!-- Todo Widget -->
        <ng-container *ngSwitchCase="'todo'">
          <div class="todo-widget">
            <ng-container *ngIf="!getEntityState(asTodoConfig(widget.config).entityId)">
              <div class="empty-state">
                <i class="fa fa-list-check"></i>
                <p>Not configured</p>
              </div>
            </ng-container>
            <ng-container *ngIf="getEntityState(asTodoConfig(widget.config).entityId)">
              <div class="todo-content">
                <ng-container *ngIf="widget.position.w === 1 && widget.position.h === 1; else todoListView">
                  <div class="todo-count" style="display:flex;flex-direction:column;align-items:center;justify-content:center;height:100%;">
                    <i class="fa fa-list-check" style="font-size:1.2rem;"></i>
                    <span style="font-size:1.2rem;font-weight:bold;">{{ getPendingTodoCount(asTodoConfig(widget.config).entityId) }}</span>
                    <small>Pending</small>
                  </div>
                </ng-container>
                <ng-template #todoListView>
                  <h4>Tasks</h4>
                  <ng-container *ngIf="getTodoItemsLimited(asTodoConfig(widget.config).entityId, widget.position.w, widget.position.h).length > 0; else noTasks">
                    <div class="todo-items">
                      <ng-container *ngFor="let item of getTodoItemsLimited(asTodoConfig(widget.config).entityId, widget.position.w, widget.position.h); trackBy: trackByItemId">
                        <div class="todo-item">
                          <ng-container *ngIf="item.complete; else notComplete">
                            <i class="fa fa-check-circle"></i>
                          </ng-container>
                          <ng-template #notComplete>
                            <i class="fa fa-circle"></i>
                          </ng-template>
                          <span [class.completed]="item.complete">{{ item.summary }}</span>
                        </div>
                      </ng-container>
                    </div>
                  </ng-container>
                  <ng-template #noTasks>
                    <div class="empty-state">
                      <i class="fa fa-list-check"></i>
                      <p>No tasks found.</p>
                      <small *ngIf="getEntityState(asTodoConfig(widget.config).entityId)">
                        State: {{ getEntityState(asTodoConfig(widget.config).entityId)!.state }}<br>
                        Attributes: {{ getEntityState(asTodoConfig(widget.config).entityId)!.attributes | json }}
                      </small>
                    </div>
                  </ng-template>
                </ng-template>
              </div>
            </ng-container>
          </div>
        </ng-container>
      </ng-container>
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
  @Input() todoItemsByEntityId?: Record<string, TodoItem[]>;
  // ...existing code...
  // Add missing config helpers for new widget types
  asDisplayConfig(config: any) {
    return config as any;
  }
  asAppIconConfig(config: any) {
    return config as any;
  }
  asImageConfig(config: any) {
    return config as any;
  }

  // Add trackBy functions for *ngFor
  trackByBadgeLabel(index: number, badge: any) {
    return badge.label || badge.entityId || index;
  }
  trackByItemId(index: number, item: any) {
    return item.id || index;
  }
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

  getTodoItems(entityId?: string): Array<{ id: string | number; complete: boolean; summary: string }> {
    if (this.todoItemsByEntityId && entityId && this.todoItemsByEntityId[entityId]) {
      // Map backend items to expected format for display
      return this.todoItemsByEntityId[entityId].map((item: any, idx: number) => ({
        ...item,
        id: item.uid || item.id || idx,
        // Home Assistant todo items may represent completion in different ways
        complete: (item.status && (item.status === 'completed' || item.status === 'done')) || item.complete === true || item.completed === true || false,
        summary: item.summary || item.title || ''
      }));
    }
    // fallback to old state-based logic (should not be used)
    const state = this.getEntityState(entityId);
    if (!state?.attributes?.['todo_items']) return [];
    const items = state.attributes['todo_items'] as any[];
    return items.map((item: any, idx: number) => ({
      ...item,
      id: idx,
      complete: item.complete || false,
      summary: item.summary || ''
    }));
  }

  getPendingTodoCount(entityId?: string): number {
    const items = this.getTodoItems(entityId);
    return items.filter(i => !i.complete).length;
  }

  getTodoItemsLimited(entityId?: string, w = 2, h = 2): any[] {
    // Estimate how many items fit: 1 row per h, 2 items per w (roughly)
    const max = Math.max(1, w * h);
    return this.getTodoItems(entityId).slice(0, max);
  }
}
