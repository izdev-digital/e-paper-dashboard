export interface User {
  id: string;
  username: string;
  nickname?: string;
  isSuperUser: boolean;
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
  layoutConfig?: string;
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
  layoutConfig?: string;
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
}

export interface HeaderConfig {
  title: string;
  badges?: BadgeConfig[];
  iconSize?: number;
  titleAlign?: 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right';
}

export interface BadgeConfig {
  entityId?: string;
  icon?: string;
}

export interface MarkdownConfig {
  content: string;
}

export interface CalendarConfig {
  entityId: string;
  maxEvents: number;
}

export type ForecastMode = 'hourly' | 'daily' | 'weekly';

export interface WeatherForecastConfig {
  entityId: string;
  forecastMode?: ForecastMode; // 'hourly', 'daily', 'weekly' - defaults to 'daily'
  maxItems?: number; // Max forecast items to display (auto if not specified)
}

export interface WeatherConfig {
  entityId: string;
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
