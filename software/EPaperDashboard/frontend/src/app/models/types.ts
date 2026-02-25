export type DeploymentMode = 'addon' | 'host' | 'standalone';

export interface User {
  id: string;
  username: string;
  nickname?: string;
  isSuperUser: boolean;
  deploymentMode?: DeploymentMode;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface RegisterRequest {
  username: string;
  password: string;
}

export interface Dashboard {
  id: string;
  userId: string;
  name: string;
  description: string;
  apiKey: string;
  hasAccessToken: boolean;
  host?: string;
  path?: string;
  updateTimes?: string[];
  layoutConfig?: DashboardLayout;
  renderingMode?: 'Custom' | 'HomeAssistant';
}

export interface CreateDashboardRequest {
  name: string;
  description?: string;
}

export interface UpdateDashboardRequest {
  name?: string;
  description?: string;
  accessToken?: string;
  clearAccessToken?: boolean;
  host?: string;
  path?: string;
  updateTimes?: string[];
  layoutConfig?: DashboardLayout;
  renderingMode?: 'Custom' | 'HomeAssistant';
}


// Dashboard Designer Types
export type WidgetType =
  | 'header'
  | 'markdown'
  | 'calendar'
  | 'weather'
  | 'weather-forecast'
  | 'graph'
  | 'todo'
  | 'app-icon'
  | 'image'
  | 'version'
  | 'rss-feed';

export interface WidgetPosition {
  x: number;
  y: number;
  w: number;
  h: number;
}


export interface AppIconConfig {
  size?: number;
}

export interface ImageConfig {
  imageUrl: string;
  fit?: 'contain' | 'cover' | 'fill';
}

export interface VersionConfig {
  // No configuration needed - displays the app version
}

export interface WidgetColorOverrides {
  widgetBackgroundColor?: string;
  widgetBorderColor?: string;
  widgetTitleTextColor?: string;
  widgetTextColor?: string;
  iconColor?: string;
}

export interface WidgetConfig {
  id: string;
  type: WidgetType;
  position: WidgetPosition;
  config:
  | HeaderConfig
  | MarkdownConfig
  | CalendarConfig
  | WeatherConfig
  | WeatherForecastConfig
  | GraphConfig
  | TodoConfig
  | AppIconConfig
  | ImageConfig
  | VersionConfig
  | RssFeedConfig;
  colorOverrides?: WidgetColorOverrides;
  titleOverride?: string;
  showTitle?: boolean;
}

export interface HeaderConfig {
  title: string;
  badges?: BadgeConfig[];
  iconSize?: number;
  iconPosition?: 'left' | 'right';
  /** Title element position/size as % of the header widget bounds (set by the visual editor) */
  titleX?: number;
  titleY?: number;
  titleW?: number;
  titleH?: number;
  /** Layout editor settings – stored with the widget so they persist across sessions */
  snapStep?: number;      // 0 = off, or 1 / 2 / 5 (%)
  showGuides?: boolean;
}

export interface BadgeConfig {
  entityId?: string;
  icon?: string;
  /** Position and size as % of the header widget bounds (set by the visual editor) */
  x?: number;
  y?: number;
  w?: number;
  h?: number;
}

export interface MarkdownConfig {
  content: string;
}

export type CalendarEventItemType = 'datetime' | 'title' | 'location' | 'description';

export interface CalendarEventItemConfig {
  type: CalendarEventItemType;
  visible?: boolean;
  /** Font Awesome icon class (e.g. 'fa-clock'). Each item type has a default. */
  icon?: string;
  /** Position and size as % of the event entry bounds (set by the visual editor) */
  x?: number;
  y?: number;
  w?: number;
  h?: number;
}

/** Default icon for each calendar event item type */
export function defaultCalendarEventItemIcon(type: CalendarEventItemType): string {
  switch (type) {
    case 'datetime':    return 'fa-clock';
    case 'title':       return 'fa-heading';
    case 'location':    return 'fa-location-dot';
    case 'description': return 'fa-align-left';
    default:            return '';
  }
}

export const DEFAULT_CALENDAR_EVENT_ITEMS: CalendarEventItemConfig[] = [
  { type: 'datetime',    visible: true,  x: 0, y: 0,  w: 100, h: 50 },
  { type: 'title',       visible: true,  x: 0, y: 50, w: 100, h: 50 },
  { type: 'location',    visible: false, x: 0, y: 50, w: 100, h: 25, icon: 'fa-location-dot' },
  { type: 'description', visible: false, x: 0, y: 75, w: 100, h: 25, icon: 'fa-align-left' },
];

export interface CalendarConfig {
  entityId: string;
  maxEvents: number;
  items?: CalendarEventItemConfig[];
  /** Gap between event entries in px (default 0) */
  eventGap?: number;
  /** Fixed height per event entry in px. When unset, events share available space equally. */
  eventHeight?: number;
  /** Layout editor settings – stored with the widget so they persist across sessions */
  snapStep?: number;      // 0 = off, or 1 / 2 / 5 (%)
  showGuides?: boolean;
}

export type ForecastMode = 'hourly' | 'daily' | 'weekly';

export interface WeatherForecastConfig {
  entityId: string;
  forecastMode?: ForecastMode; // 'hourly', 'daily', 'weekly' - defaults to 'daily'
  maxItems?: number; // Max forecast items to display (auto if not specified)
}

export type WeatherItemType = 'title' | 'temperature' | 'condition' | 'pressure' | 'attribute';

export interface WeatherItemConfig {
  type: WeatherItemType;
  visible?: boolean;
  /** Font Awesome icon class (e.g. 'fa-temperature-half'). Each item type has a default. */
  icon?: string;
  /** For 'attribute' type – which HA attribute key to display (e.g. 'humidity', 'wind_speed') */
  attributeKey?: string;
  /** Optional display label (e.g. "Humidity") */
  label?: string;
  /** Position and size as % of the weather widget bounds (set by the visual editor) */
  x?: number;
  y?: number;
  w?: number;
  h?: number;
}

/** Default icon for each weather item type */
export function defaultWeatherItemIcon(type: WeatherItemType, attributeKey?: string): string {
  switch (type) {
    case 'temperature': return 'fa-temperature-half';
    case 'condition':   return 'fa-cloud-sun';
    case 'pressure':    return 'fa-gauge';
    case 'attribute':
      switch (attributeKey) {
        case 'humidity':       return 'fa-droplet';
        case 'wind_speed':     return 'fa-wind';
        case 'wind_bearing':   return 'fa-compass';
        case 'visibility':     return 'fa-eye';
        case 'dew_point':      return 'fa-temperature-low';
        case 'cloud_coverage': return 'fa-cloud';
        case 'uv_index':       return 'fa-sun';
        default:               return 'fa-circle-info';
      }
    default: return '';
  }
}

export const DEFAULT_WEATHER_ITEMS: WeatherItemConfig[] = [
  { type: 'title',       visible: true, x: 0,  y: 0,  w: 100, h: 20 },
  { type: 'temperature', visible: true, x: 0,  y: 22, w: 50,  h: 20, icon: 'fa-temperature-half' },
  { type: 'condition',   visible: true, x: 50, y: 22, w: 50,  h: 20, icon: 'fa-cloud-sun' },
  { type: 'pressure',    visible: true, x: 0,  y: 44, w: 50,  h: 20, icon: 'fa-gauge' },
  { type: 'attribute',   visible: true, x: 50, y: 44, w: 50,  h: 20, attributeKey: 'humidity', label: 'Humidity', icon: 'fa-droplet' },
];

export interface WeatherConfig {
  entityId: string;
  items?: WeatherItemConfig[];
  /** Layout editor settings – stored with the widget so they persist across sessions */
  snapStep?: number;      // 0 = off, or 1 / 2 / 5 (%)
  showGuides?: boolean;
}

export interface GraphSeriesConfig {
  entityId: string;
  label?: string;
  color?: string;
}

export interface GraphConfig {
  series: GraphSeriesConfig[];
  period: '1h' | '6h' | '24h' | '7d' | '30d';
  plotType?: 'line' | 'bar';
  lineWidth?: number;
  barWidth?: number;
}

export interface TodoConfig {
  entityId: string;
  showCompleted?: boolean;
  maxItems?: number;
  pendingIcon?: string;
  completedIcon?: string;
}

export interface RssFeedConfig {
  entityId: string;
  title?: string;
}

export interface DashboardLayout {
  width: number;
  height: number;
  gridCols: number;
  gridRows: number;
  colorScheme: ColorScheme;
  widgets: WidgetConfig[];
  canvasPadding?: number;
  widgetGap?: number;
  widgetBorder?: number;
  widgetPadding?: number;
  titleFontSize?: number;
  textFontSize?: number;
  titleFontWeight?: number;
  textFontWeight?: number;
}

export interface ColorScheme {
  name: string;
  variant?: 'light' | 'dark';
  palette: string[]; // Allowed colors only (e.g., ['#000000', '#ff0000', '#ffffff'])
  background: string;
  canvasBackgroundColor: string;
  widgetBackgroundColor: string;
  widgetBorderColor: string;
  widgetTitleTextColor: string;
  widgetTextColor: string;
  iconColor: string;
  foreground: string;
  accent: string;
  text: string;
}

export interface HassEntityState {
  entityId: string;
  state: string;
  attributes?: Record<string, any>;
}

export const DEFAULT_COLOR_SCHEMES: ColorScheme[] = [
  {
    name: 'E-Paper Light (Black/Red/White)',
    palette: ['#000000', '#ff0000', '#ffffff'],
    background: '#ffffff',
    canvasBackgroundColor: '#ffffff',
    widgetBackgroundColor: '#ffffff',
    widgetBorderColor: '#000000',
    widgetTitleTextColor: '#000000',
    widgetTextColor: '#000000',
    iconColor: '#ff0000',
    foreground: '#000000',
    accent: '#ff0000',
    text: '#000000'
  },
  {
    name: 'E-Paper Dark (Black/Red/White)',
    variant: 'dark',
    palette: ['#000000', '#ff0000', '#ffffff'],
    background: '#000000',
    canvasBackgroundColor: '#000000',
    widgetBackgroundColor: '#000000',
    widgetBorderColor: '#ffffff',
    widgetTitleTextColor: '#ffffff',
    widgetTextColor: '#ffffff',
    iconColor: '#ff0000',
    foreground: '#ffffff',
    accent: '#ff0000',
    text: '#ffffff'
  },
  {
    name: 'E-Paper Light (Black/White)',
    variant: 'light',
    palette: ['#000000', '#ffffff'],
    background: '#ffffff',
    canvasBackgroundColor: '#ffffff',
    widgetBackgroundColor: '#ffffff',
    widgetBorderColor: '#000000',
    widgetTitleTextColor: '#000000',
    widgetTextColor: '#000000',
    iconColor: '#000000',
    foreground: '#000000',
    accent: '#000000',
    text: '#000000'
  },
  {
    name: 'E-Paper Dark (Black/White)',
    variant: 'dark',
    palette: ['#000000', '#ffffff'],
    background: '#000000',
    canvasBackgroundColor: '#000000',
    widgetBackgroundColor: '#000000',
    widgetBorderColor: '#ffffff',
    widgetTitleTextColor: '#ffffff',
    widgetTextColor: '#ffffff',
    iconColor: '#ffffff',
    foreground: '#ffffff',
    accent: '#ffffff',
    text: '#ffffff'
  },
  {
    name: 'E-Paper Light (Yellow/Black/White)',
    variant: 'light',
    palette: ['#000000', '#ffff00', '#ffffff'],
    background: '#ffffff',
    canvasBackgroundColor: '#ffffff',
    widgetBackgroundColor: '#ffffff',
    widgetBorderColor: '#000000',
    widgetTitleTextColor: '#000000',
    widgetTextColor: '#000000',
    iconColor: '#ffff00',
    foreground: '#000000',
    accent: '#ffff00',
    text: '#000000'
  },
  {
    name: 'E-Paper Dark (Yellow/Black/White)',
    variant: 'dark',
    palette: ['#000000', '#ffff00', '#ffffff'],
    background: '#000000',
    canvasBackgroundColor: '#000000',
    widgetBackgroundColor: '#000000',
    widgetBorderColor: '#ffffff',
    widgetTitleTextColor: '#ffffff',
    widgetTextColor: '#ffffff',
    iconColor: '#ffff00',
    foreground: '#ffffff',
    accent: '#ffff00',
    text: '#ffffff'
  }
];
