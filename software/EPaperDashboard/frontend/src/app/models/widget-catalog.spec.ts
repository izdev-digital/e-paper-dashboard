import { createDefaultWidgetConfig, getWidgetDefaultSize, getWidgetDefinition, isEntityCompatible, WIDGET_DEFINITIONS } from './widget-catalog';

describe('widget catalog', () => {
  it('defines every widget type once', () => {
    expect(new Set(WIDGET_DEFINITIONS.map(item => item.type)).size).toBe(WIDGET_DEFINITIONS.length);
  });

  it('creates independent nested defaults', () => {
    const first = createDefaultWidgetConfig('weather') as any;
    const second = createDefaultWidgetConfig('weather') as any;
    first.items[0].visible = false;
    expect(second.items[0].visible).toBe(true);
  });

  it('provides widget-specific sizes and capabilities', () => {
    expect(getWidgetDefaultSize('header', 8, 12)).toEqual({ w: 8, h: 1 });
    expect(getWidgetDefinition('app-icon').supportsTitle).toBe(false);
    expect(isEntityCompatible('calendar', { entityId: 'calendar.home', friendlyName: 'Home', domain: 'calendar' })).toBe(true);
    expect(isEntityCompatible('calendar', { entityId: 'sensor.temp', friendlyName: 'Temp', domain: 'sensor' })).toBe(false);
  });
});
