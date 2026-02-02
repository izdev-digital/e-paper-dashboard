import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import type { TodoItem } from '../../services/home-assistant.service';
import { WidgetConfig, ColorScheme, HassEntityState, TodoConfig } from '../../models/types';

@Component({
  selector: 'app-widget-todo',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./todo-widget.component.scss'],
  template: `
    <div class="todo-widget">
      @if (!config.entityId) {
        <div class="empty-state">
          <i class="fa fa-list-check"></i>
          <p>Not configured</p>
          <small>Select a todo entity to display tasks</small>
        </div>
      }
      @if (config.entityId && !getEntityState(config.entityId)) {
        <div class="empty-state">
          <i class="fa fa-list-check"></i>
          <p>Loading...</p>
        </div>
      }
      @if (config.entityId && getEntityState(config.entityId)) {
        <div class="todo-content">
          @if (widget.position.w === 1 && widget.position.h === 1) {
            <div class="todo-count" style="display:flex;flex-direction:column;align-items:center;justify-content:center;height:100%;">
              <i class="fa fa-list-check" style="font-size:1.2rem;"></i>
              <span style="font-size:1.2rem;font-weight:bold;">{{ getPendingTodoCount(config.entityId) }}</span>
              <small>Pending</small>
            </div>
          } @else {
            <h4 [style.font-size.px]="config.headerFontSize || 16">{{ getEntityState(config.entityId)?.attributes?.['friendly_name'] || 'Tasks' }}</h4>
            @if (getTodoItemsLimited(config.entityId, widget.position.w, widget.position.h).length > 0) {
              <div class="todo-items" [style.font-size.px]="config.itemFontSize || 14">
                @for (item of getTodoItemsLimited(config.entityId, widget.position.w, widget.position.h); track trackByItemId($index, item)) {
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
            } @else {
              <div class="empty-state">
                <i class="fa fa-list-check"></i>
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

  get config(): TodoConfig { return (this.widget?.config || {}) as TodoConfig; }

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
}
