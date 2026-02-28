import { Component, Input, Output, EventEmitter } from '@angular/core';
import type { TodoItem } from '../../services/todo.service';
import { CommonModule } from '@angular/common';
import { AppIconWidgetComponent } from '../widgets/app-icon-widget.component';
import { ImageWidgetComponent } from '../widgets/image-widget.component';
import { HeaderWidgetComponent } from '../widgets/header-widget.component';
import { MarkdownWidgetComponent } from '../widgets/markdown-widget.component';
import { WeatherWidgetComponent } from '../widgets/weather-widget.component';
import { WeatherForecastWidgetComponent } from '../widgets/weather-forecast-widget.component';
import { GraphWidgetComponent } from '../widgets/graph-widget.component';
import { TodoWidgetComponent } from '../widgets/todo-widget.component';
import { CalendarWidgetComponent } from '../widgets/calendar-widget.component';
import { VersionWidgetComponent } from '../widgets/version-widget.component';
import { RssFeedWidgetComponent } from '../widgets/rss-feed-widget.component';
import {
  WidgetConfig,
  ColorScheme,
  HeaderConfig,
  MarkdownConfig,
  CalendarConfig,
  WeatherConfig,
  GraphConfig,
  TodoConfig,
  HassEntityState,
  DashboardLayout,
} from '../../models/types';

@Component({
  selector: 'app-widget-preview',
  standalone: true,
  imports: [
    CommonModule,
    AppIconWidgetComponent,
    ImageWidgetComponent,
    HeaderWidgetComponent,
    MarkdownWidgetComponent,
    WeatherWidgetComponent,
    WeatherForecastWidgetComponent,
    GraphWidgetComponent,
    TodoWidgetComponent,
    CalendarWidgetComponent,
    VersionWidgetComponent,
    RssFeedWidgetComponent
  ],
  template: `
    <div class="widget-preview">
      @if (!dataFetched || !hasDataForWidget()) {
        <div class="widget-preview-placeholder" [style.color]="colorScheme.text || 'currentColor'">
          <i class="fa {{ getWidgetIcon() }}" [style.color]="colorScheme.iconColor || colorScheme.accent || 'currentColor'"></i>
          <p>{{ getWidgetLabel() }}</p>
        </div>
      }
      @if (dataFetched && hasDataForWidget()) {
      @if (widget.type === 'app-icon') {
        <app-widget-app-icon [widget]="widget" [colorScheme]="colorScheme"></app-widget-app-icon>
      }
      @if (widget.type === 'image') {
        <app-widget-image [widget]="widget" [colorScheme]="colorScheme"></app-widget-image>
      }
      @if (widget.type === 'header') {
        <app-widget-header [widget]="widget" [colorScheme]="colorScheme" [entityStates]="entityStates" [designerSettings]="designerSettings"
          [internalEdit]="headerInternalEdit"
          (internalLayoutChanged)="headerLayoutChanged.emit($event)">
        </app-widget-header>
      }
      @if (widget.type === 'markdown') {
        <app-widget-markdown [widget]="widget" [colorScheme]="colorScheme" [designerSettings]="designerSettings"></app-widget-markdown>
      }
      @if (widget.type === 'weather') {
        <app-widget-weather [widget]="widget" [colorScheme]="colorScheme" [entityStates]="entityStates" [designerSettings]="designerSettings"
          [internalEdit]="weatherInternalEdit"
          (internalLayoutChanged)="weatherLayoutChanged.emit($event)">
        </app-widget-weather>
      }
      @if (widget.type === 'weather-forecast') {
        <app-widget-weather-forecast [widget]="widget" [colorScheme]="colorScheme" [entityStates]="entityStates" [designerSettings]="designerSettings"></app-widget-weather-forecast>
      }
      @if (widget.type === 'graph') {
        <app-widget-graph [widget]="widget" [colorScheme]="colorScheme" [entityStates]="entityStates" [designerSettings]="designerSettings" [dashboardId]="dashboardId"></app-widget-graph>
      }
      @if (widget.type === 'todo') {
        <app-widget-todo [widget]="widget" [colorScheme]="colorScheme" [entityStates]="entityStates" [todoItemsByEntityId]="todoItemsByEntityId" [designerSettings]="designerSettings"></app-widget-todo>
      }
      @if (widget.type === 'calendar') {
        <app-widget-calendar [widget]="widget" [colorScheme]="colorScheme" [entityStates]="entityStates" [calendarEventsByEntityId]="calendarEventsByEntityId" [designerSettings]="designerSettings">
        </app-widget-calendar>
      }
      @if (widget.type === 'version') {
        <app-widget-version [widget]="widget" [colorScheme]="colorScheme" [designerSettings]="designerSettings"></app-widget-version>
      }
      @if (widget.type === 'rss-feed') {
        <app-widget-rss-feed [widget]="widget" [colorScheme]="colorScheme" [entityStates]="entityStates" [designerSettings]="designerSettings"></app-widget-rss-feed>
      }
      }
    </div>
  `,
  styleUrls: ['./widget-preview.component.scss']
})
export class WidgetPreviewComponent {
  @Input() todoItemsByEntityId?: Record<string, TodoItem[]>;
  @Input() calendarEventsByEntityId?: Record<string, any[]>;
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() designerSettings?: DashboardLayout;
  @Input() entityStates: Record<string, HassEntityState> | null = null;
  @Input() dashboardId?: string;
  /** Whether live preview data has ever been fetched. When false, show icon+title placeholders. */
  @Input() dataFetched = true;
  /** When true, the header widget will show its internal layout editor overlay. */
  @Input() headerInternalEdit = false;
  @Output() headerLayoutChanged = new EventEmitter<HeaderConfig>();
  /** When true, the weather widget will show its internal layout editor overlay. */
  @Input() weatherInternalEdit = false;
  @Output() weatherLayoutChanged = new EventEmitter<WeatherConfig>();

  // ─── widget type → icon / label ──────────────────────────────────────────
  private static readonly WIDGET_META: Record<string, { icon: string; label: string }> = {
    'header':           { icon: 'fa-heading',       label: 'Header' },
    'markdown':         { icon: 'fa-align-left',    label: 'Markdown' },
    'calendar':         { icon: 'fa-calendar',      label: 'Calendar' },
    'weather':          { icon: 'fa-cloud-sun',     label: 'Weather' },
    'weather-forecast': { icon: 'fa-cloud-sun-rain', label: 'Forecast' },
    'graph':            { icon: 'fa-chart-line',    label: 'Graph' },
    'todo':             { icon: 'fa-list-check',    label: 'Tasks' },
    'rss-feed':         { icon: 'fa-rss',           label: 'RSS Feed' },
    'app-icon':         { icon: 'fa-rocket',        label: 'App Icon' },
    'image':            { icon: 'fa-image',         label: 'Image' },
    'version':          { icon: 'fa-code-branch',   label: 'Version' },
  };

  getWidgetIcon(): string {
    return WidgetPreviewComponent.WIDGET_META[this.widget.type]?.icon || 'fa-puzzle-piece';
  }

  getWidgetLabel(): string {
    return this.widget.titleOverride
      || WidgetPreviewComponent.WIDGET_META[this.widget.type]?.label
      || this.widget.type;
  }

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

  hasDataForWidget(): boolean {
    const type = this.widget.type;
    const config = this.widget.config as any;
    const entityId = config?.entityId as string | undefined;

    if (!['header', 'weather', 'weather-forecast', 'graph', 'todo', 'calendar', 'rss-feed'].includes(type)) {
      return true;
    }

    if (type === 'header') {
      const badges = (config?.badges ?? []) as any[];
      const entityBadges = badges.filter((b: any) => b.entityId?.trim());
      if (entityBadges.length === 0) return true;
      return entityBadges.some((b: any) =>
        this.entityStates && this.entityStates[b.entityId]
      );
    }

    if (!entityId) {
      return false;
    }

    if (type === 'todo') {
      return !!(
        this.entityStates && this.entityStates[entityId] &&
        this.todoItemsByEntityId && entityId in this.todoItemsByEntityId
      );
    }

    if (type === 'calendar') {
      return !!(
        this.entityStates && this.entityStates[entityId] &&
        this.calendarEventsByEntityId && entityId in this.calendarEventsByEntityId
      );
    }

    if (type === 'rss-feed') {
      const state = this.entityStates?.[entityId];
      const attrs = state?.attributes;
      return !!(attrs && (attrs['title'] || attrs['link'] || attrs['description']));
    }

    if (type === 'weather') {
      const state = this.entityStates?.[entityId];
      return !!(state?.attributes && state.attributes['temperature'] != null);
    }

    if (type === 'weather-forecast') {
      const state = this.entityStates?.[entityId];
      return !!(state?.attributes?.['forecast']);
    }

    return !!(this.entityStates && this.entityStates[entityId]);
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
      const mapped = this.todoItemsByEntityId[entityId].map((item: any, idx: number) => ({
        ...item,
        id: item.uid || item.id || idx,
        // Home Assistant uses 'status' field: 'needs_action' (incomplete) or 'completed' (complete)
        complete: item.status === 'completed' || item.status === 'done' || item.complete === true || item.completed === true || false,
        summary: item.summary || item.title || ''
      }));
      mapped.sort((a, b) => {
        const ac = a.complete ? 1 : 0;
        const bc = b.complete ? 1 : 0;
        return ac - bc;
      });
      return mapped;
    }
    const state = this.getEntityState(entityId);
    if (!state?.attributes?.['todo_items']) return [];
    const items = state.attributes['todo_items'] as any[];
    return items.map((item: any, idx: number) => ({
      ...item,
      id: idx,
      complete: item.status === 'completed' || item.complete === true || false,
      summary: item.summary || ''
    }));
  }

  getPendingTodoCount(entityId?: string): number {
    const items = this.getTodoItems(entityId);
    return items.filter(i => !i.complete).length;
  }

  getTodoItemsLimited(entityId?: string, w = 2, h = 2): any[] {
    // Estimate how many items fit: roughly w * (h * 2)
    const max = Math.max(1, w * Math.max(1, h * 2));
    return this.getTodoItems(entityId).slice(0, max);
  }
}
