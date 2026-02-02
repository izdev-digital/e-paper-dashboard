import { Component, Input } from '@angular/core';
import type { TodoItem } from '../../services/home-assistant.service';
import { CommonModule } from '@angular/common';
import { DisplayWidgetComponent } from '../widgets/display-widget.component';
import { AppIconWidgetComponent } from '../widgets/app-icon-widget.component';
import { ImageWidgetComponent } from '../widgets/image-widget.component';
import { HeaderWidgetComponent } from '../widgets/header-widget.component';
import { MarkdownWidgetComponent } from '../widgets/markdown-widget.component';
import { WeatherWidgetComponent } from '../widgets/weather-widget.component';
import { WeatherForecastWidgetComponent } from '../widgets/weather-forecast-widget.component';
import { GraphWidgetComponent } from '../widgets/graph-widget.component';
import { TodoWidgetComponent } from '../widgets/todo-widget.component';
import { CalendarWidgetComponent } from '../widgets/calendar-widget.component';
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
  imports: [
    CommonModule,
    DisplayWidgetComponent,
    AppIconWidgetComponent,
    ImageWidgetComponent,
    HeaderWidgetComponent,
    MarkdownWidgetComponent,
    WeatherWidgetComponent,
    WeatherForecastWidgetComponent,
    GraphWidgetComponent,
    TodoWidgetComponent,
    CalendarWidgetComponent
  ],
  template: `
    <div class="widget-preview">
      @if (widget.type === 'display') {
        <app-widget-display [widget]="widget" [colorScheme]="colorScheme"></app-widget-display>
      }
      @if (widget.type === 'app-icon') {
        <app-widget-app-icon [widget]="widget" [colorScheme]="colorScheme"></app-widget-app-icon>
      }
      @if (widget.type === 'image') {
        <app-widget-image [widget]="widget" [colorScheme]="colorScheme"></app-widget-image>
      }
      @if (widget.type === 'header') {
        <app-widget-header [widget]="widget" [colorScheme]="colorScheme" [entityStates]="entityStates"></app-widget-header>
      }
      @if (widget.type === 'markdown') {
        <app-widget-markdown [widget]="widget" [colorScheme]="colorScheme"></app-widget-markdown>
      }
      @if (widget.type === 'weather') {
        <app-widget-weather [widget]="widget" [colorScheme]="colorScheme" [entityStates]="entityStates"></app-widget-weather>
      }
      @if (widget.type === 'weather-forecast') {
        <app-widget-weather-forecast [widget]="widget" [colorScheme]="colorScheme" [entityStates]="entityStates"></app-widget-weather-forecast>
      }
      @if (widget.type === 'graph') {
        <app-widget-graph [widget]="widget" [colorScheme]="colorScheme" [entityStates]="entityStates"></app-widget-graph>
      }
      @if (widget.type === 'todo') {
        <app-widget-todo [widget]="widget" [colorScheme]="colorScheme" [entityStates]="entityStates" [todoItemsByEntityId]="todoItemsByEntityId"></app-widget-todo>
      }
      @if (widget.type === 'calendar') {
        <app-widget-calendar [widget]="widget" [colorScheme]="colorScheme" [entityStates]="entityStates"></app-widget-calendar>
      }
    </div>
  `,
  styleUrls: ['./widget-preview.component.scss']
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

  // TrackBy functions for @for loops
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
      const mapped = this.todoItemsByEntityId[entityId].map((item: any, idx: number) => ({
        ...item,
        id: item.uid || item.id || idx,
        // Home Assistant uses 'status' field: 'needs_action' (incomplete) or 'completed' (complete)
        complete: item.status === 'completed' || item.status === 'done' || item.complete === true || item.completed === true || false,
        summary: item.summary || item.title || ''
      }));
      // Sort to show incomplete items first
      mapped.sort((a, b) => {
        const ac = a.complete ? 1 : 0;
        const bc = b.complete ? 1 : 0;
        return ac - bc;
      });
      console.debug('widget-preview: todo items for', entityId, mapped);
      return mapped;
    }
    // Fallback to entity state (legacy, should not be used)
    console.debug('widget-preview: fallback to entity state for', entityId);
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
