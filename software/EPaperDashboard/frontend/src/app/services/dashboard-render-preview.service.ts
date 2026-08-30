import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DashboardLayout } from '../models/types';

export interface RenderRectangle {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface WidgetRenderGeometry {
  id: string;
  type: string;
  bounds: RenderRectangle;
  contentBounds: RenderRectangle;
  elements: EditableWidgetElementGeometry[];
}

export interface EditableWidgetElementGeometry {
  id: string;
  kind: string;
  index: number | null;
  bounds: RenderRectangle;
  position: RenderRectangle;
  movable: boolean;
  resizable: boolean;
}

export interface DashboardRenderPreview {
  revision: number;
  width: number;
  height: number;
  contentType: string;
  imageBase64: string;
  renderedAt: string;
  widgets: WidgetRenderGeometry[];
}

@Injectable({ providedIn: 'root' })
export class DashboardRenderPreviewService {
  private readonly http = inject(HttpClient);

  render(
    dashboardId: string,
    layout: DashboardLayout,
    revision: number,
    refreshData = false,
  ): Observable<DashboardRenderPreview> {
    return this.http.post<DashboardRenderPreview>(
      `/api/dashboards/${dashboardId}/designer-preview`,
      { layout, revision, refreshData },
    );
  }

  toImageUrl(preview: DashboardRenderPreview): string {
    return `data:${preview.contentType};base64,${preview.imageBase64}`;
  }
}
