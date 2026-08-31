import { describe, expect, it } from 'vitest';
import { WidgetPreviewComponent } from './widget-preview.component';
import { WidgetConfig } from '../../models/types';

function todoWidget(): WidgetConfig {
  return {
    id: 'todo-1',
    type: 'todo',
    position: { x: 0, y: 0, w: 2, h: 2 },
    config: { entityId: 'todo.house' },
  };
}

describe('WidgetPreviewComponent source status', () => {
  it('distinguishes source errors from an empty result', () => {
    const component = new WidgetPreviewComponent();
    component.widget = todoWidget();
    component.dataFetched = true;
    component.sourceStatuses = {
      'todo:todo.house': {
        state: 'error',
        error: 'Home Assistant unavailable',
        fetchedAt: new Date().toISOString(),
        fromCache: false,
      },
    };

    expect(component.getPlaceholderLabel()).toBe('Data unavailable');
    expect(component.getSourceError()).toBe('Home Assistant unavailable');
  });

  it('shows an intentional empty state without treating it as an error', () => {
    const component = new WidgetPreviewComponent();
    component.widget = todoWidget();
    component.dataFetched = true;
    component.sourceStatuses = {
      'todo:todo.house': {
        state: 'empty',
        fetchedAt: new Date().toISOString(),
        fromCache: true,
      },
    };

    expect(component.getPlaceholderLabel()).toBe('No data');
    expect(component.getSourceError()).toBeUndefined();
  });
});
