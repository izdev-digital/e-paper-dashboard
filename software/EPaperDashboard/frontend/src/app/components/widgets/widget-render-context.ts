import { ColorScheme, DashboardLayout, WidgetConfig } from '../../models/types';

export type SupportedWidgetFontWeight = 400 | 700;

export interface WidgetRenderContext {
  titleFontSize: number;
  textFontSize: number;
  titleFontWeight: SupportedWidgetFontWeight;
  textFontWeight: SupportedWidgetFontWeight;
  titleColor: string;
  textColor: string;
  iconColor: string;
  backgroundColor: string;
  borderColor: string;
}

export function resolveWidgetRenderContext(
  widget: WidgetConfig,
  colorScheme: ColorScheme | null | undefined,
  settings?: DashboardLayout,
): WidgetRenderContext {
  const scheme = colorScheme ?? ({} as ColorScheme);
  const fallbackText = scheme.text || 'currentColor';

  return {
    titleFontSize: settings?.titleFontSize ?? 16,
    textFontSize: settings?.textFontSize ?? 14,
    titleFontWeight: normalizeWidgetFontWeight(settings?.titleFontWeight ?? 700),
    textFontWeight: normalizeWidgetFontWeight(settings?.textFontWeight ?? 400),
    titleColor: widget.colorOverrides?.widgetTitleTextColor
      || scheme.widgetTitleTextColor
      || fallbackText,
    textColor: widget.colorOverrides?.widgetTextColor
      || scheme.widgetTextColor
      || fallbackText,
    iconColor: widget.colorOverrides?.iconColor
      || scheme.iconColor
      || scheme.accent
      || fallbackText,
    backgroundColor: widget.colorOverrides?.widgetBackgroundColor
      || scheme.widgetBackgroundColor
      || scheme.background
      || 'transparent',
    borderColor: widget.colorOverrides?.widgetBorderColor
      || scheme.widgetBorderColor
      || fallbackText,
  };
}

export function normalizeWidgetFontWeight(weight: number): SupportedWidgetFontWeight {
  return weight >= 700 ? 700 : 400;
}
