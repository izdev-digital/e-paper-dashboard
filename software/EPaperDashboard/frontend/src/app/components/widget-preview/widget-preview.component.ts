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
    TodoWidgetComponent
    ,CalendarWidgetComponent
  ],
  template: `
    <div class="widget-preview">
      <app-widget-display *ngIf="widget.type === 'display'" [widget]="widget" [colorScheme]="colorScheme"></app-widget-display>
      <app-widget-app-icon *ngIf="widget.type === 'app-icon'" [widget]="widget" [colorScheme]="colorScheme"></app-widget-app-icon>
      <app-widget-image *ngIf="widget.type === 'image'" [widget]="widget" [colorScheme]="colorScheme"></app-widget-image>
      <app-widget-header *ngIf="widget.type === 'header'" [widget]="widget" [colorScheme]="colorScheme" [entityStates]="entityStates"></app-widget-header>
      <app-widget-markdown *ngIf="widget.type === 'markdown'" [widget]="widget" [colorScheme]="colorScheme"></app-widget-markdown>
      <app-widget-weather *ngIf="widget.type === 'weather'" [widget]="widget" [colorScheme]="colorScheme" [entityStates]="entityStates"></app-widget-weather>
      <app-widget-weather-forecast *ngIf="widget.type === 'weather-forecast'" [widget]="widget" [colorScheme]="colorScheme" [entityStates]="entityStates"></app-widget-weather-forecast>
      <app-widget-graph *ngIf="widget.type === 'graph'" [widget]="widget" [colorScheme]="colorScheme" [entityStates]="entityStates"></app-widget-graph>
      <app-widget-todo *ngIf="widget.type === 'todo'" [widget]="widget" [colorScheme]="colorScheme" [entityStates]="entityStates" [todoItemsByEntityId]="todoItemsByEntityId"></app-widget-todo>
      <app-widget-calendar *ngIf="widget.type === 'calendar'" [widget]="widget" [colorScheme]="colorScheme" [entityStates]="entityStates"></app-widget-calendar>
    </div>
  `
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
      const mapped = this.todoItemsByEntityId[entityId].map((item: any, idx: number) => ({
        ...item,
        id: item.uid || item.id || idx,
        // Home Assistant todo items may represent completion in different ways
        complete: (item.status && (item.status === 'completed' || item.status === 'done')) || item.complete === true || item.completed === true || false,
        summary: item.summary || item.title || ''
      }));
      // Prefer showing incomplete items first
      mapped.sort((a, b) => {
        const ac = a.complete ? 1 : 0;
        const bc = b.complete ? 1 : 0;
        return ac - bc;
      });
      console.debug('widget-preview: todo items for', entityId, mapped);
      return mapped;
    }
    // fallback to old state-based logic (should not be used)
    console.debug('widget-preview: fallback to entity state for', entityId);
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
    // Estimate how many items fit: roughly w * (h * 2)
    const max = Math.max(1, w * Math.max(1, h * 2));
    return this.getTodoItems(entityId).slice(0, max);
  }
}
