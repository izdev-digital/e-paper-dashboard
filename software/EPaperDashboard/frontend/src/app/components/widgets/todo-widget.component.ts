import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import type { TodoItem } from '../../services/home-assistant.service';
import { WidgetConfig, ColorScheme, HassEntityState, TodoConfig, DashboardLayout } from '../../models/types';

@Component({
  selector: 'app-widget-todo',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./todo-widget.component.scss'],
  template: `
    <div class="todo-widget" [style.color]="getTextColor()">
      @if (!config.entityId) {
        <div class="empty-state">
          <i class="fa fa-list-check" [style.color]="getIconColor()"></i>
          <p>Not configured</p>
          <small>Select a todo entity to display tasks</small>
        </div>
      }
      @if (config.entityId && !getEntityState(config.entityId)) {
        <div class="empty-state">
          <i class="fa fa-list-check" [style.color]="getIconColor()"></i>
          <p>Loading...</p>
        </div>
      }
      @if (config.entityId && getEntityState(config.entityId)) {
        <div class="todo-content">
          @if (widget.position.w === 1 && widget.position.h === 1) {
            <div class="todo-count" style="display:flex;flex-direction:column;align-items:center;justify-content:center;height:100%;">
              <i class="fa fa-list-check" style="font-size:1.2rem;" [style.color]="getIconColor()"></i>
              <span style="font-size:1.2rem;font-weight:bold;">{{ getPendingTodoCount(config.entityId) }}</span>
              <small>Pending</small>
            </div>
          } @else {
            <h4 [style.font-size.px]="getHeaderFontSize()" [style.color]="getTitleColor()">{{ getEntityState(config.entityId)?.attributes?.['friendly_name'] || 'Tasks' }}</h4>
            @if (getTodoItemsLimited(config.entityId, widget.position.w, widget.position.h).length > 0) {
              <div class="todo-items" [style.font-size.px]="getItemFontSize()">
                @for (item of getTodoItemsLimited(config.entityId, widget.position.w, widget.position.h); track trackByItemId($index, item)) {
                  <div class="todo-item">
                    @if (item.complete) {
                      <i class="fa fa-check-circle" [style.color]="getIconColor()"></i>
                    } @else {
                      <i class="fa fa-circle" [style.color]="getIconColor()"></i>
                    }
                    <span [class.completed]="item.complete" [style.color]="getTextColor()">{{ item.summary }}</span>
                  </div>
                }
              </div>
            } @else {
              <div class="empty-state">
                <i class="fa fa-list-check" [style.color]="getIconColor()"></i>
                <p>No tasks found</p>
              </div>
            }
          }
        </div>
      }
    </div>
  `
})
export class TodoWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() entityStates: Record<string, HassEntityState> | null = null;
  @Input() todoItemsByEntityId?: Record<string, TodoItem[]>;
  @Input() designerSettings?: DashboardLayout;

  get config(): TodoConfig { return (this.widget?.config || {}) as TodoConfig; }

  getHeaderFontSize(): number {
    return this.designerSettings?.titleFontSize ?? 16;
  }

  getItemFontSize(): number {
    return this.designerSettings?.textFontSize ?? 14;
  }

  getEntityState(entityId?: string) {
    if (!entityId || !this.entityStates) return null;
    return this.entityStates[entityId] ?? null;
  }

  getTodoItems(entityId?: string): Array<{ id: string | number; complete: boolean; summary: string }> {
    if (this.todoItemsByEntityId && entityId && this.todoItemsByEntityId[entityId]) {
      let mapped = this.todoItemsByEntityId[entityId].map((item: any, idx: number) => ({
        ...item,
        id: item.uid || item.id || idx,
        // Home Assistant uses 'status' field: 'needs_action' (incomplete) or 'completed' (complete)
        complete: item.status === 'completed' || item.status === 'done' || item.complete === true || item.completed === true || false,
        summary: item.summary || item.title || ''
      }));
      
      // Filter out completed items if showCompleted is false
      if (this.config.showCompleted === false) {
        mapped = mapped.filter(item => !item.complete);
      }
      
      // Sort to show incomplete items first
      mapped.sort((a, b) => {
        const ac = a.complete ? 1 : 0;
        const bc = b.complete ? 1 : 0;
        return ac - bc;
      });
      return mapped;
    }
    // Fallback to entity state (legacy, should not be used)
    const state = this.getEntityState(entityId);
    if (!state?.attributes?.['todo_items']) return [];
    const items = state.attributes['todo_items'] as any[];
    return items.map((item: any, idx: number) => ({ 
      id: idx, 
      complete: item.status === 'completed' || item.complete === true || false, 
      summary: item.summary || '' 
    }));
  }

  getPendingTodoCount(entityId?: string): number { return this.getTodoItems(entityId).filter(i => !i.complete).length; }

  getTodoItemsLimited(entityId?: string, w = 2, h = 2): any[] {
    const max = Math.max(1, w * Math.max(1, h * 2));
    return this.getTodoItems(entityId).slice(0, max);
  }

  trackByItemId(index: number, item: any) { return item.id || index; }

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
}
