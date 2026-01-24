




import { Component, OnInit, inject, signal, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DragDropModule, CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { DashboardService } from '../../services/dashboard.service';
import { ToastService } from '../../services/toast.service';
import { HomeAssistantService } from '../../services/home-assistant.service';
import { WidgetPreviewComponent } from '../widget-preview/widget-preview.component';
import { WidgetConfigComponent } from '../widget-config/widget-config.component';
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
  imports: [CommonModule, FormsModule, DragDropModule, WidgetPreviewComponent, WidgetConfigComponent],
  templateUrl: './dashboard-designer.component.html',
  styleUrls: ['./dashboard-designer.component.scss']
})
export class DashboardDesignerComponent implements OnInit {
    // Handle mousedown on toolbox widget to start drag with template preview
    onToolboxWidgetMouseDown(event: MouseEvent, widget: { type: WidgetType; label: string; icon: string }): void {
      event.preventDefault();
      const layout = this.layout();
      const canvas = document.querySelector('.dashboard-canvas') as HTMLElement;
      if (!canvas) return;

      // Create a preview element
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

      const onMouseMove = (e: MouseEvent) => movePreview(e);
      const onMouseUp = (e: MouseEvent) => {
        document.removeEventListener('mousemove', onMouseMove);
        document.removeEventListener('mouseup', onMouseUp);
        preview.remove();

        // Check if mouse is over the canvas
        const rect = canvas.getBoundingClientRect();
        if (
          e.clientX >= rect.left &&
          e.clientX <= rect.right &&
          e.clientY >= rect.top &&
          e.clientY <= rect.bottom
        ) {
          // Calculate grid position
          const cellWidth = rect.width / layout.gridCols;
          const cellHeight = rect.height / layout.gridRows;
          const x = Math.max(0, Math.min(layout.gridCols - 1, Math.floor((e.clientX - rect.left) / cellWidth)));
          const y = Math.max(0, Math.min(layout.gridRows - 1, Math.floor((e.clientY - rect.top) / cellHeight)));
          const newWidget: WidgetConfig = {
            id: this.generateId(),
            type: widget.type,
            position: {
              x,
              y,
              w: Math.min(4, layout.gridCols - x),
              h: Math.min(2, layout.gridRows - y)
            },
            config: this.getDefaultConfig(widget.type)
          };
          this.layout.update(l => ({ ...l, widgets: [...l.widgets, newWidget] }));
        }
      };
      document.addEventListener('mousemove', onMouseMove);
      document.addEventListener('mouseup', onMouseUp);
    }
  tabOrder: Array<'dashboard' | 'widgets' | 'properties'> = ['dashboard', 'widgets', 'properties'];

  switchTab(direction: 'left' | 'right') {
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
    // Prevent dropping into the toolbox
    toolboxDropPredicate() {
      return false;
    }
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dashboardService = inject(DashboardService);
  private readonly toastService = inject(ToastService);
  private readonly homeAssistantService = inject(HomeAssistantService);

  dashboardId: string = '';
  dashboard = signal<Dashboard | null>(null);
  layout = signal<DashboardLayout>({
    width: 800,
    height: 480,
    gridCols: 12,
    gridRows: 8,
    colorScheme: DEFAULT_COLOR_SCHEMES[0],
    widgets: []
  });

  colorSchemes = DEFAULT_COLOR_SCHEMES;
  availableWidgets: { type: WidgetType; label: string; icon: string }[] = [
    { type: 'header', label: 'Header', icon: 'fa-heading' },
    { type: 'markdown', label: 'Markdown', icon: 'fa-align-left' },
    { type: 'calendar', label: 'Calendar', icon: 'fa-calendar' },
    { type: 'weather', label: 'Weather', icon: 'fa-cloud-sun' },
    { type: 'weather-forecast', label: 'Weather Forecast', icon: 'fa-cloud-sun-rain' },
    { type: 'graph', label: 'Graph', icon: 'fa-chart-line' },
    { type: 'todo', label: 'Todo List', icon: 'fa-list-check' }
  ];

  selectedWidget = signal<WidgetConfig | null>(null);
  isLoading = signal(false);
  livePreviewLoading = signal(false);
  entityStates = signal<Record<string, HassEntityState>>({});
  activeTab = signal<'dashboard' | 'widgets' | 'properties'>('dashboard');
  
  // Drag state
  private isDragging = false;
  private isResizing = false;
  private dragStartPos = { x: 0, y: 0 };
  private dragStartWidget = { x: 0, y: 0, w: 0, h: 0 };
  private resizeDirection: 'e' | 's' | 'se' | null = null;

  constructor() {
    // Auto-refresh live preview whenever layout changes
    effect(() => {
      this.layout(); // Depend on layout changes
      if (this.dashboardId && !this.isLoading()) {
        this.refreshLivePreview();
      }
    });
  }

  ngOnInit(): void {
    this.dashboardId = this.route.snapshot.paramMap.get('id') || '';
    console.log('Dashboard ID:', this.dashboardId);
    if (this.dashboardId) {
      this.isLoading.set(true);
      console.log('Loading set to true, calling loadDashboard');
      this.loadDashboard();
    } else {
      this.toastService.show('No dashboard ID provided', 'error');
      this.isLoading.set(false);
    }
  }

  loadDashboard(): void {
    console.log('loadDashboard called for ID:', this.dashboardId);
    this.dashboardService.getDashboard(this.dashboardId).subscribe({
      next: (dashboard) => {
        console.log('Dashboard loaded:', dashboard);
        this.dashboard.set(dashboard);
        if (dashboard.layoutConfig) {
          try {
            const parsedLayout = JSON.parse(dashboard.layoutConfig);
            console.log('Parsed layout:', parsedLayout);
            // Ensure colorScheme is a full object, not just a reference
            const colorScheme = parsedLayout.colorScheme?.name 
              ? this.colorSchemes.find(cs => cs.name === parsedLayout.colorScheme.name) || DEFAULT_COLOR_SCHEMES[0]
              : DEFAULT_COLOR_SCHEMES[0];
            
            this.layout.set({
              width: parsedLayout.width || 800,
              height: parsedLayout.height || 480,
              gridCols: parsedLayout.gridCols || 12,
              gridRows: parsedLayout.gridRows || 8,
              colorScheme: colorScheme,
              widgets: parsedLayout.widgets || []
            });
            console.log('Layout set:', this.layout());
          } catch (e) {
            console.error('Failed to parse layout config', e);
          }
        }
        console.log('Setting isLoading to false');
        this.isLoading.set(false);
        console.log('isLoading is now:', this.isLoading());
        this.refreshLivePreview();
      },
      error: (err) => {
        console.error('Error loading dashboard:', err);
        this.toastService.show('Failed to load dashboard', 'error');
        this.isLoading.set(false);
        console.log('isLoading set to false after error:', this.isLoading());
      }
    });
  }

  addWidget(type: WidgetType): void {
    const newWidget: WidgetConfig = {
      id: this.generateId(),
      type: type,
      position: this.findEmptyPosition(),
      config: this.getDefaultConfig(type)
    };
    this.layout.update(layout => ({
      ...layout,
      widgets: [...layout.widgets, newWidget]
    }));
  }

  onToolboxDrop(event: CdkDragDrop<WidgetType>): void {
    const type = event.item.data as WidgetType;
    const canvas = document.querySelector('.dashboard-canvas') as HTMLElement | null;
    const layout = this.layout();

    if (!canvas || !event.dropPoint) {
      this.addWidget(type);
      return;
    }

    const rect = canvas.getBoundingClientRect();
    const cellWidth = rect.width / layout.gridCols;
    const cellHeight = rect.height / layout.gridRows;

    const relativeX = event.dropPoint.x - rect.left;
    const relativeY = event.dropPoint.y - rect.top;

    const x = Math.max(0, Math.min(layout.gridCols - 1, Math.floor(relativeX / cellWidth)));
    const y = Math.max(0, Math.min(layout.gridRows - 1, Math.floor(relativeY / cellHeight)));

    const newWidget: WidgetConfig = {
      id: this.generateId(),
      type,
      position: {
        x,
        y,
        w: Math.min(4, layout.gridCols - x),
        h: Math.min(2, layout.gridRows - y)
      },
      config: this.getDefaultConfig(type)
    };

    this.layout.update(l => ({
      ...l,
      widgets: [...l.widgets, newWidget]
    }));
  }

  private generateId(): string {
    return `widget-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
  }

  private findEmptyPosition(): WidgetPosition {
    // Simple algorithm: place at first available position
    return { x: 0, y: 0, w: 4, h: 2 };
  }

  private getDefaultConfig(type: WidgetType): any {
    switch (type) {
      case 'header':
        return { title: 'New Header', badges: [] };
      case 'markdown':
        return { content: '# Markdown Content' };
      case 'calendar':
        return { entityId: '', maxEvents: 5 };
      case 'weather':
      case 'weather-forecast':
        return { entityId: '', showForecast: type === 'weather-forecast' };
      case 'graph':
        return { entityId: '', period: '24h', label: '' };
      case 'todo':
        return { entityId: '' };
      default:
        return {};
    }
  }

  selectWidget(widget: WidgetConfig): void {
    this.selectedWidget.set(widget);
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

  saveDashboard(): void {
    if (!this.dashboard()) return;

    const layoutConfig = JSON.stringify(this.layout());
    this.dashboardService.updateDashboard(this.dashboardId, { layoutConfig }).subscribe({
      next: () => {
        this.toastService.show('Dashboard layout saved successfully', 'success');
      },
      error: () => {
        this.toastService.show('Failed to save dashboard layout', 'error');
      }
    });
  }

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

  refreshLivePreview(): void {
    if (!this.dashboardId) {
      console.warn('Dashboard ID missing, cannot load live data');
      return;
    }

    const ids = this.collectEntityIds();
    console.log('Collected entity IDs:', ids);
    
    if (ids.length === 0) {
      this.entityStates.set({});
      return;
    }

    this.livePreviewLoading.set(true);
    this.homeAssistantService.getEntityStates(this.dashboardId, ids).subscribe({
      next: (states) => {
        console.log('Received entity states:', states);
        const map: Record<string, HassEntityState> = {};
        states.forEach(s => { map[s.entityId] = s; });
        this.entityStates.set(map);
        this.livePreviewLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load live data from Home Assistant:', err);
        this.livePreviewLoading.set(false);
      }
    });
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

  resizeWidget(widget: WidgetConfig, deltaW: number, deltaH: number): void {
    const layout = this.layout();
    const newW = Math.max(1, Math.min(layout.gridCols - widget.position.x, widget.position.w + deltaW));
    const newH = Math.max(1, Math.min(layout.gridRows - widget.position.y, widget.position.h + deltaH));
    this.layout.update(l => ({
      ...l,
      widgets: l.widgets.map(w => 
        w.id === widget.id ? { ...w, position: { ...w.position, w: newW, h: newH } } : w
      )
    }));
  }

  onWidgetMouseDown(event: MouseEvent, widget: WidgetConfig): void {
    event.stopPropagation();
    this.selectedWidget.set(widget);
    
    // Check if clicking on resize handle
    const target = event.target as HTMLElement;
    if (target.classList.contains('resize-handle')) {
      this.startResize(event, widget, target.dataset['direction'] as 'e' | 's' | 'se');
    } else {
      this.startDrag(event, widget);
    }
  }

  private startDrag(event: MouseEvent, widget: WidgetConfig): void {
    let isDragging = true;
    this.dragStartPos = { x: event.clientX, y: event.clientY };
    this.dragStartWidget = { ...widget.position };
    
    const canvas = document.querySelector('.dashboard-canvas') as HTMLElement;
    const rect = canvas.getBoundingClientRect();
    const layout = this.layout();
    const cellWidth = rect.width / layout.gridCols;
    const cellHeight = rect.height / layout.gridRows;
    
    const onMouseMove = (e: MouseEvent) => {
      if (!isDragging) return;
      
      const deltaX = e.clientX - this.dragStartPos.x;
      const deltaY = e.clientY - this.dragStartPos.y;
      
      const gridDeltaX = Math.round(deltaX / cellWidth);
      const gridDeltaY = Math.round(deltaY / cellHeight);
      
      const newX = Math.max(0, Math.min(layout.gridCols - widget.position.w, this.dragStartWidget.x + gridDeltaX));
      const newY = Math.max(0, Math.min(layout.gridRows - widget.position.h, this.dragStartWidget.y + gridDeltaY));
      
      this.layout.update(l => ({
        ...l,
        widgets: l.widgets.map(w => 
          w.id === widget.id ? { ...w, position: { ...w.position, x: newX, y: newY } } : w
        )
      }));
    };
    
    const onMouseUp = () => {
      isDragging = false;
      document.removeEventListener('mousemove', onMouseMove);
      document.removeEventListener('mouseup', onMouseUp);
    };
    
    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);
  }

  private startResize(event: MouseEvent, widget: WidgetConfig, direction: 'e' | 's' | 'se'): void {
    event.stopPropagation();
    let isResizing = true;
    this.dragStartPos = { x: event.clientX, y: event.clientY };
    this.dragStartWidget = { ...widget.position };
    
    const canvas = document.querySelector('.dashboard-canvas') as HTMLElement;
    const rect = canvas.getBoundingClientRect();
    const layout = this.layout();
    const cellWidth = rect.width / layout.gridCols;
    const cellHeight = rect.height / layout.gridRows;
    
    const onMouseMove = (e: MouseEvent) => {
      if (!isResizing) return;
      
      const deltaX = e.clientX - this.dragStartPos.x;
      const deltaY = e.clientY - this.dragStartPos.y;
      
      const gridDeltaX = Math.round(deltaX / cellWidth);
      const gridDeltaY = Math.round(deltaY / cellHeight);
      
      let newW = widget.position.w;
      let newH = widget.position.h;
      
      if (direction === 'e' || direction === 'se') {
        newW = Math.max(1, Math.min(layout.gridCols - widget.position.x, this.dragStartWidget.w + gridDeltaX));
      }
      
      if (direction === 's' || direction === 'se') {
        newH = Math.max(1, Math.min(layout.gridRows - widget.position.y, this.dragStartWidget.h + gridDeltaY));
      }
      
      this.layout.update(l => ({
        ...l,
        widgets: l.widgets.map(w => 
          w.id === widget.id ? { ...w, position: { ...w.position, w: newW, h: newH } } : w
        )
      }));
    };
    
    const onMouseUp = () => {
      isResizing = false;
      document.removeEventListener('mousemove', onMouseMove);
      document.removeEventListener('mouseup', onMouseUp);
    };
    
    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);
  }

  getCanvasStyle(): any {
    const layout = this.layout();
    return {
      width: `${layout.width}px`,
      height: `${layout.height}px`,
      backgroundColor: layout.colorScheme.background,
      color: layout.colorScheme.text,
      display: 'grid',
      gridTemplateColumns: `repeat(${layout.gridCols}, 1fr)`,
      gridTemplateRows: `repeat(${layout.gridRows}, 1fr)`,
      gap: '4px',
      position: 'relative',
      boxSizing: 'content-box',
    };
  }

  getWidgetStyle(widget: WidgetConfig): any {
    const layout = this.layout();
    return {
      gridColumn: `${widget.position.x + 1} / span ${widget.position.w}`,
      gridRow: `${widget.position.y + 1} / span ${widget.position.h}`,
      backgroundColor: layout.colorScheme.background,
      border: `2px solid ${layout.colorScheme.foreground}`,
      color: layout.colorScheme.text,
      padding: '8px',
      overflow: 'visible',
      cursor: 'grab',
      position: 'relative',
      userSelect: 'none'
    };
  }

  goBack(): void {
    this.router.navigate(['/dashboards', this.dashboardId, 'edit']);
  }
}
