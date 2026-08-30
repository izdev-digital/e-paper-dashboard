import type { HassEntity } from '../services/home-assistant.service';
import {
  DEFAULT_CALENDAR_EVENT_ITEMS,
  DEFAULT_FORECAST_FIELDS,
  DEFAULT_WEATHER_ITEMS,
  WidgetConfig,
  WidgetType,
} from './types';

export type WidgetCategory = 'content' | 'home-assistant' | 'chart' | 'asset' | 'system';

export interface WidgetDefinition {
  type: WidgetType;
  label: string;
  previewLabel?: string;
  icon: string;
  category: WidgetCategory;
  defaultSize: { w: number; h: number };
  minSize: { w: number; h: number };
  supportsTitle: boolean;
  createDefaultConfig: () => WidgetConfig['config'];
  acceptsEntity?: (entity: HassEntity) => boolean;
}

const graphDomains = new Set([
  'sensor', 'binary_sensor', 'input_number', 'number', 'counter', 'climate',
  'light', 'cover', 'fan', 'humidifier', 'water_heater', 'weather', 'person',
  'device_tracker', 'sun', 'zone',
]);

const domainIs = (...domains: string[]) => (entity: HassEntity) => domains.includes(entity.domain.toLowerCase());

export const WIDGET_DEFINITIONS: readonly WidgetDefinition[] = [
  {
    type: 'header', label: 'Header', icon: 'fa-heading', category: 'content',
    defaultSize: { w: 12, h: 1 }, minSize: { w: 3, h: 1 }, supportsTitle: false,
    createDefaultConfig: () => ({ title: 'New Header', badges: [] }),
    acceptsEntity: () => true,
  },
  {
    type: 'markdown', label: 'Markdown', icon: 'fa-align-left', category: 'content',
    defaultSize: { w: 4, h: 2 }, minSize: { w: 2, h: 1 }, supportsTitle: false,
    createDefaultConfig: () => ({ content: '# Markdown Content' }),
  },
  {
    type: 'calendar', label: 'Calendar', icon: 'fa-calendar', category: 'home-assistant',
    defaultSize: { w: 6, h: 3 }, minSize: { w: 3, h: 2 }, supportsTitle: true,
    createDefaultConfig: () => ({
      entityId: '', maxEvents: 7, items: DEFAULT_CALENDAR_EVENT_ITEMS.map(item => ({ ...item })),
    }),
    acceptsEntity: domainIs('calendar'),
  },
  {
    type: 'weather', label: 'Weather', icon: 'fa-cloud-sun', category: 'home-assistant',
    defaultSize: { w: 4, h: 2 }, minSize: { w: 2, h: 1 }, supportsTitle: true,
    createDefaultConfig: () => ({ entityId: '', items: DEFAULT_WEATHER_ITEMS.map(item => ({ ...item })) }),
    acceptsEntity: domainIs('weather'),
  },
  {
    type: 'weather-forecast', label: 'Weather Forecast', previewLabel: 'Forecast', icon: 'fa-cloud-sun-rain', category: 'home-assistant',
    defaultSize: { w: 6, h: 3 }, minSize: { w: 2, h: 1 }, supportsTitle: true,
    createDefaultConfig: () => ({ entityId: '', forecastMode: 'daily', visibleFields: [...DEFAULT_FORECAST_FIELDS] }),
    acceptsEntity: domainIs('weather'),
  },
  {
    type: 'graph', label: 'Graph', icon: 'fa-chart-line', category: 'chart',
    defaultSize: { w: 6, h: 3 }, minSize: { w: 3, h: 2 }, supportsTitle: true,
    createDefaultConfig: () => ({ series: [], period: '24h', plotType: 'line', lineWidth: 2 }),
    acceptsEntity: entity => {
      const numericState = entity.state != null && entity.state.trim() !== '' && Number.isFinite(Number(entity.state));
      return numericState || graphDomains.has(entity.domain.toLowerCase());
    },
  },
  {
    type: 'todo', label: 'Todo List', previewLabel: 'Tasks', icon: 'fa-list-check', category: 'home-assistant',
    defaultSize: { w: 5, h: 3 }, minSize: { w: 2, h: 2 }, supportsTitle: true,
    createDefaultConfig: () => ({ entityId: '' }),
    acceptsEntity: domainIs('todo'),
  },
  {
    type: 'rss-feed', label: 'RSS Feed', icon: 'fa-rss', category: 'home-assistant',
    defaultSize: { w: 4, h: 4 }, minSize: { w: 3, h: 3 }, supportsTitle: true,
    createDefaultConfig: () => ({ entityId: '', title: 'Topic of the day' }),
    acceptsEntity: domainIs('event', 'sensor'),
  },
  {
    type: 'app-icon', label: 'App Icon', icon: 'fa-rocket', category: 'asset',
    defaultSize: { w: 2, h: 2 }, minSize: { w: 1, h: 1 }, supportsTitle: false,
    createDefaultConfig: () => ({ size: 48 }),
  },
  {
    type: 'image', label: 'Image', icon: 'fa-image', category: 'asset',
    defaultSize: { w: 4, h: 3 }, minSize: { w: 2, h: 2 }, supportsTitle: true,
    createDefaultConfig: () => ({ imageUrl: '', fit: 'contain' }),
  },
  {
    type: 'version', label: 'Version', icon: 'fa-code-branch', category: 'system',
    defaultSize: { w: 2, h: 1 }, minSize: { w: 1, h: 1 }, supportsTitle: false,
    createDefaultConfig: () => ({}),
  },
  {
    type: 'ai-content', label: 'AI Content', icon: 'fa-wand-magic-sparkles', category: 'content',
    defaultSize: { w: 4, h: 3 }, minSize: { w: 2, h: 2 }, supportsTitle: true,
    createDefaultConfig: () => ({ prompt: '' }),
  },
];

const widgetDefinitionsByType = new Map(WIDGET_DEFINITIONS.map(definition => [definition.type, definition]));

export function getWidgetDefinition(type: WidgetType): WidgetDefinition {
  const definition = widgetDefinitionsByType.get(type);
  if (!definition) throw new Error(`Unknown widget type: ${type}`);
  return definition;
}

export function createDefaultWidgetConfig(type: WidgetType): WidgetConfig['config'] {
  return getWidgetDefinition(type).createDefaultConfig();
}

export function getWidgetDefaultSize(type: WidgetType, gridCols: number, gridRows: number) {
  const size = getWidgetDefinition(type).defaultSize;
  return { w: Math.min(size.w, gridCols), h: Math.min(size.h, gridRows) };
}

export function isEntityCompatible(type: WidgetType, entity: HassEntity): boolean {
  return getWidgetDefinition(type).acceptsEntity?.(entity) ?? true;
}
