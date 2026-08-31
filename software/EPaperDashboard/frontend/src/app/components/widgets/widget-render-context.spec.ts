import { ColorScheme, DashboardLayout, WidgetConfig } from '../../models/types';
import { normalizeWidgetFontWeight, resolveWidgetRenderContext } from './widget-render-context';

describe('widget render context', () => {
  const widget = {
    id: 'widget-1',
    type: 'markdown',
    position: { x: 0, y: 0, w: 1, h: 1 },
    config: {},
  } as WidgetConfig;
  const colorScheme = {
    text: '#111111',
    accent: '#ff0000',
    widgetTitleTextColor: '#222222',
    widgetTextColor: '#333333',
    iconColor: '#444444',
    widgetBackgroundColor: '#ffffff',
    widgetBorderColor: '#000000',
  } as ColorScheme;

  it('uses the same typography defaults as the native renderer', () => {
    const context = resolveWidgetRenderContext(widget, colorScheme);

    expect(context.titleFontSize).toBe(16);
    expect(context.textFontSize).toBe(14);
    expect(context.titleFontWeight).toBe(700);
    expect(context.textFontWeight).toBe(400);
  });

  it('normalizes unsupported font weights', () => {
    expect(normalizeWidgetFontWeight(300)).toBe(400);
    expect(normalizeWidgetFontWeight(600)).toBe(400);
    expect(normalizeWidgetFontWeight(700)).toBe(700);
    expect(normalizeWidgetFontWeight(900)).toBe(700);
  });

  it('prefers widget color overrides', () => {
    const overriddenWidget = {
      ...widget,
      colorOverrides: {
        widgetTitleTextColor: '#aaaaaa',
        widgetTextColor: '#bbbbbb',
        iconColor: '#cccccc',
      },
    } as WidgetConfig;
    const settings = {
      titleFontSize: 20,
      textFontSize: 12,
      titleFontWeight: 900,
      textFontWeight: 300,
    } as DashboardLayout;

    const context = resolveWidgetRenderContext(overriddenWidget, colorScheme, settings);

    expect(context.titleColor).toBe('#aaaaaa');
    expect(context.textColor).toBe('#bbbbbb');
    expect(context.iconColor).toBe('#cccccc');
    expect(context.titleFontWeight).toBe(700);
    expect(context.textFontWeight).toBe(400);
  });
});
