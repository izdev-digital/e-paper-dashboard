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
    <div 
      class="todo-widget" 
      [style.--headerFontSize]="getHeaderFontSize() + 'px'"
      [style.--itemFontSize]="getItemFontSize() + 'px'"
      [style.--smallFontSize]="getSmallFontSize() + 'px'"
      [style.--iconColor]="getIconColor()"
      [style.--titleColor]="getTitleColor()"
      [style.--textColor]="getTextColor()"
      [style.color]="getTextColor()">
      @if (!isDataFetched()) {
        <div class="preview-state">
          <i class="fa fa-list-check"></i>
          <p>Tasks</p>
        </div>
      }
      @if (isDataFetched()) {
        <div class="todo-content">
          @if (widget.position.w === 1 && widget.position.h === 1) {
            <div class="todo-count">
              <i class="fa fa-list-check"></i>
              <span>{{ getPendingTodoCount(config.entityId) }}</span>
              <small>Pending</small>
            </div>
          } @else {
            <h4>{{ getEntityState(config.entityId)?.attributes?.['friendly_name'] || 'Tasks' }}</h4>
            @if (getTodoItemsLimited(config.entityId, widget.position.w, widget.position.h).length > 0) {
              <div class="todo-items">
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
  @Input() designerSettings?: DashboardLayout;

  get config(): TodoConfig { return (this.widget?.config || {}) as TodoConfig; }

  getHeaderFontSize(): number {
    return this.designerSettings?.titleFontSize ?? 15;
  }

  getItemFontSize(): number {
    return this.designerSettings?.textFontSize ?? 12;
  }

  getSmallFontSize(): number {
    return Math.round((this.designerSettings?.textFontSize ?? 12) * 0.75);
  }

  /**
   * Checks if todo data has been fetched for the configured entity.
   */
  isDataFetched(): boolean {
    const entityId = this.config.entityId;
    if (!entityId) return false;

    // Check if we have fetched todo items from the API
    if (this.todoItemsByEntityId && entityId in this.todoItemsByEntityId) {
      return true;
    }

    // Fallback: check if entity has todo items in attributes
    const state = this.getEntityState(entityId);
    if (!state || !state.attributes) return false;

    return !!state.attributes['todo_items'];
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
