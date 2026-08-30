import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DashboardRenderPreviewService } from './dashboard-render-preview.service';
import { DEFAULT_COLOR_SCHEMES, DashboardLayout } from '../models/types';

describe('DashboardRenderPreviewService', () => {
  let service: DashboardRenderPreviewService;
  let http: HttpTestingController;

  const layout: DashboardLayout = {
    width: 800,
    height: 480,
    gridCols: 12,
    gridRows: 8,
    colorScheme: DEFAULT_COLOR_SCHEMES[0],
    widgets: [],
    canvasPadding: 16,
    widgetGap: 4,
    widgetBorder: 3,
    widgetPadding: 4,
    titleFontSize: 16,
    textFontSize: 14,
    titleFontWeight: 700,
    textFontWeight: 400,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(DashboardRenderPreviewService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts the transient layout and revision to the canonical preview endpoint', () => {
    service.render('dashboard-1', layout, 9, true).subscribe();

    const request = http.expectOne('/api/dashboards/dashboard-1/designer-preview');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ layout, revision: 9, refreshData: true });
    request.flush({
      revision: 9,
      width: 800,
      height: 480,
      contentType: 'image/png',
      imageBase64: 'AA==',
      renderedAt: '2026-08-30T00:00:00Z',
      widgets: [],
    });
  });

  it('creates a browser image URL from the encoded response', () => {
    expect(service.toImageUrl({
      revision: 1,
      width: 1,
      height: 1,
      contentType: 'image/png',
      imageBase64: 'AA==',
      renderedAt: '2026-08-30T00:00:00Z',
      widgets: [],
    })).toBe('data:image/png;base64,AA==');
  });
});
