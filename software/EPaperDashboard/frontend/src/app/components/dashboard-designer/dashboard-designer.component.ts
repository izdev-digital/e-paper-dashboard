import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { DashboardService } from '../../services/dashboard.service';
import { ToastService } from '../../services/toast.service';
import { HomeAssistantService, HassEntity } from '../../services/home-assistant.service';
import { EntityStateService } from '../../services/entity-state.service';
import { TodoService, type TodoItem } from '../../services/todo.service';
import { CalendarService } from '../../services/calendar.service';
import { WeatherService } from '../../services/weather.service';
import { AiService } from '../../services/ai.service';
import { DialogService } from '../../services/dialog.service';
import { HasUnsavedChanges } from '../../guards/unsaved-changes.guard';
import { WidgetPreviewComponent } from '../widget-preview/widget-preview.component';
import { WidgetConfigComponent } from '../widget-config/widget-config.component';
import { RenderedPreviewModalComponent } from '../rendered-preview-modal/rendered-preview-modal.component';
import {
  Dashboard,
  DashboardLayout,
  DashboardOrientation,
  WidgetConfig,
  WidgetColorOverrides,
  WidgetType,
  ColorScheme,
  DEFAULT_COLOR_SCHEMES,
  WidgetPosition,
  HassEntityState,
  HeaderConfig,
  WeatherConfig,
  DEFAULT_WEATHER_ITEMS,
  DEFAULT_CALENDAR_EVENT_ITEMS,
  DEFAULT_FORECAST_FIELDS,
  DEFAULT_DASHBOARD_SIZE,
  DashboardSizePreset,
  DASHBOARD_SIZE_PRESETS,
  AiConfig,
  AiDataSummary,
} from '../../models/types';

@Component({
  selector: 'app-dashboard-designer',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, WidgetPreviewComponent, WidgetConfigComponent, RenderedPreviewModalComponent],
  templateUrl: './dashboard-designer.component.html',
  styleUrls: ['./dashboard-designer.component.scss']
})
export class DashboardDesignerComponent implements OnInit, HasUnsavedChanges {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly http = inject(HttpClient);
  private readonly dashboardService = inject(DashboardService);
  private readonly toastService = inject(ToastService);
  private readonly homeAssistantService = inject(HomeAssistantService);
  private readonly entityStateService = inject(EntityStateService);
  private readonly todoService = inject(TodoService);
  private readonly calendarService = inject(CalendarService);
  private readonly weatherService = inject(WeatherService);
  private readonly aiService = inject(AiService);
  private readonly dialogService = inject(DialogService);

  // Dashboard data
  dashboardId: string = '';
  dashboard = signal<Dashboard | null>(null);
  orientation = signal<DashboardOrientation>('Landscape');
  sizePresets: DashboardSizePreset[] = DASHBOARD_SIZE_PRESETS;
  selectedSizeIndex = 0;
  layout = signal<DashboardLayout>({
    width: DEFAULT_DASHBOARD_SIZE.width,
    height: DEFAULT_DASHBOARD_SIZE.height,
    gridCols: 12,
    gridRows: 8,
    colorScheme: DEFAULT_COLOR_SCHEMES[0],
    widgets: [],
    canvasPadding: 16,
    widgetGap: 4,
    widgetBorder: 3,
    widgetPadding: 4,
    titleFontSize: 16,
    textFontSize: 14,
    titleFontWeight: 700,
    textFontWeight: 400
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
    { type: 'rss-feed', label: 'RSS Feed', icon: 'fa-rss' },
    { type: 'app-icon', label: 'App Icon', icon: 'fa-rocket' },
    { type: 'image', label: 'Image', icon: 'fa-image' },
    { type: 'version', label: 'Version', icon: 'fa-code-branch' },
    { type: 'ai-content', label: 'AI Content', icon: 'fa-wand-magic-sparkles' }
  ];

  selectedWidget = signal<WidgetConfig | null>(null);
  ghost = signal<{ id: string; position: WidgetPosition } | null>(null);
  isLoading = signal(false);
  livePreviewLoading = signal(false);
  /** Becomes true after the first successful refreshLivePreview(). */
  livePreviewEverFetched = signal(false);
  entityStates = signal<Record<string, HassEntityState>>({});
  availableEntities = signal<HassEntity[]>([]);
  entitiesLoading = signal(false);
  activeTab = signal<'dashboard' | 'widgets' | 'properties'>('dashboard');
  todoItemsByEntityId = signal<Record<string, TodoItem[]>>({});
  calendarEventsByEntityId = signal<Record<string, any[]>>({});
  toolboxCollapsed = signal(false); // Widget toolbox left panel collapsed
  colorSchemeCollapsed = signal(false); // Color scheme section expanded by default
  colorOverridesCollapsed = signal(true); // Layout color overrides collapsed by default
  layoutCollapsed = signal(false); // Layout section expanded by default
  fontsCollapsed = signal(true); // Fonts section collapsed by default
  aiSectionCollapsed = signal(false); // AI section expanded by default when AI enabled
  widgetColorOverridesCollapsed = signal(true); // Widget color overrides collapsed by default
  showPreviewModal = signal(false);
  previewLoading = signal(false);
  previewError = signal('');
  previewImageUrl = signal('');
  /** ID of the widget whose internal layout editor is currently active (header or weather). */
  internalEditingWidgetId = signal<string | null>(null);

  // AI state
  isGeneratingAi = signal(false);
  aiGeneratedWidgets = signal<WidgetConfig[]>([]);
  aiLastGenerated = signal<string | null>(null);
  aiLastError = signal<string | null>(null);
  aiDataSummary = signal<AiDataSummary | null>(null);
  aiPromptTokenEstimate = signal<number | null>(null);

  // AI settings (editable in the AI tab)
  aiEnabled = signal(false);
  aiPrompt = signal('');
  aiLeadTimeMinutes = signal(5);
  aiConfigMode = signal<string>('None');

  // Dirty tracking
  private savedSnapshot = signal('');
  readonly isDirty = computed(() => {
    const current = this.computeCurrentSnapshot();
    return current !== this.savedSnapshot();
  });

  // Tab navigation
  tabOrder: Array<'dashboard' | 'widgets' | 'properties'> = ['dashboard', 'widgets', 'properties'];

  // Drag state
  private dragStartPos = { x: 0, y: 0 };
  private dragStartWidget = { x: 0, y: 0, w: 0, h: 0 };
  private previewObjectUrl: string | null = null;

  ngOnInit(): void {
    this.dashboardId = this.route.snapshot.paramMap.get('id') || '';
    if (this.dashboardId) {
      this.isLoading.set(true);
      this.loadDashboard();
    } else {
      this.toastService.show('No dashboard ID provided', 'error');
      this.isLoading.set(false);
    }
  }

  // Dashboard loading
  loadDashboard(): void {
    this.dashboardService.getDashboard(this.dashboardId).subscribe({
      next: (dashboard) => {
        this.dashboard.set(dashboard);
        const storedOrientation = dashboard.orientation || 'Landscape';
        const screenW = dashboard.screenWidth || DEFAULT_DASHBOARD_SIZE.width;
        const screenH = dashboard.screenHeight || DEFAULT_DASHBOARD_SIZE.height;

        const sizeIdx = this.sizePresets.findIndex(s => s.width === screenW && s.height === screenH);
        this.selectedSizeIndex = sizeIdx >= 0 ? sizeIdx : 0;

        if (dashboard.layoutConfig) {
          const w = dashboard.layoutConfig.width ?? screenW;
          const h = dashboard.layoutConfig.height ?? screenH;
          const effectiveOrientation: DashboardOrientation = h > w ? 'Portrait' : 'Landscape';
          this.orientation.set(effectiveOrientation);
          this.layout.set(this.normalizeLayout(dashboard.layoutConfig, effectiveOrientation, screenW, screenH));
        } else {
          this.orientation.set(storedOrientation);
          const isPortrait = storedOrientation === 'Portrait';
          this.layout.update(l => ({
            ...l,
            width: isPortrait ? screenH : screenW,
            height: isPortrait ? screenW : screenH,
            gridCols: isPortrait ? 8 : 12,
            gridRows: isPortrait ? 12 : 8,
          }));
        }
        this.loadAvailableEntities();
        this.loadAiGeneratedWidgets();

        // Initialize AI settings from dashboard
        this.aiPrompt.set(dashboard.aiPrompt ?? '');
        this.aiLeadTimeMinutes.set(dashboard.aiLeadTimeMinutes ?? 5);

        // Resolve effective AI config mode from backend (already resolved server-side)
        const effectiveMode = dashboard.effectiveAiConfigMode ?? 'None';
        this.aiConfigMode.set(effectiveMode);
        this.aiEnabled.set(effectiveMode !== 'None' && (dashboard.isAiEnabled ?? false));

        this.markAsPristine();
      },
      error: (err) => {
        this.toastService.show('Failed to load dashboard', 'error');
        this.isLoading.set(false);
      }
    });
  }

  loadAvailableEntities(): void {
    if (!this.dashboardId) {
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
        this.availableEntities.set([]);
        this.entitiesLoading.set(false);
        this.isLoading.set(false);
      }
    });
  }

  onWidgetSelect(widget: WidgetConfig): void {
    this.selectedWidget.set(widget);
    this.activeTab.set('properties');
  }

  loadAiGeneratedWidgets(): void {
    const d = this.dashboard();
    if (!d?.isAiEnabled) return;

    this.aiService.getGeneratedWidgets(this.dashboardId).subscribe({
      next: (result) => {
        this.aiGeneratedWidgets.set(result.widgets || []);
        this.aiLastGenerated.set(
          result.generatedAt ? new Date(result.generatedAt).toLocaleString() : null
        );
        this.aiLastError.set(result.lastError ?? null);
      },
      error: () => {}
    });
  }

  generateAiContent(): void {
    if (this.isGeneratingAi()) {
      return;
    }
    this.isGeneratingAi.set(true);

    // Send the prompt directly — no need to save first
    this.aiService.generateDashboard(this.dashboardId, this.aiPrompt()).subscribe({
      next: (result) => {
        this.aiGeneratedWidgets.set(result.widgets || []);
        this.aiLastGenerated.set(
          result.generatedAt ? new Date(result.generatedAt).toLocaleString() : null
        );
        this.aiLastError.set(null);
        this.aiDataSummary.set(result.dataSummary ?? null);
        this.aiPromptTokenEstimate.set(result.promptTokenEstimate ?? null);
        this.toastService.success(`AI generated ${result.widgets?.length ?? 0} widgets`);
        this.isGeneratingAi.set(false);
      },
      error: (err) => {
        const errorMsg = err.error?.message || err.error || 'AI generation failed';
        this.aiLastError.set(errorMsg);
        this.toastService.error(errorMsg);
        this.isGeneratingAi.set(false);
      }
    });
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


  onWidgetMouseDown(event: MouseEvent, widget: WidgetConfig): void {
    event.stopPropagation();
    // While a header widget is in internal-edit mode, swallow all outer drag/resize
    // interactions for that specific widget so the inner editor gets full mouse control.
    if (this.internalEditingWidgetId() === widget.id) {
      return;
    }
    // Selecting any other widget exits internal-edit mode on the previous one.
    if (this.internalEditingWidgetId() !== null) {
      this.internalEditingWidgetId.set(null);
    }
    this.selectedWidget.set(widget);
    this.activeTab.set('properties');
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

  toggleInternalEdit(widget: WidgetConfig): void {
    const current = this.internalEditingWidgetId();
    this.internalEditingWidgetId.set(current === widget.id ? null : widget.id);
  }

  onHeaderLayoutChanged(config: HeaderConfig, widgetId: string): void {
    this.layout.update(l => ({
      ...l,
      widgets: l.widgets.map(w =>
        w.id === widgetId ? { ...w, config: { ...config } } : w
      ),
    }));
    // Also keep the selectedWidget signal in sync so the config panel reflects changes.
    const updated = this.layout().widgets.find(w => w.id === widgetId);
    if (updated) this.selectedWidget.set(updated);
  }

  onWeatherLayoutChanged(config: WeatherConfig, widgetId: string): void {
    this.layout.update(l => ({
      ...l,
      widgets: l.widgets.map(w =>
        w.id === widgetId ? { ...w, config: { ...config } } : w
      ),
    }));
    const updated = this.layout().widgets.find(w => w.id === widgetId);
    if (updated) this.selectedWidget.set(updated);
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
        const updated = this.layout().widgets.find(w => w.id === g.id);
        if (updated) this.selectedWidget.set(updated);
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
        const updated = this.layout().widgets.find(w => w.id === g.id);
        if (updated) this.selectedWidget.set(updated);
      }
      this.ghost.set(null);
      document.removeEventListener('mousemove', onMouseMove);
      document.removeEventListener('mouseup', onMouseUp);
    };

    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);
  }


  saveDashboard(): void {
    if (!this.dashboard()) return;

    const layoutConfig = this.computePixelPositions(this.layout());
    const payload: any = { layoutConfig, orientation: this.orientation() };

    // Include AI settings in every save
    payload.isAiEnabled = this.aiEnabled();
    payload.aiPrompt = this.aiPrompt();
    payload.aiLeadTimeMinutes = this.aiLeadTimeMinutes();

    this.dashboardService.updateDashboard(this.dashboardId, payload).subscribe({
      next: () => {
        this.dashboard.update(d => d ? {
          ...d,
          isAiEnabled: payload.isAiEnabled,
          aiPrompt: payload.aiPrompt,
          aiLeadTimeMinutes: payload.aiLeadTimeMinutes,
        } : d);
        this.markAsPristine();
        this.toastService.show('Dashboard saved successfully', 'success');
      },
      error: (err) => {
        if (err.status === 401 || err.status === 403) {
          this.toastService.show('Authentication error. Please log in again.', 'error');
          this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } });
        } else {
          this.toastService.show('Failed to save dashboard', 'error');
        }
      }
    });
  }

  /**
   * Computes and stores pixel positions for every widget based on grid layout parameters.
   * These stored pixel values are used by the server-side rendering to position widgets
   * with absolute pixel coordinates, ensuring pixel-perfect rendering.
   */
  private computePixelPositions(layout: DashboardLayout): DashboardLayout {
    const padding = layout.canvasPadding ?? 0;
    const gap = layout.widgetGap ?? 0;
    const cols = Math.max(1, layout.gridCols);
    const rows = Math.max(1, layout.gridRows);
    const innerWidth = Math.max(0, layout.width - padding * 2 - gap * (cols - 1));
    const innerHeight = Math.max(0, layout.height - padding * 2 - gap * (rows - 1));
    const cellWidth = innerWidth / cols;
    const cellHeight = innerHeight / rows;

    const widgets = layout.widgets.map(widget => ({
      ...widget,
      position: {
        ...widget.position,
        pixelX: Math.round((padding + widget.position.x * (cellWidth + gap)) * 100) / 100,
        pixelY: Math.round((padding + widget.position.y * (cellHeight + gap)) * 100) / 100,
        pixelWidth: Math.round((widget.position.w * cellWidth + (widget.position.w - 1) * gap) * 100) / 100,
        pixelHeight: Math.round((widget.position.h * cellHeight + (widget.position.h - 1) * gap) * 100) / 100,
      }
    }));

    return { ...layout, widgets };
  }

  goBack(): void {
    this.router.navigate(['/dashboards', this.dashboardId, 'edit']);
  }

  hasUnsavedChanges(): boolean {
    return this.isDirty();
  }

  discardChanges(): void {
    const snapshot = JSON.parse(this.savedSnapshot());
    this.layout.set(snapshot.layout);
    this.orientation.set(snapshot.orientation);
    this.aiEnabled.set(snapshot.aiEnabled);
    this.aiPrompt.set(snapshot.aiPrompt);
    this.aiLeadTimeMinutes.set(snapshot.aiLeadTimeMinutes);
    this.selectedWidget.set(null);
  }

  private computeCurrentSnapshot(): string {
    const layout = this.layout();
    return JSON.stringify({
      layout,
      orientation: this.orientation(),
      aiEnabled: this.aiEnabled(),
      aiPrompt: this.aiPrompt(),
      aiLeadTimeMinutes: this.aiLeadTimeMinutes(),
    });
  }

  private markAsPristine(): void {
    this.savedSnapshot.set(this.computeCurrentSnapshot());
  }

  previewServerSideRendered(): void {
    if (!this.dashboard()) {
      this.toastService.show('No dashboard loaded', 'error');
      return;
    }

    this.openRenderedPreview();
  }

  openRenderedPreview(): void {
    this.showPreviewModal.set(true);
    this.previewLoading.set(true);
    this.previewError.set('');
    this.previewImageUrl.set('');

    if (this.previewObjectUrl) {
      try {
        URL.revokeObjectURL(this.previewObjectUrl);
      } catch (e) {}
      this.previewObjectUrl = null;
    }

    const url = `/api/dashboards/${this.dashboardId}/render-image?format=png`;

    this.http.get(url, {
      responseType: 'blob'
    }).subscribe({
      next: (blob) => {
        const imageUrl = URL.createObjectURL(blob);
        this.previewObjectUrl = imageUrl;
        this.previewImageUrl.set(imageUrl);
        this.previewLoading.set(false);
      },
      error: async (error) => {
        this.previewLoading.set(false);
        let errorMessage = 'Failed to load rendered preview';

        if (error.error instanceof Blob) {
          try {
            const text = await error.error.text();
            try {
              const json = JSON.parse(text);
              errorMessage = json.title || json.error || json.message || text;
            } catch (jsonError) {
              errorMessage = text || `HTTP Error ${error.status}`;
            }
          } catch (e) {
            errorMessage = `HTTP Error ${error.status}`;
          }
        } else if (error.status) {
          errorMessage = `HTTP Error ${error.status}`;
        }

        this.previewError.set(errorMessage);
        this.toastService.show(errorMessage, 'error');
      }
    });
  }

  updateColorScheme(scheme: ColorScheme): void {
    this.layout.update(layout => {
      // Strip widget color overrides that reference colors outside the new palette
      const widgets = layout.widgets.map(w => {
        if (!w.colorOverrides) return w;
        const cleaned: WidgetColorOverrides = {};
        let hasAny = false;
        for (const [key, val] of Object.entries(w.colorOverrides)) {
          if (val && scheme.palette.includes(val.toLowerCase())) {
            (cleaned as any)[key] = val;
            hasAny = true;
          }
        }
        // Also strip graph series colors outside the new palette
        let config = w.config;
        if (w.type === 'graph' && (config as any)?.series) {
          const series = ((config as any).series as any[]).map(s => {
            if (s.color && !scheme.palette.includes(s.color.toLowerCase())) {
              return { ...s, color: this.getDefaultGraphColor(scheme, 0) };
            }
            return s;
          });
          config = { ...(config as any), series };
        }
        return { ...w, colorOverrides: hasAny ? cleaned : undefined, config };
      });
      return { ...layout, colorScheme: scheme, widgets };
    });
    // Refresh selected widget reference if needed
    const sel = this.selectedWidget();
    if (sel) {
      const updated = this.layout().widgets.find(w => w.id === sel.id);
      if (updated) this.selectedWidget.set(updated);
    }
  }

  /** Reset the current color scheme to its base defaults and clear ALL widget color overrides. */
  resetAllColorOverrides(): void {
    const currentName = this.layout().colorScheme.name;
    const baseScheme = this.colorSchemes.find(cs => cs.name === currentName) || DEFAULT_COLOR_SCHEMES[0];
    this.layout.update(layout => ({
      ...layout,
      colorScheme: { ...baseScheme },
      widgets: layout.widgets.map(w => {
        const { colorOverrides, ...rest } = w;
        return rest as WidgetConfig;
      })
    }));
    // Refresh selected widget reference
    const sel = this.selectedWidget();
    if (sel) {
      const updated = this.layout().widgets.find(w => w.id === sel.id);
      if (updated) this.selectedWidget.set(updated);
    }
  }

  /** Reset only the layout-level color overrides back to the base scheme defaults. */
  resetLayoutColorOverrides(): void {
    const currentName = this.layout().colorScheme.name;
    const baseScheme = this.colorSchemes.find(cs => cs.name === currentName) || DEFAULT_COLOR_SCHEMES[0];
    this.layout.update(layout => ({ ...layout, colorScheme: { ...baseScheme } }));
  }

  setOrientation(newOrientation: DashboardOrientation): void {
    if (newOrientation === this.orientation()) return;
    this.orientation.set(newOrientation);

    const size = this.sizePresets[this.selectedSizeIndex] ?? DEFAULT_DASHBOARD_SIZE;
    const isPortrait = newOrientation === 'Portrait';

    this.layout.update(l => ({
      ...l,
      width: isPortrait ? size.height : size.width,
      height: isPortrait ? size.width : size.height,
      gridCols: l.gridRows,
      gridRows: l.gridCols,
    }));
  }

  onSizeChange(): void {
    const size = this.sizePresets[this.selectedSizeIndex] ?? DEFAULT_DASHBOARD_SIZE;
    const isPortrait = this.orientation() === 'Portrait';
    this.layout.update(l => ({
      ...l,
      width: isPortrait ? size.height : size.width,
      height: isPortrait ? size.width : size.height,
    }));
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

  updateWidgetPadding(padding: number): void {
    this.layout.update(layout => ({ ...layout, widgetPadding: padding }));
  }

  updateTitleFontSize(fontSize: number | string): void {
    const size = typeof fontSize === 'string' ? parseInt(fontSize, 10) : fontSize;
    this.layout.update(layout => ({ ...layout, titleFontSize: size }));
  }

  updateTextFontSize(fontSize: number | string): void {
    const size = typeof fontSize === 'string' ? parseInt(fontSize, 10) : fontSize;
    this.layout.update(layout => ({ ...layout, textFontSize: size }));
  }

  updateTitleFontWeight(fontWeight: number | string): void {
    const weight = typeof fontWeight === 'string' ? parseInt(fontWeight, 10) : fontWeight;
    this.layout.update(layout => ({ ...layout, titleFontWeight: weight }));
  }

  updateTextFontWeight(fontWeight: number | string): void {
    const weight = typeof fontWeight === 'string' ? parseInt(fontWeight, 10) : fontWeight;
    this.layout.update(layout => ({ ...layout, textFontWeight: weight }));
  }

  updateCanvasBackgroundColor(color: string): void {
    this.layout.update(layout => ({
      ...layout,
      colorScheme: { ...layout.colorScheme, canvasBackgroundColor: color }
    }));
  }

  updateWidgetBorderColor(color: string): void {
    this.layout.update(layout => ({
      ...layout,
      colorScheme: { ...layout.colorScheme, widgetBorderColor: color }
    }));
  }

  updateWidgetTitleTextColor(color: string): void {
    this.layout.update(layout => ({
      ...layout,
      colorScheme: { ...layout.colorScheme, widgetTitleTextColor: color }
    }));
  }

  updateWidgetTextColor(color: string): void {
    this.layout.update(layout => ({
      ...layout,
      colorScheme: { ...layout.colorScheme, widgetTextColor: color }
    }));
  }

  updateIconColor(color: string): void {
    this.layout.update(layout => ({
      ...layout,
      colorScheme: { ...layout.colorScheme, iconColor: color }
    }));
  }

  updateWidgetBackgroundColor(color: string): void {
    this.layout.update(layout => ({
      ...layout,
      colorScheme: { ...layout.colorScheme, widgetBackgroundColor: color }
    }));
  }

  updateWidgetColorOverride(widget: WidgetConfig, colorProperty: keyof WidgetColorOverrides, value: string): void {
    const updatedWidget: WidgetConfig = {
      ...widget,
      colorOverrides: {
        ...widget.colorOverrides,
        [colorProperty]: value || undefined
      }
    };

    this.layout.update(layout => ({
      ...layout,
      widgets: layout.widgets.map(w => w.id === widget.id ? updatedWidget : w)
    }));

    this.selectedWidget.set(updatedWidget);
  }

  clearWidgetColorOverride(widget: WidgetConfig, colorProperty: keyof WidgetColorOverrides): void {
    const overrides = { ...widget.colorOverrides };
    delete overrides[colorProperty];

    const updatedWidget: WidgetConfig = {
      ...widget,
      colorOverrides: Object.keys(overrides).length > 0 ? overrides : undefined
    };

    this.layout.update(layout => ({
      ...layout,
      widgets: layout.widgets.map(w => w.id === widget.id ? updatedWidget : w)
    }));

    this.selectedWidget.set(updatedWidget);
  }

  onWidgetConfigChanged(widget: WidgetConfig): void {
    // Update the layout signal with the new widget configuration
    this.layout.update(layout => ({
      ...layout,
      widgets: layout.widgets.map(w => w.id === widget.id ? { ...widget } : w)
    }));
    
    // Update selected widget if it's currently selected
    if (this.selectedWidget()?.id === widget.id) {
      this.selectedWidget.set({ ...widget });
    }
  }

  /** Find the base (default) scheme that matches the current scheme by name. */
  private getBaseScheme(): ColorScheme {
    const currentName = this.layout().colorScheme.name;
    return this.colorSchemes.find(cs => cs.name === currentName) || DEFAULT_COLOR_SCHEMES[0];
  }

  // Helper method to check if a specific layout color property is overridden vs. the base scheme
  isLayoutColorOverridden(prop: 'canvasBackgroundColor' | 'widgetBackgroundColor' | 'widgetBorderColor' | 'widgetTitleTextColor' | 'widgetTextColor' | 'iconColor'): boolean {
    return this.layout().colorScheme[prop] !== this.getBaseScheme()[prop];
  }

  // Helper method to check if layout has any color overrides vs. the selected base scheme
  hasLayoutColorOverrides(): boolean {
    return this.isLayoutColorOverridden('canvasBackgroundColor') ||
      this.isLayoutColorOverridden('widgetBackgroundColor') ||
      this.isLayoutColorOverridden('widgetBorderColor') ||
      this.isLayoutColorOverridden('widgetTitleTextColor') ||
      this.isLayoutColorOverridden('widgetTextColor') ||
      this.isLayoutColorOverridden('iconColor');
  }

  // Helper method to check if a specific widget color override differs from the current scheme
  isWidgetColorOverridden(widget: WidgetConfig | null, prop: keyof WidgetColorOverrides): boolean {
    if (!widget?.colorOverrides) return false;
    const val = widget.colorOverrides[prop];
    if (val == null || val === '') return false;
    const scheme = this.layout().colorScheme;
    const schemeDefaults: Record<string, string> = {
      widgetBackgroundColor: scheme.widgetBackgroundColor,
      widgetBorderColor: scheme.widgetBorderColor,
      widgetTitleTextColor: scheme.widgetTitleTextColor,
      widgetTextColor: scheme.widgetTextColor,
      iconColor: scheme.iconColor,
    };
    return val !== schemeDefaults[prop];
  }

  // Helper method to check if widget has any color overrides that actually differ from the current scheme
  hasWidgetColorOverrides(widget: WidgetConfig | null): boolean {
    if (!widget?.colorOverrides) return false;
    return this.isWidgetColorOverridden(widget, 'widgetBackgroundColor') ||
      this.isWidgetColorOverridden(widget, 'widgetBorderColor') ||
      this.isWidgetColorOverridden(widget, 'widgetTitleTextColor') ||
      this.isWidgetColorOverridden(widget, 'widgetTextColor') ||
      this.isWidgetColorOverridden(widget, 'iconColor');
  }


  /** Get a sensible default color for a graph series from the palette. */
  private getDefaultGraphColor(scheme: ColorScheme, index: number): string {
    const chartColors = scheme.palette.filter(
      c => c !== scheme.canvasBackgroundColor && c !== scheme.widgetBackgroundColor && c !== scheme.background
    );
    if (chartColors.length > 0) return chartColors[index % chartColors.length];
    return scheme.palette[index % scheme.palette.length] || '#000000';
  }

  getColorName(color: string): string {
    const colorMap: Record<string, string> = {
      '#000000': 'Black',
      '#ffffff': 'White',
      '#ff0000': 'Red',
      '#ffff00': 'Yellow'
    };
    return colorMap[color.toLowerCase()] || color;
  }

  refreshLivePreview(): void {
    if (!this.dashboardId) {
      return;
    }

    const ids = this.collectEntityIds();

    if (ids.length === 0) {
      this.entityStates.set({});
      this.livePreviewEverFetched.set(true);
      return;
    }

    this.livePreviewLoading.set(true);
    this.entityStateService.getEntityStates(this.dashboardId, ids).subscribe({
      next: (states) => {
        const map: Record<string, HassEntityState> = {};
        states.forEach(s => { map[s.entityId] = s; });
        this.entityStates.set(map);
        this.livePreviewEverFetched.set(true);

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
          this.todoService.getTodoItems(this.dashboardId, entityId).subscribe({
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
        this.livePreviewLoading.set(false);
        const msg = err?.error?.error || err?.message || 'Failed to fetch entity data';
        this.toastService.show(msg, 'error');
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
      this.fetchWeatherForecasts();
      return;
    }

    let completed = 0;
    const calendarMap: Record<string, any[]> = {};
    calendarEntityIds.forEach(entityId => {
      this.calendarService.getCalendarEvents(this.dashboardId, entityId).subscribe({
        next: (events: any[]) => {
          calendarMap[entityId] = events || [];
          completed++;
          if (completed === calendarEntityIds.length) {
            this.calendarEventsByEntityId.set(calendarMap);
            this.fetchWeatherForecasts();
          }
        },
        error: () => {
          calendarMap[entityId] = [];
          completed++;
          if (completed === calendarEntityIds.length) {
            this.calendarEventsByEntityId.set(calendarMap);
            this.fetchWeatherForecasts();
          }
        }
      });
    });
  }

  private fetchWeatherForecasts() {
    const weatherEntityIds = this.layout().widgets
      .filter(w => w.type === 'weather-forecast' && (w.config as any).entityId)
      .map(w => (w.config as any).entityId)
      .filter((id, idx, arr) => !!id && arr.indexOf(id) === idx);

    if (weatherEntityIds.length === 0) {
      this.livePreviewLoading.set(false);
      return;
    }

    let completed = 0;
    const weatherMap: Record<string, any> = {};
    weatherEntityIds.forEach(entityId => {
      const forecastMode = this.layout().widgets
        .find(w => w.type === 'weather-forecast' && (w.config as any).entityId === entityId)
        ?.config as any;
      const forecastType = this.mapForecastModeToServiceType(forecastMode?.forecastMode || 'daily');
      
      this.weatherService.getWeatherForecast(this.dashboardId, entityId, forecastType).subscribe({
        next: (forecast: any) => {
          // Merge forecast data into entity state attributes
          const state = this.entityStates()[entityId];
          if (state && state.attributes) {
            state.attributes['forecast'] = forecast?.forecast || [];
          }
          completed++;
          if (completed === weatherEntityIds.length) {
            this.livePreviewLoading.set(false);
          }
        },
        error: () => {
          completed++;
          if (completed === weatherEntityIds.length) {
            this.livePreviewLoading.set(false);
          }
        }
      });
    });
  }

  private mapForecastModeToServiceType(mode: string): string {
    switch (mode) {
      case 'hourly': return 'hourly';
      case 'weekly': return 'daily';
      case 'daily':
      default: return 'daily';
    }
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
        case 'todo':
        case 'rss-feed':
          if ((widget.config as any).entityId) ids.add((widget.config as any).entityId);
          break;
        case 'graph': {
          const graphCfg = widget.config as any;
          if (Array.isArray(graphCfg?.series)) {
            graphCfg.series.forEach((s: any) => {
              if (s?.entityId) ids.add(s.entityId);
            });
          }
          break;
        }
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

  getCanvasStyle(): any {
    const layout = this.layout();
    return {
      width: `${layout.width}px`,
      minWidth: `${layout.width}px`,
      'min-width': `${layout.width}px`,
      height: `${layout.height}px`,
      minHeight: `${layout.height}px`,
      'min-height': `${layout.height}px`,
      backgroundColor: layout.colorScheme.canvasBackgroundColor || layout.colorScheme.background,
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
    const borderColor = widget.colorOverrides?.widgetBorderColor || layout.colorScheme.widgetBorderColor || layout.colorScheme.foreground;
    const backgroundColor = widget.colorOverrides?.widgetBackgroundColor || layout.colorScheme.widgetBackgroundColor || layout.colorScheme.background;
    return {
      gridColumn: `${widget.position.x + 1} / span ${widget.position.w}`,
      gridRow: `${widget.position.y + 1} / span ${widget.position.h}`,
      backgroundColor: backgroundColor,
      border: `${layout.widgetBorder ?? 2}px solid ${borderColor}`,
      color: layout.colorScheme.text,
      padding: `${layout.widgetPadding ?? 0}px`,
      boxSizing: 'border-box',
      overflow: 'visible',
      cursor: 'grab',
      position: 'relative',
      userSelect: 'none'
    };
  }

  getSelectionOverlayStyle(): any {
    const widget = this.selectedWidget();
    if (!widget) return { display: 'none' };
    const layout = this.layout();
    const padding = layout.canvasPadding ?? 0;
    const gap     = layout.widgetGap ?? 0;
    const cols    = layout.gridCols;
    const rows    = layout.gridRows;

    // Use actual inner dimensions (clientWidth excludes the canvas CSS border)
    const canvasEl = document.querySelector('.dashboard-canvas') as HTMLElement | null;
    const totalW = canvasEl ? canvasEl.clientWidth  : layout.width;
    const totalH = canvasEl ? canvasEl.clientHeight : layout.height;

    const cellW = (totalW - 2 * padding - gap * (cols - 1)) / cols;
    const cellH = (totalH - 2 * padding - gap * (rows - 1)) / rows;

    // During drag/resize the live position is in ghost(), not layout()
    const ghost = this.ghost();
    const p = (ghost?.id === widget.id ? ghost.position : null) ?? widget.position;

    const left   = padding + p.x * (cellW + gap);
    const top    = padding + p.y * (cellH + gap);
    const width  = p.w * cellW + (p.w - 1) * gap;
    const height = p.h * cellH + (p.h - 1) * gap;

    const isInternal = this.internalEditingWidgetId() === widget.id;

    return {
      position: 'absolute',
      left:   `${left}px`,
      top:    `${top}px`,
      width:  `${width}px`,
      height: `${height}px`,
      boxSizing: 'border-box',
      pointerEvents: isInternal ? 'none' : 'auto',
      zIndex: 100,
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

  private generateId(): string {
    return `widget-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
  }

  compareColorSchemes(a: ColorScheme, b: ColorScheme): boolean {
    return a?.name === b?.name;
  }

  private normalizeLayout(parsedLayout: any, orientation?: DashboardOrientation, screenWidth?: number, screenHeight?: number): DashboardLayout {
    const isPortrait = orientation === 'Portrait';
    const sw = screenWidth || DEFAULT_DASHBOARD_SIZE.width;
    const sh = screenHeight || DEFAULT_DASHBOARD_SIZE.height;
    const baseLayout: DashboardLayout = {
      width: isPortrait ? sh : sw,
      height: isPortrait ? sw : sh,
      gridCols: isPortrait ? 8 : 12,
      gridRows: isPortrait ? 12 : 8,
      colorScheme: DEFAULT_COLOR_SCHEMES[0],
      widgets: [],
      canvasPadding: 16,
      widgetGap: 4,
      widgetBorder: 3,
      widgetPadding: 4,
      titleFontSize: 16,
      textFontSize: 14,
      titleFontWeight: 700,
      textFontWeight: 400
    };

    const baseScheme = parsedLayout?.colorScheme?.name
      ? this.colorSchemes.find(cs => cs.name === parsedLayout.colorScheme.name) || DEFAULT_COLOR_SCHEMES[0]
      : DEFAULT_COLOR_SCHEMES[0];

    const mergedScheme: ColorScheme = {
      ...baseScheme,
      ...(parsedLayout?.colorScheme ?? {}),
      name: baseScheme.name,
      palette: Array.isArray(parsedLayout?.colorScheme?.palette) && parsedLayout.colorScheme.palette.length > 0
        ? parsedLayout.colorScheme.palette
        : baseScheme.palette
    };

    const widgets = Array.isArray(parsedLayout?.widgets)
      ? parsedLayout.widgets.map((widget: any) => this.normalizeWidget(widget))
      : [];

    return {
      width: typeof parsedLayout?.width === 'number' ? parsedLayout.width : baseLayout.width,
      height: typeof parsedLayout?.height === 'number' ? parsedLayout.height : baseLayout.height,
      gridCols: typeof parsedLayout?.gridCols === 'number' ? parsedLayout.gridCols : baseLayout.gridCols,
      gridRows: typeof parsedLayout?.gridRows === 'number' ? parsedLayout.gridRows : baseLayout.gridRows,
      colorScheme: mergedScheme,
      widgets: widgets,
      canvasPadding: typeof parsedLayout?.canvasPadding === 'number' ? parsedLayout.canvasPadding : baseLayout.canvasPadding,
      widgetGap: typeof parsedLayout?.widgetGap === 'number' ? parsedLayout.widgetGap : baseLayout.widgetGap,
      widgetBorder: typeof parsedLayout?.widgetBorder === 'number' ? parsedLayout.widgetBorder : baseLayout.widgetBorder,
      widgetPadding: typeof parsedLayout?.widgetPadding === 'number' ? parsedLayout.widgetPadding : baseLayout.widgetPadding,
      titleFontSize: typeof parsedLayout?.titleFontSize === 'number' ? parsedLayout.titleFontSize : baseLayout.titleFontSize,
      textFontSize: typeof parsedLayout?.textFontSize === 'number' ? parsedLayout.textFontSize : baseLayout.textFontSize,
      titleFontWeight: typeof parsedLayout?.titleFontWeight === 'number' ? parsedLayout.titleFontWeight : baseLayout.titleFontWeight,
      textFontWeight: typeof parsedLayout?.textFontWeight === 'number' ? parsedLayout.textFontWeight : baseLayout.textFontWeight
    };
  }

  private normalizeWidget(widget: any): WidgetConfig {
    const type = widget?.type as WidgetType;
    const defaultConfig = this.getDefaultConfig(type);
    const config = {
      ...defaultConfig,
      ...(widget?.config ?? {})
    };

    const position = {
      x: typeof widget?.position?.x === 'number' ? widget.position.x : 0,
      y: typeof widget?.position?.y === 'number' ? widget.position.y : 0,
      w: typeof widget?.position?.w === 'number' ? widget.position.w : 2,
      h: typeof widget?.position?.h === 'number' ? widget.position.h : 2
    };

    return {
      id: widget?.id || this.generateId(),
      type: type,
      position,
      config,
      colorOverrides: widget?.colorOverrides,
      titleOverride: widget?.titleOverride
    } as WidgetConfig;
  }

  // AI settings methods
  onAiEnabledChange(enabled: boolean): void {
    if (enabled && this.aiConfigMode() === 'None') {
      this.aiEnabled.set(false);
      return;
    }
    this.aiEnabled.set(enabled);
    if (!enabled) {
      this.aiGeneratedWidgets.set([]);
      this.aiLastGenerated.set(null);
    }
  }

  onAiPromptChange(value: string): void {
    this.aiPrompt.set(value);
  }

  onAiLeadTimeChange(value: string): void {
    this.aiLeadTimeMinutes.set(parseInt(value, 10) || 5);
  }



  private getDefaultConfig(type: WidgetType): any {
    switch (type) {
      case 'header':
        return { title: 'New Header', badges: [] };
      case 'markdown':
        return { content: '# Markdown Content' };
      case 'calendar':
        return { entityId: '', maxEvents: 7, items: [...DEFAULT_CALENDAR_EVENT_ITEMS] };
      case 'weather':
        return { entityId: '', items: [...DEFAULT_WEATHER_ITEMS] };
      case 'weather-forecast':
        return {
          entityId: '',
          forecastMode: 'daily',
          visibleFields: [...DEFAULT_FORECAST_FIELDS]
        };
      case 'graph':
        return { series: [], period: '24h', plotType: 'line', lineWidth: 2 };
      case 'todo':
        return { entityId: '' };
      case 'rss-feed':
        return { entityId: '', title: 'Topic of the day' };
      case 'app-icon':
        return { size: 48 };
      case 'image':
        return { imageUrl: '', fit: 'contain' };
      case 'version':
        return {};
      case 'ai-content':
        return { prompt: '' };
      default:
        return {};
    }
  }
}
