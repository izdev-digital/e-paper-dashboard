import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { DragDropModule, CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { DashboardService } from '../../services/dashboard.service';
import { ToastService } from '../../services/toast.service';
import { WidgetPreviewComponent } from '../widget-preview/widget-preview.component';
import { WidgetConfigComponent } from '../widget-config/widget-config.component';
import {
  Dashboard,
  DashboardLayout,
  WidgetConfig,
  WidgetType,
  ColorScheme,
  DEFAULT_COLOR_SCHEMES,
  WidgetPosition
} from '../../models/types';

@Component({
  selector: 'app-dashboard-designer',
  standalone: true,
  imports: [CommonModule, FormsModule, DragDropModule, WidgetPreviewComponent, WidgetConfigComponent],
  templateUrl: './dashboard-designer.component.html',
  styleUrls: ['./dashboard-designer.component.scss']
})
export class DashboardDesignerComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly dashboardService = inject(DashboardService);
  private readonly toastService = inject(ToastService);

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
  
  // Drag state
  private isDragging = false;
  private isResizing = false;
  private dragStartPos = { x: 0, y: 0 };
  private dragStartWidget = { x: 0, y: 0, w: 0, h: 0 };
  private resizeDirection: 'e' | 's' | 'se' | null = null;

  ngOnInit(): void {
    this.dashboardId = this.route.snapshot.paramMap.get('id') || '';
    if (this.dashboardId) {
      this.isLoading.set(true);
      this.loadDashboard();
    }
  }

  loadDashboard(): void {
    this.dashboardService.getDashboard(this.dashboardId).subscribe({
      next: (dashboard) => {
        this.dashboard.set(dashboard);
        if (dashboard.layoutConfig) {
          try {
            this.layout.set(JSON.parse(dashboard.layoutConfig));
          } catch (e) {
            console.error('Failed to parse layout config', e);
          }
        }
        this.isLoading.set(false);
      },
      error: (err) => {
        this.toastService.show('Failed to load dashboard', 'error');
        this.isLoading.set(false);
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

  getGridStyle(): any {
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
      position: 'relative'
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
