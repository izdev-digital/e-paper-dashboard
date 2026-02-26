import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  WidgetConfig,
  Dashboard,
  HeaderConfig,
  MarkdownConfig,
  CalendarConfig,
  CalendarEventItemConfig,
  DEFAULT_CALENDAR_EVENT_ITEMS,
  defaultCalendarEventItemIcon,
  WeatherConfig,
  WeatherItemConfig,
  DEFAULT_WEATHER_ITEMS,
  defaultWeatherItemIcon,
  WeatherForecastConfig,
  ForecastField,
  ALL_FORECAST_FIELDS,
  DEFAULT_FORECAST_FIELDS,
  FORECAST_FIELD_LABELS,
  GraphConfig,
  TodoConfig,
  AppIconConfig,
  RssFeedConfig,
  ColorScheme
} from '../../models/types';
import { HttpClient } from '@angular/common/http';
import { HomeAssistantService, HassEntity } from '../../services/home-assistant.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-widget-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './widget-config.component.html',
  styleUrls: ['./widget-config.component.scss']
})
export class WidgetConfigComponent implements OnChanges {
  trackByBadgeLabel(index: number, badge: any) {
    return index;
  }

  trackByEntityId(index: number, entity: any) {
    return entity.entity_id || index;
  }

  private readonly homeAssistantService = inject(HomeAssistantService);
  private readonly authService = inject(AuthService);
  private readonly http = inject(HttpClient);

  imageUploading = false;
  imageUploadError: string | null = null;
  showImageUrlInput = false;
  imageUrlValue = '';

  formatEntityLabel(entity: any): string {
    const base = entity?.friendly_name || entity?.entity_id || 'Unknown';
    const details: string[] = [];

    const domain = entity?.domain;
    const deviceClass = entity?.device_class;
    const unit = entity?.unit_of_measurement;

    if (domain) details.push(domain);
    if (deviceClass) details.push(deviceClass);
    if (unit) details.push(unit);

    if (details.length === 0) {
      return base;
    }

    return `${base} (${details.join(', ')})`;
  }


  @Input() widget!: WidgetConfig;
  @Input() dashboard!: Dashboard | null;
  @Input() availableEntities: HassEntity[] = [];
  @Input() entitiesLoading: boolean = false;
  @Input() colorScheme?: ColorScheme;
  @Output() widgetChanged = new EventEmitter<WidgetConfig>();

  entities = signal<any[]>([]);
  loadingEntities = signal(false);
  entityFetchError: string | null = null;

  get headerConfig(): HeaderConfig {
    return this.widget.config as HeaderConfig;
  }

  get markdownConfig(): MarkdownConfig {
    return this.widget.config as MarkdownConfig;
  }

  get calendarConfig(): CalendarConfig {
    return this.widget.config as CalendarConfig;
  }

  get weatherConfig(): WeatherConfig {
    return this.widget.config as WeatherConfig;
  }

  get weatherForecastConfig(): WeatherForecastConfig {
    return this.widget.config as WeatherForecastConfig;
  }

  get graphConfig(): GraphConfig {
    return this.widget.config as GraphConfig;
  }

  get todoConfig(): TodoConfig {
    return this.widget.config as TodoConfig;
  }

  get appIconConfig(): AppIconConfig {
    return this.widget.config as AppIconConfig;
  }

  get rssFeedConfig(): RssFeedConfig {
    return this.widget.config as RssFeedConfig;
  }

  onPropertyChanged(): void {
    this.widgetChanged.emit(this.widget);
  }

  // ---- Weather Forecast field visibility helpers ----
  readonly allForecastFields = ALL_FORECAST_FIELDS;

  isForecastFieldVisible(field: ForecastField): boolean {
    const fields = this.weatherForecastConfig.visibleFields ?? DEFAULT_FORECAST_FIELDS;
    return fields.includes(field);
  }

  toggleForecastField(field: ForecastField): void {
    const current = this.weatherForecastConfig.visibleFields ?? [...DEFAULT_FORECAST_FIELDS];
    const idx = current.indexOf(field);
    if (idx >= 0) {
      current.splice(idx, 1);
    } else {
      // Insert in canonical order
      const ordered = ALL_FORECAST_FIELDS.filter(f => current.includes(f) || f === field);
      current.length = 0;
      current.push(...ordered);
    }
    this.weatherForecastConfig.visibleFields = current;
    this.onPropertyChanged();
  }

  getForecastFieldLabel(field: ForecastField): string {
    return FORECAST_FIELD_LABELS[field];
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['availableEntities']) {
      const mapped = this.availableEntities.map(e => ({
        entity_id: e.entityId,
        friendly_name: e.friendlyName,
        domain: e.domain,
        device_class: e.deviceClass ?? undefined,
        unit_of_measurement: e.unitOfMeasurement ?? undefined,
        icon: e.icon ?? undefined,
        state: e.state ?? undefined,
        supported_features: e.supportedFeatures ?? undefined
      }));
      this.entities.set(mapped);
      this.loadingEntities.set(false);
      this.entityFetchError = null;
    }

    if (changes['entitiesLoading']) {
      this.loadingEntities.set(this.entitiesLoading);
    }

    if (changes['dashboard'] && this.availableEntities.length === 0) {
      // In addon mode, HA connection is auto-managed (no host/token needed per dashboard)
      const canLoad = this.authService.isAddonMode()
        ? !!this.dashboard
        : (this.dashboard?.hasAccessToken && this.dashboard?.host);
      if (canLoad) {
        this.loadEntities();
      }
    }
  }

  getFilteredEntities(): any[] {
    const allEntities = this.entities();

    const getDomain = (entity: any): string => {
      if (entity?.domain) {
        return String(entity.domain).toLowerCase();
      }
      const id = entity?.entity_id?.toLowerCase() || '';
      return id.includes('.') ? id.split('.')[0] : id;
    };

    // Filter entities based on widget type
    switch (this.widget.type) {
      case 'todo':
        return allEntities.filter(e => getDomain(e) === 'todo');
      case 'calendar':
        return allEntities.filter(e => getDomain(e) === 'calendar');
      case 'weather':
      case 'weather-forecast':
        return allEntities.filter(e => getDomain(e) === 'weather');
      case 'rss-feed':
        // Feedreader creates event entities with names like event.feed_name_latest_feed
        // Show all event entities and let user select the appropriate feedreader entity
        return allEntities.filter(e => getDomain(e) === 'event');
      case 'graph':
        // Graph can work with any entity that has numeric state values
        // Include sensors, counters, numbers, climate (temperature), light (brightness), etc.
        const filtered = allEntities.filter(e => {
          const domain = getDomain(e);
          return (
            domain === 'sensor' ||
            domain === 'binary_sensor' ||
            domain === 'input_number' ||
            domain === 'number' ||
            domain === 'counter' ||
            domain === 'climate' ||
            domain === 'light' ||
            domain === 'cover' ||
            domain === 'fan' ||
            domain === 'humidifier' ||
            domain === 'water_heater' ||
            domain === 'weather' ||
            domain === 'person' ||
            domain === 'device_tracker' ||
            domain === 'sun' ||
            domain === 'zone'
          );
        });
        return filtered;
      default:
        return allEntities;
    }
  }

  loadEntities(): void {
    if (!this.dashboard) return;
    this.loadingEntities.set(true);
    this.entityFetchError = null;
    this.homeAssistantService.getEntities(this.dashboard.id).subscribe({
      next: (entities) => {
        const mapped = entities.map(e => ({
          entity_id: e.entityId,
          friendly_name: e.friendlyName,
          domain: e.domain,
          device_class: e.deviceClass ?? undefined,
          unit_of_measurement: e.unitOfMeasurement ?? undefined,
          icon: e.icon ?? undefined,
          state: e.state ?? undefined,
          supported_features: e.supportedFeatures ?? undefined
        }));
        this.entities.set(mapped);
        this.loadingEntities.set(false);
      },
      error: (err) => {
        this.entityFetchError = (err?.error?.message || err?.message || err?.toString() || 'Unknown error');
        this.entities.set([]);
        this.loadingEntities.set(false);
      }
    });
  }

  addGraphSeries(): void {
    if (!this.graphConfig.series) {
      this.graphConfig.series = [];
    }
    this.graphConfig.series.push({ entityId: '', label: '', color: this.getDefaultGraphColor(this.graphConfig.series.length) });
  }

  removeGraphSeries(index: number): void {
    if (this.graphConfig.series) {
      this.graphConfig.series.splice(index, 1);
    }
  }

  trackByGraphSeries(index: number, series: any) {
    return index;
  }

  private getDefaultGraphColor(index: number): string {
    // Prefer palette colors (excluding background colors) for graph series
    if (this.colorScheme?.palette?.length) {
      const bg = this.colorScheme.canvasBackgroundColor || this.colorScheme.background;
      const wbg = this.colorScheme.widgetBackgroundColor;
      const chartColors = this.colorScheme.palette.filter(c => c !== bg && c !== wbg);
      if (chartColors.length > 0) return chartColors[index % chartColors.length];
    }
    const colors = ['#ff0000', '#00ff00', '#0000ff', '#ffff00', '#ff00ff', '#00ffff'];
    return colors[index % colors.length];
  }

  getColorName(hex: string): string {
    if (!hex) return 'Auto';
    
    const colorMap: Record<string, string> = {
      '#000000': 'Black',
      '#ffffff': 'White',
      '#ff0000': 'Red',
      '#00ff00': 'Green',
      '#0000ff': 'Blue',
      '#ffff00': 'Yellow',
      '#ff00ff': 'Magenta',
      '#00ffff': 'Cyan',
      '#808080': 'Gray',
      '#ffa500': 'Orange',
      '#800080': 'Purple',
      '#ffc0cb': 'Pink',
      '#a52a2a': 'Brown',
      '#808000': 'Olive',
      '#800000': 'Maroon',
      '#008000': 'Dark Green',
      '#000080': 'Navy'
    };

    const lowerHex = hex.toLowerCase();
    return colorMap[lowerHex] || hex;
  }

  onHeaderConfigChanged(updatedConfig: HeaderConfig): void {
    // Merge the updated header config (with new positions) back into the widget
    this.widget = { ...this.widget, config: updatedConfig };
    this.widgetChanged.emit(this.widget);
  }

  addBadge(): void {
    const config = this.headerConfig;
    const newBadge = { entityId: undefined, icon: undefined };
    if (config.badges) {
      config.badges.push(newBadge);
    } else {
      config.badges = [newBadge];
    }
    this.onPropertyChanged();
  }

  removeBadge(index: number): void {
    const config = this.headerConfig;
    if (config.badges) {
      config.badges.splice(index, 1);
      this.onPropertyChanged();
    }
  }

  getBadgeEntityLabel(badge: any): string {
    if (!badge.entityId) return '';
    const entity = this.entities().find((e: any) => e.entity_id === badge.entityId);
    return entity ? this.formatEntityLabel(entity) : badge.entityId;
  }

  // ─── Weather item helpers ─────────────────────────────────────────────────

  getWeatherItems(): WeatherItemConfig[] {
    const cfg = this.weatherConfig;
    if (!cfg.items || cfg.items.length === 0) {
      cfg.items = [...DEFAULT_WEATHER_ITEMS];
    }
    return cfg.items;
  }

  trackByWeatherItem(index: number, item: WeatherItemConfig): string {
    return item.type + '-' + (item.attributeKey ?? index);
  }

  toggleWeatherItemVisibility(index: number): void {
    const items = this.getWeatherItems();
    items[index] = { ...items[index], visible: items[index].visible === false };
    this.onPropertyChanged();
  }

  getWeatherNonTitleItems(): WeatherItemConfig[] {
    return this.getWeatherItems().filter(i => i.type !== 'title');
  }

  toggleWeatherNonTitleItemVisibility(filteredIndex: number): void {
    const allItems = this.getWeatherItems();
    const nonTitleItems = allItems.filter(i => i.type !== 'title');
    const item = nonTitleItems[filteredIndex];
    const realIndex = allItems.indexOf(item);
    if (realIndex >= 0) {
      allItems[realIndex] = { ...allItems[realIndex], visible: allItems[realIndex].visible === false };
      this.onPropertyChanged();
    }
  }

  toggleWeatherTitleVisibility(): void {
    const items = this.getWeatherItems();
    const idx = items.findIndex(i => i.type === 'title');
    if (idx >= 0) {
      items[idx] = { ...items[idx], visible: items[idx].visible === false };
    }
  }

  toggleTitleVisibility(): void {
    this.widget.showTitle = !(this.widget.showTitle !== false);
    // Keep weather title item visibility in sync
    if (this.widget.type === 'weather') {
      const items = this.getWeatherItems();
      const idx = items.findIndex(i => i.type === 'title');
      if (idx >= 0) {
        items[idx] = { ...items[idx], visible: this.widget.showTitle };
      }
    }
    this.onPropertyChanged();
  }

  getWeatherItemLabel(item: WeatherItemConfig): string {
    switch (item.type) {
      case 'title': return 'Title';
      case 'temperature': return 'Temperature';
      case 'condition': return 'Condition';
      case 'pressure': return 'Pressure';
      case 'attribute': return item.label || item.attributeKey || 'Attribute';
      default: return item.type;
    }
  }

  getDefaultWeatherItemIcon(item: WeatherItemConfig): string {
    return defaultWeatherItemIcon(item.type, item.attributeKey);
  }

  // ─── Calendar event item helpers ──────────────────────────────────────────

  getCalendarEventItems(): CalendarEventItemConfig[] {
    const cfg = this.calendarConfig;
    if (!cfg.items || cfg.items.length === 0) {
      cfg.items = [...DEFAULT_CALENDAR_EVENT_ITEMS];
    }
    return cfg.items;
  }

  trackByCalendarEventItem(index: number, item: CalendarEventItemConfig): string {
    return item.type;
  }

  toggleCalendarEventItemVisibility(index: number): void {
    const items = this.getCalendarEventItems();
    items[index] = { ...items[index], visible: items[index].visible === false };
    this.onPropertyChanged();
  }

  isCalendarItemAlwaysVisible(item: CalendarEventItemConfig): boolean {
    return item.type === 'datetime' || item.type === 'title';
  }

  getCalendarEventItemLabel(item: CalendarEventItemConfig): string {
    switch (item.type) {
      case 'datetime':    return 'Date / Time';
      case 'title':       return 'Event Title';
      case 'location':    return 'Location';
      case 'description': return 'Description';
      default: return item.type;
    }
  }

  getDefaultCalendarEventItemIcon(item: CalendarEventItemConfig): string {
    return defaultCalendarEventItemIcon(item.type);
  }

  // ─── Image upload helpers ─────────────────────────────────────────────────

  onImageFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !this.dashboard) return;

    this.imageUploading = true;
    this.imageUploadError = null;

    const formData = new FormData();
    formData.append('file', file);

    this.http.post<{ imageUrl: string }>(
      `/api/dashboards/${this.dashboard.id}/images`,
      formData
    ).subscribe({
      next: (res) => {
        (this.widget.config as any).imageUrl = res.imageUrl;
        this.imageUploading = false;
        this.onPropertyChanged();
      },
      error: (err) => {
        this.imageUploadError = err?.error?.message || 'Upload failed.';
        this.imageUploading = false;
      }
    });

    // Reset input so re-selecting the same file triggers change
    input.value = '';
  }

  clearUploadedImage(): void {
    const config = this.widget.config as any;
    const imageUrl: string = config.imageUrl || '';

    // If it's an uploaded image, delete it from the server
    if (imageUrl.startsWith('/api/dashboards/') && imageUrl.includes('/images/')) {
      this.http.delete(imageUrl).subscribe({
        error: () => { /* ignore delete errors */ }
      });
    }

    config.imageUrl = '';
    this.onPropertyChanged();
  }

  uploadImageFromUrl(): void {
    if (!this.imageUrlValue || !this.dashboard) return;

    // Delete previous uploaded image if any
    const config = this.widget.config as any;
    const prevUrl: string = config.imageUrl || '';
    if (prevUrl.startsWith('/api/dashboards/') && prevUrl.includes('/images/')) {
      this.http.delete(prevUrl).subscribe({ error: () => {} });
    }

    this.imageUploading = true;
    this.imageUploadError = null;

    this.http.post<{ imageUrl: string }>(
      `/api/dashboards/${this.dashboard.id}/images/from-url`,
      { url: this.imageUrlValue }
    ).subscribe({
      next: (res) => {
        config.imageUrl = res.imageUrl;
        this.imageUploading = false;
        this.showImageUrlInput = false;
        this.imageUrlValue = '';
        this.onPropertyChanged();
      },
      error: (err) => {
        this.imageUploadError = err?.error?.message || 'Failed to fetch image from URL.';
        this.imageUploading = false;
      }
    });
  }
}
