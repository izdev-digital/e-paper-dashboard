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
  | 'display'
  | 'app-icon'
  | 'image'
  | 'version';

export interface WidgetPosition {
  x: number;
  y: number;
  w: number;
  h: number;
}


export interface DisplayConfig {
  text: string;
  color?: string;
}

export interface AppIconConfig {
  iconUrl?: string; // fallback to default app icon if not set
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
    | GraphConfig
    | TodoConfig
    | DisplayConfig
    | AppIconConfig
    | ImageConfig
    | VersionConfig;
  colorOverrides?: WidgetColorOverrides;
}

export interface HeaderConfig {
  title: string;
  badges?: BadgeConfig[];
  iconUrl?: string;
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

export interface WeatherConfig {
  entityId: string;
  showForecast: boolean;
}

export interface GraphConfig {
  entityId: string;
  period: '1h' | '6h' | '24h' | '7d' | '30d';
  label?: string;
}

export interface TodoConfig {
  entityId: string;
  showCompleted?: boolean;
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
}

export interface ColorScheme {
  name: string;
  variant?: 'light' | 'dark';
  background: string;
  canvasBackgroundColor: string;
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
    variant: 'light',
    background: '#ffffff',
    canvasBackgroundColor: '#ffffff',
    widgetBorderColor: '#000000',
    widgetTitleTextColor: '#000000',
    widgetTextColor: '#333333',
    iconColor: '#ff0000',
    foreground: '#000000',
    accent: '#ff0000',
    text: '#000000'
  },
  {
    name: 'E-Paper Dark (Black/Red/White)',
    variant: 'dark',
    background: '#1a1a1a',
    canvasBackgroundColor: '#1a1a1a',
    widgetBorderColor: '#ffffff',
    widgetTitleTextColor: '#ffffff',
    widgetTextColor: '#cccccc',
    iconColor: '#ff6666',
    foreground: '#ffffff',
    accent: '#ff6666',
    text: '#ffffff'
  },
  {
    name: 'E-Paper Light (Black/White)',
    variant: 'light',
    background: '#ffffff',
    canvasBackgroundColor: '#ffffff',
    widgetBorderColor: '#000000',
    widgetTitleTextColor: '#000000',
    widgetTextColor: '#333333',
    iconColor: '#666666',
    foreground: '#000000',
    accent: '#666666',
    text: '#000000'
  },
  {
    name: 'E-Paper Dark (Black/White)',
    variant: 'dark',
    background: '#1a1a1a',
    canvasBackgroundColor: '#1a1a1a',
    widgetBorderColor: '#ffffff',
    widgetTitleTextColor: '#ffffff',
    widgetTextColor: '#cccccc',
    iconColor: '#999999',
    foreground: '#ffffff',
    accent: '#999999',
    text: '#ffffff'
  },
  {
    name: 'E-Paper Light (Yellow/Black/White)',
    variant: 'light',
    background: '#ffffff',
    canvasBackgroundColor: '#ffffff',
    widgetBorderColor: '#000000',
    widgetTitleTextColor: '#000000',
    widgetTextColor: '#333333',
    iconColor: '#ffcc00',
    foreground: '#000000',
    accent: '#ffcc00',
    text: '#000000'
  },
  {
    name: 'E-Paper Dark (Yellow/Black/White)',
    variant: 'dark',
    background: '#1a1a1a',
    canvasBackgroundColor: '#1a1a1a',
    widgetBorderColor: '#ffffff',
    widgetTitleTextColor: '#ffffff',
    widgetTextColor: '#cccccc',
    iconColor: '#ffdd33',
    foreground: '#ffffff',
    accent: '#ffdd33',
    text: '#ffffff'
  }
];
