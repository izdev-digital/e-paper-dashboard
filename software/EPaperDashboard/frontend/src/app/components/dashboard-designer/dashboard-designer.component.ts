




import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DashboardService } from '../../services/dashboard.service';
import { ToastService } from '../../services/toast.service';
import { HomeAssistantService, HassEntity } from '../../services/home-assistant.service';
import { WidgetPreviewComponent } from '../widget-preview/widget-preview.component';
import { WidgetConfigComponent } from '../widget-config/widget-config.component';

import type { TodoItem } from '../../services/home-assistant.service';
import {
  Dashboard,
  DashboardLayout,
  WidgetConfig,
  WidgetType,
  ColorScheme,
  DEFAULT_COLOR_SCHEMES,
  WidgetPosition,
  HassEntityState
} from '../../models/types';

@Component({
  selector: 'app-dashboard-designer',
  standalone: true,
  imports: [CommonModule, FormsModule, WidgetPreviewComponent, WidgetConfigComponent],
  templateUrl: './dashboard-designer.component.html',
  styleUrls: ['./dashboard-designer.component.scss']
})
export class DashboardDesignerComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dashboardService = inject(DashboardService);
  private readonly toastService = inject(ToastService);
  private readonly homeAssistantService = inject(HomeAssistantService);

  // Dashboard data
  dashboardId: string = '';
  dashboard = signal<Dashboard | null>(null);
  layout = signal<DashboardLayout>({
    width: 800,
    height: 480,
    gridCols: 12,
    gridRows: 8,
    colorScheme: DEFAULT_COLOR_SCHEMES[0],
    widgets: [],
    canvasPadding: 16,
    widgetGap: 4,
    widgetBorder: 3
  });

  // UI State
  colorSchemes = DEFAULT_COLOR_SCHEMES;
  availableWidgets: { type: WidgetType; label: string; icon: string }[] = [
    { type: 'header', label: 'Header', icon: 'fa-heading' },
    { type: 'markdown', label: 'Markdown', icon: 'fa-align-left' },
    { type: 'calendar', label: 'Calendar', icon: 'fa-calendar' },
    { type: 'weather', label: 'Weather', icon: 'fa-cloud-sun' },
    { type: 'weather-forecast', label: 'Weather Forecast', icon: 'fa-cloud-sun-rain' },
    { type: 'graph', label: 'Graph', icon: 'fa-chart-line' },
    { type: 'todo', label: 'Todo List', icon: 'fa-list-check' },
    { type: 'display', label: 'Display', icon: 'fa-display' },
    { type: 'app-icon', label: 'App Icon', icon: 'fa-rocket' },
    { type: 'image', label: 'Image', icon: 'fa-image' }
  ];

  selectedWidget = signal<WidgetConfig | null>(null);
  ghost = signal<{ id: string; position: WidgetPosition } | null>(null);
  isLoading = signal(false);
  livePreviewLoading = signal(false);
  entityStates = signal<Record<string, HassEntityState>>({});
  availableEntities = signal<HassEntity[]>([]);
  entitiesLoading = signal(false);
  activeTab = signal<'dashboard' | 'widgets' | 'properties'>('dashboard');
  todoItemsByEntityId = signal<Record<string, TodoItem[]>>({});
  calendarEventsByEntityId = signal<Record<string, any[]>>({});
  
  // Tab navigation
  tabOrder: Array<'dashboard' | 'widgets' | 'properties'> = ['dashboard', 'widgets', 'properties'];

  // Drag state
  private dragStartPos = { x: 0, y: 0 };
  private dragStartWidget = { x: 0, y: 0, w: 0, h: 0 };

  ngOnInit(): void {
    this.dashboardId = this.route.snapshot.paramMap.get('id') || '';
    if (this.dashboardId) {
      this.isLoading.set(true);
      this.loadDashboard();
    } else {
      this.toastService.show('No dashboard ID provided', 'error');
      this.isLoading.set(false);
    }

    window.addEventListener('keydown', this.onGlobalKeyDown);
  }

  // Dashboard loading
  loadDashboard(): void {
    this.dashboardService.getDashboard(this.dashboardId).subscribe({
      next: (dashboard) => {
        this.dashboard.set(dashboard);
        if (dashboard.layoutConfig) {
          try {
            const parsedLayout = JSON.parse(dashboard.layoutConfig);
            const colorScheme = parsedLayout.colorScheme?.name 
              ? this.colorSchemes.find(cs => cs.name === parsedLayout.colorScheme.name) || DEFAULT_COLOR_SCHEMES[0]
              : DEFAULT_COLOR_SCHEMES[0];
            
            this.layout.set({
              width: parsedLayout.width || 800,
              height: parsedLayout.height || 480,
              gridCols: parsedLayout.gridCols || 12,
              gridRows: parsedLayout.gridRows || 8,
              colorScheme: colorScheme,
              widgets: parsedLayout.widgets || [],
              canvasPadding: typeof parsedLayout.canvasPadding === 'number' ? parsedLayout.canvasPadding : 16,
              widgetGap: typeof parsedLayout.widgetGap === 'number' ? parsedLayout.widgetGap : 4,
              widgetBorder: typeof parsedLayout.widgetBorder === 'number' ? parsedLayout.widgetBorder : 3
            });
          } catch (e) {
            console.error('Failed to parse layout config', e);
          }
        }
        this.loadAvailableEntities();
      },
      error: (err) => {
        console.error('Error loading dashboard:', err);
        this.toastService.show('Failed to load dashboard', 'error');
        this.isLoading.set(false);
      }
    });
  }

  loadAvailableEntities(): void {
    if (!this.dashboardId) {
      console.warn('Dashboard ID missing, cannot load entities');
      return;
    }

    this.entitiesLoading.set(true);
    this.homeAssistantService.getEntities(this.dashboardId).subscribe({
      next: (entities) => {
        this.availableEntities.set(entities);
        this.entitiesLoading.set(false);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load available entities:', err);
        this.availableEntities.set([]);
        this.entitiesLoading.set(false);
        this.isLoading.set(false);
      }
    });
  }

  // Widget management
  onWidgetSelect(widget: WidgetConfig): void {
    this.selectedWidget.set(widget);
    this.activeTab.set('properties');
  }

  deleteWidget(widget: WidgetConfig): void {
    this.layout.update(layout => ({
      ...layout,
      widgets: layout.widgets.filter(w => w.id !== widget.id)
    }));
    if (this.selectedWidget()?.id === widget.id) {
      this.selectedWidget.set(null);
    }
  }

  updateWidgetPosition(widget: WidgetConfig, position: Partial<WidgetPosition>): void {
    this.layout.update(layout => ({
      ...layout,
      widgets: layout.widgets.map(w => 
        w.id === widget.id ? { ...w, position: { ...w.position, ...position } } : w
      )
    }));
  }

  // Drag and drop from toolbox
  onToolboxWidgetMouseDown(event: MouseEvent, widget: { type: WidgetType; label: string; icon: string }): void {
      event.preventDefault();
      const layout = this.layout();
      const canvas = document.querySelector('.dashboard-canvas') as HTMLElement;
      if (!canvas) return;

      // Create a small preview element that follows the cursor
      const preview = document.createElement('div');
      preview.className = 'toolbox-drag-preview';
      preview.style.position = 'fixed';
      preview.style.pointerEvents = 'none';
      preview.style.zIndex = '9999';
      preview.style.opacity = '0.85';
      preview.innerHTML = `<i class='fa ${widget.icon}'></i> ${widget.label}`;
      document.body.appendChild(preview);

      const movePreview = (e: MouseEvent) => {
        preview.style.left = e.clientX + 8 + 'px';
        preview.style.top = e.clientY + 8 + 'px';
      };
      movePreview(event);

      const onMouseMove = (e: MouseEvent) => {
        movePreview(e);

        // Update ghost if over canvas
        const rect = canvas.getBoundingClientRect();
        if (e.clientX >= rect.left && e.clientX <= rect.right && e.clientY >= rect.top && e.clientY <= rect.bottom) {
          const padding = layout.canvasPadding ?? 0;
          const gap = layout.widgetGap ?? 0;
          const cols = Math.max(1, layout.gridCols);
          const rows = Math.max(1, layout.gridRows);
          const innerWidth = Math.max(0, rect.width - padding * 2 - gap * (cols - 1));
          const innerHeight = Math.max(0, rect.height - padding * 2 - gap * (rows - 1));
          const cellWidth = innerWidth / cols;
          const cellHeight = innerHeight / rows;
          const slotWidth = cellWidth + gap;
          const slotHeight = cellHeight + gap;
          const relX = e.clientX - rect.left - padding;
          const relY = e.clientY - rect.top - padding;
          const x = Math.max(0, Math.min(layout.gridCols - 1, Math.floor(relX / slotWidth)));
          const y = Math.max(0, Math.min(layout.gridRows - 1, Math.floor(relY / slotHeight)));
          const w = Math.min(4, layout.gridCols - x);
          const h = Math.min(2, layout.gridRows - y);
          this.ghost.set({ id: 'toolbox-' + widget.type, position: { x, y, w, h } });
        } else {
          this.ghost.set(null);
        }
      };

      const onMouseUp = (e: MouseEvent) => {
        document.removeEventListener('mousemove', onMouseMove);
        document.removeEventListener('mouseup', onMouseUp);
        preview.remove();

        // If ghost present, commit widget
        const g = this.ghost();
        if (g) {
          const newWidget: WidgetConfig = {
            id: this.generateId(),
            type: widget.type,
            position: { ...g.position },
            config: this.getDefaultConfig(widget.type)
          };
          this.layout.update(l => ({ ...l, widgets: [...l.widgets, newWidget] }));
        }
        this.ghost.set(null);
      };

      document.addEventListener('mousemove', onMouseMove);
      document.addEventListener('mouseup', onMouseUp);
    }

  // Tab navigation
  switchTab(direction: 'left' | 'right'): void {
    const current = this.tabOrder.indexOf(this.activeTab());
    let newIdx;
    if (direction === 'left') {
      newIdx = (current - 1 + this.tabOrder.length) % this.tabOrder.length;
    } else {
      newIdx = (current + 1) % this.tabOrder.length;
    }
    this.activeTab.set(this.tabOrder[newIdx]);
    setTimeout(() => {
      const tabBar = document.querySelector('.custom-tab-bar') as HTMLElement;
      if (!tabBar) return;
      const tabBtns = Array.from(tabBar.querySelectorAll('.tab-btn')) as HTMLElement[];
      const activeBtn = tabBtns[newIdx];
      if (activeBtn) {
        const barRect = tabBar.getBoundingClientRect();
        const btnRect = activeBtn.getBoundingClientRect();
        if (btnRect.left < barRect.left) {
          tabBar.scrollBy({ left: btnRect.left - barRect.left - 16, behavior: 'smooth' });
        } else if (btnRect.right > barRect.right) {
          tabBar.scrollBy({ left: btnRect.right - barRect.right + 16, behavior: 'smooth' });
        }
      }
    }, 0);
  }

  // Widget drag and drop on canvas
  onWidgetMouseDown(event: MouseEvent, widget: WidgetConfig): void {
    event.stopPropagation();
    this.selectedWidget.set(widget);
    const target = event.target as HTMLElement;
    if (target.classList.contains('resize-handle')) {
      const dir = target.dataset['direction'];
      if (dir) {
        this.startResize(event, widget, dir as 'n' | 's' | 'e' | 'w' | 'ne' | 'nw' | 'se' | 'sw');
        return;
      }
    }
    this.startDrag(event, widget);
  }

  private startDrag(event: MouseEvent, widget: WidgetConfig): void {
    let isDragging = true;
    this.dragStartPos = { x: event.clientX, y: event.clientY };
    this.dragStartWidget = { ...widget.position };

    const canvas = document.querySelector('.dashboard-canvas') as HTMLElement;
    const rect = canvas.getBoundingClientRect();
    const layout = this.layout();
    const padding = layout.canvasPadding ?? 0;
    const gap = layout.widgetGap ?? 0;
    const cols = Math.max(1, layout.gridCols);
    const rows = Math.max(1, layout.gridRows);
    const innerWidth = Math.max(0, rect.width - padding * 2 - gap * (cols - 1));
    const innerHeight = Math.max(0, rect.height - padding * 2 - gap * (rows - 1));
    const cellWidth = innerWidth / cols;
    const cellHeight = innerHeight / rows;
    const slotWidth = cellWidth + gap;
    const slotHeight = cellHeight + gap;

    this.ghost.set({ id: widget.id, position: { ...widget.position } });

    const onMouseMove = (e: MouseEvent) => {
      if (!isDragging) return;

      const deltaX = e.clientX - this.dragStartPos.x;
      const deltaY = e.clientY - this.dragStartPos.y;
      const gridDeltaX = Math.round(deltaX / slotWidth);
      const gridDeltaY = Math.round(deltaY / slotHeight);
      const newX = Math.max(0, Math.min(layout.gridCols - widget.position.w, this.dragStartWidget.x + gridDeltaX));
      const newY = Math.max(0, Math.min(layout.gridRows - widget.position.h, this.dragStartWidget.y + gridDeltaY));

      this.ghost.set({ id: widget.id, position: { ...widget.position, x: newX, y: newY } });
    };

    const onMouseUp = () => {
      isDragging = false;
      const g = this.ghost();
      if (g) {
        this.layout.update(l => ({
          ...l,
          widgets: l.widgets.map(w => w.id === g.id ? { ...w, position: { ...g.position } } : w)
        }));
      }
      this.ghost.set(null);
      document.removeEventListener('mousemove', onMouseMove);
      document.removeEventListener('mouseup', onMouseUp);
    };

    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);
  }

  private startResize(event: MouseEvent, widget: WidgetConfig, direction: 'n' | 's' | 'e' | 'w' | 'ne' | 'nw' | 'se' | 'sw'): void {
    event.stopPropagation();
    let isResizing = true;
    this.dragStartPos = { x: event.clientX, y: event.clientY };
    this.dragStartWidget = { ...widget.position };
    const canvas = document.querySelector('.dashboard-canvas') as HTMLElement;
    const rect = canvas.getBoundingClientRect();
    const layout = this.layout();
    const padding = layout.canvasPadding ?? 0;
    const gap = layout.widgetGap ?? 0;
    const cols = Math.max(1, layout.gridCols);
    const rows = Math.max(1, layout.gridRows);
    const innerWidth = Math.max(0, rect.width - padding * 2 - gap * (cols - 1));
    const innerHeight = Math.max(0, rect.height - padding * 2 - gap * (rows - 1));
    const cellWidth = innerWidth / cols;
    const cellHeight = innerHeight / rows;
    const slotWidth = cellWidth + gap;
    const slotHeight = cellHeight + gap;

    this.ghost.set({ id: widget.id, position: { ...widget.position } });

    const onMouseMove = (e: MouseEvent) => {
      if (!isResizing) return;
      const deltaX = e.clientX - this.dragStartPos.x;
      const deltaY = e.clientY - this.dragStartPos.y;
      const gridDeltaX = Math.round(deltaX / slotWidth);
      const gridDeltaY = Math.round(deltaY / slotHeight);
      let newX = this.dragStartWidget.x;
      let newY = this.dragStartWidget.y;
      let newW = this.dragStartWidget.w;
      let newH = this.dragStartWidget.h;

      if (direction.includes('e')) {
        newW = Math.max(1, Math.min(cols - this.dragStartWidget.x, this.dragStartWidget.w + gridDeltaX));
      }
      if (direction.includes('w')) {
        newX = this.dragStartWidget.x + gridDeltaX;
        newW = this.dragStartWidget.w - gridDeltaX;
        if (newX < 0) {
          newW += newX;
          newX = 0;
        }
        if (newW < 1) {
          const diff = 1 - newW;
          newW = 1;
          newX = Math.max(0, newX - diff);
        }
        if (newX + newW > cols) {
          newW = cols - newX;
        }
      }

      if (direction.includes('s')) {
        newH = Math.max(1, Math.min(rows - this.dragStartWidget.y, this.dragStartWidget.h + gridDeltaY));
      }
      if (direction.includes('n')) {
        newY = this.dragStartWidget.y + gridDeltaY;
        newH = this.dragStartWidget.h - gridDeltaY;
        if (newY < 0) {
          newH += newY;
          newY = 0;
        }
        if (newH < 1) {
          const diff = 1 - newH;
          newH = 1;
          newY = Math.max(0, newY - diff);
        }
        if (newY + newH > rows) {
          newH = rows - newY;
        }
      }

      newX = Math.max(0, Math.min(cols - 1, newX));
      newY = Math.max(0, Math.min(rows - 1, newY));
      newW = Math.max(1, Math.min(cols - newX, newW));
      newH = Math.max(1, Math.min(rows - newY, newH));

      this.ghost.set({ id: widget.id, position: { x: newX, y: newY, w: newW, h: newH } });
    };

    const onMouseUp = () => {
      isResizing = false;
      const g = this.ghost();
      if (g) {
        this.layout.update(l => ({
          ...l,
          widgets: l.widgets.map(w => w.id === g.id ? { ...w, position: { ...g.position } } : w)
        }));
      }
      this.ghost.set(null);
      document.removeEventListener('mousemove', onMouseMove);
      document.removeEventListener('mouseup', onMouseUp);
    };

    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);
  }

  moveWidget(widget: WidgetConfig, deltaX: number, deltaY: number): void {
    const layout = this.layout();
    const newX = Math.max(0, Math.min(layout.gridCols - widget.position.w, widget.position.x + deltaX));
    const newY = Math.max(0, Math.min(layout.gridRows - widget.position.h, widget.position.y + deltaY));
    this.layout.update(l => ({
      ...l,
      widgets: l.widgets.map(w => 
        w.id === widget.id ? { ...w, position: { ...w.position, x: newX, y: newY } } : w
      )
    }));
  }

  private onGlobalKeyDown = (e: KeyboardEvent) => {
    const sel = this.selectedWidget();
    if (!sel) return;

    let dx = 0;
    let dy = 0;
    const step = e.shiftKey ? 5 : 1;
    switch (e.key) {
      case 'ArrowLeft': dx = -step; break;
      case 'ArrowRight': dx = step; break;
      case 'ArrowUp': dy = -step; break;
      case 'ArrowDown': dy = step; break;
      default: return;
    }
    e.preventDefault();
    this.moveWidget(sel, dx, dy);
  }

  // Dashboard operations
  saveDashboard(): void {
    if (!this.dashboard()) return;

    const layoutConfig = JSON.stringify(this.layout());
    this.dashboardService.updateDashboard(this.dashboardId, { layoutConfig }).subscribe({
      next: () => {
        this.toastService.show('Dashboard layout saved successfully', 'success');
      },
      error: (err) => {
        console.error('Error saving dashboard:', err);
        if (err.status === 401 || err.status === 403) {
          this.toastService.show('Authentication error. Please log in again.', 'error');
          this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } });
        } else {
          this.toastService.show('Failed to save dashboard layout', 'error');
        }
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/dashboards', this.dashboardId, 'edit']);
  }

  // Layout updates
  updateColorScheme(scheme: ColorScheme): void {
    this.layout.update(layout => ({ ...layout, colorScheme: scheme }));
  }

  updateLayoutWidth(width: number): void {
    this.layout.update(layout => ({ ...layout, width }));
  }

  updateLayoutHeight(height: number): void {
    this.layout.update(layout => ({ ...layout, height }));
  }

  updateLayoutGridCols(gridCols: number): void {
    this.layout.update(layout => ({ ...layout, gridCols }));
  }

  updateLayoutGridRows(gridRows: number): void {
    this.layout.update(layout => ({ ...layout, gridRows }));
  }

  updateCanvasPadding(padding: number): void {
    this.layout.update(layout => ({ ...layout, canvasPadding: padding }));
  }

  updateWidgetGap(gap: number): void {
    this.layout.update(layout => ({ ...layout, widgetGap: gap }));
  }

  updateWidgetBorder(border: number): void {
    this.layout.update(layout => ({ ...layout, widgetBorder: border }));
  }

  // Live preview data
  refreshLivePreview(): void {
    if (!this.dashboardId) {
      console.warn('Dashboard ID missing, cannot load live data');
      return;
    }

    const ids = this.collectEntityIds();
    
    if (ids.length === 0) {
      this.entityStates.set({});
      return;
    }

    this.livePreviewLoading.set(true);
    this.homeAssistantService.getEntityStates(this.dashboardId, ids).subscribe({
      next: (states) => {
        const map: Record<string, HassEntityState> = {};
        states.forEach(s => { map[s.entityId] = s; });
        this.entityStates.set(map);

        const todoEntityIds = this.layout().widgets
          .filter(w => w.type === 'todo' && (w.config as any).entityId)
          .map(w => (w.config as any).entityId)
          .filter((id, idx, arr) => !!id && arr.indexOf(id) === idx);

        if (todoEntityIds.length === 0) {
          this.todoItemsByEntityId.set({});
          this.livePreviewLoading.set(false);
          return;
        }

        let completed = 0;
        const todoMap: Record<string, TodoItem[]> = {};
        todoEntityIds.forEach(entityId => {
          this.homeAssistantService.getTodoItems(this.dashboardId, entityId).subscribe({
            next: (items) => {
              todoMap[entityId] = items || [];
              completed++;
              if (completed === todoEntityIds.length) {
                this.todoItemsByEntityId.set(todoMap);
                this.fetchCalendarEvents();
              }
            },
            error: () => {
              todoMap[entityId] = [];
              completed++;
              if (completed === todoEntityIds.length) {
                this.todoItemsByEntityId.set(todoMap);
                this.fetchCalendarEvents();
              }
            }
          });
        });
      },
      error: (err) => {
        console.error('Failed to load live data from Home Assistant:', err);
        this.livePreviewLoading.set(false);
      }
    });
  }

  private fetchCalendarEvents() {
    const calendarEntityIds = this.layout().widgets
      .filter(w => w.type === 'calendar' && (w.config as any).entityId)
      .map(w => (w.config as any).entityId)
      .filter((id, idx, arr) => !!id && arr.indexOf(id) === idx);

    if (calendarEntityIds.length === 0) {
      this.calendarEventsByEntityId.set({});
      this.livePreviewLoading.set(false);
      return;
    }

    let completed = 0;
    const calendarMap: Record<string, any[]> = {};
    calendarEntityIds.forEach(entityId => {
      this.homeAssistantService.getCalendarEvents(this.dashboardId, entityId).subscribe({
        next: (events) => {
          calendarMap[entityId] = events || [];
          completed++;
          if (completed === calendarEntityIds.length) {
            this.calendarEventsByEntityId.set(calendarMap);
            this.livePreviewLoading.set(false);
          }
        },
        error: () => {
          calendarMap[entityId] = [];
          completed++;
          if (completed === calendarEntityIds.length) {
            this.calendarEventsByEntityId.set(calendarMap);
            this.livePreviewLoading.set(false);
          }
        }
      });
    });
  }

  private collectEntityIds(): string[] {
    const ids = new Set<string>();
    for (const widget of this.layout().widgets) {
      switch (widget.type) {
        case 'calendar':
          if ((widget.config as any).entityId) ids.add((widget.config as any).entityId);
          break;
        case 'weather':
        case 'weather-forecast':
          if ((widget.config as any).entityId) ids.add((widget.config as any).entityId);
          break;
        case 'graph':
        case 'todo':
          if ((widget.config as any).entityId) ids.add((widget.config as any).entityId);
          break;
        case 'header': {
          const cfg = widget.config as any;
          if (cfg?.badges?.length) {
            cfg.badges.forEach((b: any) => {
              if (b?.entityId) ids.add(b.entityId);
            });
          }
          break;
        }
      }
    }
    return Array.from(ids);
  }

  // Style helpers
  getCanvasStyle(): any {
    const layout = this.layout();
    return {
      width: `${layout.width}px`,
      minWidth: `${layout.width}px`,
      'min-width': `${layout.width}px`,
      height: `${layout.height}px`,
      minHeight: `${layout.height}px`,
      'min-height': `${layout.height}px`,
      backgroundColor: layout.colorScheme.background,
      color: layout.colorScheme.text,
      display: 'grid',
      gridTemplateColumns: `repeat(${layout.gridCols}, 1fr)`,
      gridTemplateRows: `repeat(${layout.gridRows}, 1fr)`,
      gap: `${layout.widgetGap ?? 0}px`,
      padding: `${layout.canvasPadding ?? 0}px`,
      '--widget-border': `${layout.widgetBorder ?? 3}px`,
      position: 'relative',
      boxSizing: 'border-box',
    };
  }

  getGridOverlayStyle(): any {
    const layout = this.layout();
    const padding = layout.canvasPadding ?? 0;
    const gap = layout.widgetGap ?? 0;
    const cols = Math.max(1, layout.gridCols);
    const rows = Math.max(1, layout.gridRows);
    const canvasEl = document.querySelector('.dashboard-canvas') as HTMLElement | null;
    const rect = canvasEl ? canvasEl.getBoundingClientRect() : null;
    const totalWidth = rect ? rect.width : layout.width;
    const totalHeight = rect ? rect.height : layout.height;

    const innerWidth = Math.max(0, totalWidth - padding * 2 - gap * (cols - 1));
    const innerHeight = Math.max(0, totalHeight - padding * 2 - gap * (rows - 1));

    const cellWidth = innerWidth / cols;
    const cellHeight = innerHeight / rows;
    const slotWidth = cellWidth + gap;
    const slotHeight = cellHeight + gap;
    const lineColor = 'rgba(0,0,0,0.06)';
    const offset = padding - gap / 2;

    return {
      position: 'absolute',
      top: '0',
      left: '0',
      right: '0',
      bottom: '0',
      pointerEvents: 'none',
      backgroundImage: `linear-gradient(to right, ${lineColor} 1px, transparent 1px), linear-gradient(to bottom, ${lineColor} 1px, transparent 1px)`,
      backgroundSize: `${slotWidth}px ${slotHeight}px, ${slotWidth}px ${slotHeight}px`,
      backgroundPosition: `${offset}px ${offset}px, ${offset}px ${offset}px`,
      zIndex: 1,
      opacity: 0.6
    };
  }

  getWidgetStyle(widget: WidgetConfig): any {
    const layout = this.layout();
    return {
      gridColumn: `${widget.position.x + 1} / span ${widget.position.w}`,
      gridRow: `${widget.position.y + 1} / span ${widget.position.h}`,
      backgroundColor: layout.colorScheme.background,
      border: `${layout.widgetBorder ?? 2}px solid ${layout.colorScheme.foreground}`,
      color: layout.colorScheme.text,
      padding: '8px',
      overflow: 'visible',
      cursor: 'grab',
      position: 'relative',
      userSelect: 'none'
    };
  }

  getGhostStyle(ghost: { id: string; position: WidgetPosition }): any {
    const layout = this.layout();
    const p = ghost.position;
    return {
      gridColumn: `${p.x + 1} / span ${p.w}`,
      gridRow: `${p.y + 1} / span ${p.h}`,
      backgroundColor: 'transparent',
      border: `${layout.widgetBorder ?? 2}px dashed ${layout.colorScheme.foreground}`,
      color: layout.colorScheme.text,
      padding: '8px',
      overflow: 'visible',
      cursor: 'grabbing',
      position: 'relative',
      userSelect: 'none',
      zIndex: 3,
      opacity: 0.7
    };
  }

  // Helper methods
  private generateId(): string {
    return `widget-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
  }

  private getDefaultConfig(type: WidgetType): any {
    switch (type) {
      case 'header':
        return { title: 'New Header', badges: [] };
      case 'markdown':
        return { content: '# Markdown Content' };
      case 'calendar':
        return { entityId: '', maxEvents: 7, headerFontSize: 15, eventFontSize: 12 };
      case 'weather':
      case 'weather-forecast':
        return { entityId: '', showForecast: type === 'weather-forecast' };
      case 'graph':
        return { entityId: '', period: '24h', label: '' };
      case 'todo':
        return { entityId: '' };
      case 'display':
        return { text: 'Display Text', fontSize: 18, color: '' };
      case 'app-icon':
        return { iconUrl: '', size: 48 };
      case 'image':
        return { imageUrl: '', fit: 'contain' };
      default:
        return {};
    }
  }
}
