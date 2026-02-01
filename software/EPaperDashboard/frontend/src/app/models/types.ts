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
  | 'image';

export interface WidgetPosition {
  x: number;
  y: number;
  w: number;
  h: number;
}


export interface DisplayConfig {
  text: string;
  fontSize?: number;
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
    | ImageConfig;
}

export interface HeaderConfig {
  title: string;
  badges?: BadgeConfig[];
  fontSize?: number;
}

export interface BadgeConfig {
  label: string;
  entityId?: string;
  icon?: string;
  // Transient flags used by the editor UI
  _confirmed?: boolean;
  _editing?: boolean;
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
}

export interface ColorScheme {
  name: string;
  background: string;
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
    name: 'E-Paper (Black/Red/White)',
    background: '#ffffff',
    foreground: '#000000',
    accent: '#ff0000',
    text: '#000000'
  },
  {
    name: 'E-Paper (Black/White)',
    background: '#ffffff',
    foreground: '#000000',
    accent: '#666666',
    text: '#000000'
  },
  {
    name: 'E-Paper (Yellow/Black/White)',
    background: '#ffffff',
    foreground: '#000000',
    accent: '#ffcc00',
    text: '#000000'
  }
];
