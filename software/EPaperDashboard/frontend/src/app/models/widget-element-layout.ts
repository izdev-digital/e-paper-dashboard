import { WidgetConfig } from './types';
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
  const binding = change.element.layoutBinding;
  if (!binding) return widget;

  const updated = cloneJson(widget) as unknown as Record<string, unknown>;
  if (binding.seedConfig) {
    updated['config'] = mergeMissing(
      cloneJson(binding.seedConfig),
      (updated['config'] ?? {}) as Record<string, unknown>,
    );
  }

  setJsonPointer(updated, binding.xPath, position.x);
  setJsonPointer(updated, binding.yPath, position.y);
  setJsonPointer(updated, binding.widthPath, position.width);
  setJsonPointer(updated, binding.heightPath, position.height);
  return updated as unknown as WidgetConfig;
}

function setJsonPointer(root: Record<string, unknown>, pointer: string, value: number): void {
  const segments = pointer.split('/').slice(1).map(segment =>
    segment.replace(/~1/g, '/').replace(/~0/g, '~'));
  if (!segments.length) throw new Error('Editable layout binding cannot target the document root.');

  let current: Record<string, unknown> | unknown[] = root;
  for (const [index, segment] of segments.slice(0, -1).entries()) {
    const nextSegment = segments[index + 1];
    const existing = Array.isArray(current)
      ? current[parseArrayIndex(segment)]
      : current[segment];
    if (typeof existing === 'object' && existing !== null) {
      current = existing as Record<string, unknown> | unknown[];
      continue;
    }
    const created: Record<string, unknown> | unknown[] = /^\d+$/.test(nextSegment) ? [] : {};
    if (Array.isArray(current)) current[parseArrayIndex(segment)] = created;
    else current[segment] = created;
    current = created;
  }

  const last = segments.at(-1)!;
  if (Array.isArray(current)) current[parseArrayIndex(last)] = value;
  else current[last] = value;
}

function parseArrayIndex(value: string): number {
  if (!/^\d+$/.test(value)) throw new Error(`Invalid array index in editable layout binding: ${value}`);
  return Number(value);
}

function mergeMissing(
  defaults: Record<string, unknown>,
  configured: Record<string, unknown>,
): Record<string, unknown> {
  const result = cloneJson(defaults);
  for (const [key, value] of Object.entries(configured)) {
    const defaultValue = result[key];
    result[key] = isRecord(defaultValue) && isRecord(value)
      ? mergeMissing(defaultValue, value)
      : cloneJson(value);
  }
  return result;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function cloneJson<T>(value: T): T {
  if (Array.isArray(value)) return value.map(item => cloneJson(item)) as T;
  if (isRecord(value)) {
    return Object.fromEntries(
      Object.entries(value).map(([key, item]) => [key, cloneJson(item)]),
    ) as T;
  }
  return value;
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
