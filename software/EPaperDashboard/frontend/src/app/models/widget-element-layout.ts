import {
  DEFAULT_WEATHER_ITEMS,
  HeaderConfig,
  WeatherConfig,
  WidgetConfig,
} from './types';
import {
  EditableWidgetElementGeometry,
  RenderRectangle,
} from '../services/dashboard-render-preview.service';

export interface EditableElementChange {
  element: EditableWidgetElementGeometry;
  position: RenderRectangle;
}

export function applyEditableElementChange(
  widget: WidgetConfig,
  change: EditableElementChange,
): WidgetConfig {
  const position = roundPosition(change.position);

  if (widget.type === 'header') {
    const config = widget.config as HeaderConfig;
    if (change.element.kind === 'title') {
      return {
        ...widget,
        config: {
          ...config,
          titleX: position.x,
          titleY: position.y,
          titleW: position.width,
          titleH: position.height,
        },
      };
    }

    if (change.element.kind === 'badge' && change.element.index != null) {
      const badges = (config.badges ?? []).map((badge, index) =>
        index === change.element.index
          ? {
              ...badge,
              x: position.x,
              y: position.y,
              w: position.width,
              h: position.height,
            }
          : badge,
      );
      return { ...widget, config: { ...config, badges } };
    }
  }

  if (widget.type === 'weather'
      && change.element.kind === 'weather-item'
      && change.element.index != null) {
    const config = widget.config as WeatherConfig;
    const sourceItems = config.items?.length ? config.items : DEFAULT_WEATHER_ITEMS;
    const items = sourceItems.map((item, index) =>
      index === change.element.index
        ? {
            ...item,
            x: position.x,
            y: position.y,
            w: position.width,
            h: position.height,
          }
        : { ...item },
    );
    return { ...widget, config: { ...config, items } };
  }

  return widget;
}

function roundPosition(position: RenderRectangle): RenderRectangle {
  const round = (value: number) => Math.round(value * 10) / 10;
  return {
    x: round(position.x),
    y: round(position.y),
    width: round(position.width),
    height: round(position.height),
  };
}
