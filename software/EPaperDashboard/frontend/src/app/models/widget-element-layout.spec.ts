import { describe, expect, it } from 'vitest';
import { EditableWidgetElementGeometry } from '../services/dashboard-render-preview.service';
import { WidgetConfig } from './types';
import { applyEditableElementChange } from './widget-element-layout';

function element(
  kind: string,
  index: number | null,
): EditableWidgetElementGeometry {
  return {
    id: index == null ? kind : `${kind}-${index}`,
    kind,
    index,
    bounds: { x: 0, y: 0, width: 10, height: 10 },
    position: { x: 0, y: 0, width: 10, height: 10 },
    movable: true,
    resizable: true,
  };
}

describe('applyEditableElementChange', () => {
  it('updates a header title without mutating the widget', () => {
    const widget: WidgetConfig = {
      id: 'header-1',
      type: 'header',
      position: { x: 0, y: 0, w: 4, h: 1 },
      config: { title: 'Dashboard', titleX: 5 },
    };

    const updated = applyEditableElementChange(widget, {
      element: element('title', null),
      position: { x: 11.14, y: 12.26, width: 60.04, height: 24.08 },
    });

    expect(updated).not.toBe(widget);
    expect(updated.config).toMatchObject({
      title: 'Dashboard',
      titleX: 11.1,
      titleY: 12.3,
      titleW: 60,
      titleH: 24.1,
    });
    expect(widget.config).toEqual({ title: 'Dashboard', titleX: 5 });
  });

  it('uses the renderer index to update the matching badge', () => {
    const widget: WidgetConfig = {
      id: 'header-1',
      type: 'header',
      position: { x: 0, y: 0, w: 4, h: 1 },
      config: {
        title: 'Dashboard',
        badges: [{}, { entityId: 'sensor.room' }],
      },
    };

    const updated = applyEditableElementChange(widget, {
      element: element('badge', 1),
      position: { x: 20, y: 30, width: 40, height: 15 },
    });

    expect((updated.config as { badges: object[] }).badges).toEqual([
      {},
      { entityId: 'sensor.room', x: 20, y: 30, w: 40, h: 15 },
    ]);
    expect((widget.config as { badges: object[] }).badges[1]).toEqual({ entityId: 'sensor.room' });
  });

  it('materializes default weather items before applying a position', () => {
    const widget: WidgetConfig = {
      id: 'weather-1',
      type: 'weather',
      position: { x: 0, y: 0, w: 4, h: 2 },
      config: { entityId: 'weather.home' },
    };

    const updated = applyEditableElementChange(widget, {
      element: element('weather-item', 2),
      position: { x: 44, y: 25, width: 48, height: 18 },
    });
    const items = (updated.config as { items: Array<Record<string, unknown>> }).items;

    expect(items).toHaveLength(5);
    expect(items[2]).toMatchObject({
      type: 'condition',
      x: 44,
      y: 25,
      w: 48,
      h: 18,
    });
    expect((widget.config as { items?: unknown[] }).items).toBeUndefined();
  });
});
