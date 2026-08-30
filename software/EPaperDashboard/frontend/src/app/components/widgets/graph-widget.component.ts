import { Component, Input, OnInit, OnChanges, SimpleChanges, ViewChild, ElementRef, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, HassEntityState, GraphConfig, GraphSeriesConfig, DashboardLayout } from '../../models/types';
import { EntityHistoryService } from '../../services/entity-history.service';
import { Chart, ChartConfiguration, LineController, BarController, CategoryScale, LinearScale, PointElement, LineElement, BarElement, Legend, Tooltip, Filler } from 'chart.js';
import { resolveWidgetRenderContext } from './widget-render-context';

// Register Chart.js components
Chart.register(LineController, BarController, CategoryScale, LinearScale, PointElement, LineElement, BarElement, Legend, Tooltip, Filler);

interface ChartDataPoint {
  timestamp: Date;
  value: number;
}

@Component({
  selector: 'app-widget-graph',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./graph-widget.component.scss'],
  template: `
    <div class="graph-widget" [style.color]="getTextColor()" [style.--headerFontSize]="getHeaderFontSize() + 'px'" [style.--headerFontWeight]="getHeaderFontWeight()" [style.--titleColor]="getTitleColor()" [style.--iconColor]="getIconColor()"
      [style.--widget-title-font-size]="getHeaderFontSize() + 'px'"
      [style.--widget-title-font-weight]="getHeaderFontWeight()"
      [style.--widget-title-color]="getTitleColor()">
      @if (!isDataFetched()) {
        <div class="preview-state">
          <i class="fa fa-chart-line"></i>
          <p>Graph</p>
        </div>
      }
      @if (isDataFetched()) {
        @if (widget.showTitle !== false && widget.titleOverride) {
          <h4 class="widget-frame-title">{{ widget.titleOverride }}</h4>
        }
        <canvas 
          #chartCanvas 
          class="chart-canvas"
          [attr.data-plot-type]="config.plotType || 'line'"
          [attr.data-last-update]="lastChartUpdate">
        </canvas>
      }
    </div>
  `
})
export class GraphWidgetComponent implements OnInit, OnChanges {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() entityStates: Record<string, HassEntityState> | null = null;
  @Input() designerSettings?: DashboardLayout;
  @Input() dashboardId?: string;
  @ViewChild('chartCanvas') canvasRef?: ElementRef<HTMLCanvasElement>;

  private chart: Chart | null = null;
  chartDataByEntity: Map<string, ChartDataPoint[]> = new Map();
  lastChartUpdate = 0;

  constructor(private haService: EntityHistoryService) {
    effect(() => {
      this.loadChartData();
    });
  }

  get config(): GraphConfig { 
    const cfg = (this.widget?.config as GraphConfig) || ({} as GraphConfig);
    // Ensure series is always an array
    if (!Array.isArray(cfg.series)) {
      cfg.series = [];
    }
    return cfg;
  }

  ngOnInit(): void {
    this.loadChartData();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['entityStates']) {
      this.loadChartData();
    }
  }

  hasValidEntities(): boolean {
    return this.config.series && this.config.series.length > 0 && 
           this.config.series.some(e => e.entityId && this.getEntityState(e.entityId));
  }

  /**
   * Checks if graph data has been fetched and is available for display.
   */
  isDataFetched(): boolean {
    // Only show preview when there are no series configured
    if (!this.config.series || this.config.series.length === 0) return false;
    
    // Check if we have valid entity IDs in the series
    const hasValidEntities = this.config.series.some(s => s.entityId);
    if (!hasValidEntities) return false;
    
    // Data is fetched once we have chart data populated
    return this.chartDataByEntity.size > 0;
  }

  private loadChartData(): void {
    if (!this.config.series || this.config.series.length === 0 || !this.dashboardId) {
      this.chartDataByEntity.clear();
      this.updateChart();
      return;
    }

    // Fetch real data from Home Assistant
    const entityIds = this.config.series
      .filter(e => e.entityId)
      .map(e => e.entityId);

    if (entityIds.length === 0) {
      this.chartDataByEntity.clear();
      this.updateChart();
      return;
    }

    const hoursMap: Record<string, number> = {
      '1h': 1,
      '6h': 6,
      '24h': 24,
      '7d': 24 * 7,
      '30d': 24 * 30
    };

    const hours = hoursMap[this.config.period || '24h'] || 24;

    this.haService.getEntityHistory(this.dashboardId, entityIds, hours).subscribe({
      next: (historyData) => {
        this.chartDataByEntity.clear();
        // Convert API response to chart data format
        Object.entries(historyData).forEach(([entityId, states]) => {
          const dataPoints: ChartDataPoint[] = states.map(state => ({
            timestamp: new Date(state.lastChanged),
            value: state.numericValue
          }));
          this.chartDataByEntity.set(entityId, dataPoints);
        });
        this.updateChart();
      },
      error: () => {
        this.chartDataByEntity.clear();
        this.updateChart();
      }
    });
  }

  private updateChart(): void {
    if (this.chartDataByEntity.size === 0) {
      this.chart?.destroy();
      this.chart = null;
      return;
    }

    if (!this.canvasRef?.nativeElement) return;

    const canvas = this.canvasRef.nativeElement;
    const plotType = this.config.plotType || 'line';

    // Get time labels from first series data
    const firstSeries = this.config.series?.[0]?.entityId;
    const firstDataPoints = firstSeries ? this.chartDataByEntity.get(firstSeries) : undefined;
    const timeLabels = firstDataPoints?.map(d => this.formatTime(d.timestamp)) || [];

    // Build datasets for each series
    const datasets = this.config.series?.map((s, index) => {
      const dataPoints = this.chartDataByEntity.get(s.entityId) || [];
      const color = s.color || this.getDefaultColor(index);
      const label = s.label || s.entityId;

      return {
        label,
        data: dataPoints.map(d => d.value),
        borderColor: color,
        backgroundColor: plotType === 'bar' 
          ? color 
          : 'transparent',
        borderWidth: plotType === 'line' ? (this.config.lineWidth ?? 2) : (this.config.barWidth ?? 2),
        barThickness: plotType === 'bar' ? (this.config.barWidth ? this.config.barWidth * 3 : undefined) : undefined,
        fill: false,
        tension: 0.3,
        pointRadius: 0,
        pointHoverRadius: 0,
        borderSkipped: false
      };
    }) || [];

    const chartConfig: ChartConfiguration = {
      type: plotType as 'line' | 'bar',
      data: {
        labels: timeLabels,
        datasets: datasets as any
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            display: this.config.series && this.config.series.length > 1,
            labels: {
              color: this.getTextColor(),
              font: { size: 10 },
              boxWidth: 8,
              padding: 8
            }
          },
          tooltip: {
            enabled: true,
            backgroundColor: 'rgba(0,0,0,0.7)',
            titleColor: '#fff',
            bodyColor: '#fff',
            padding: 8,
            cornerRadius: 4
          }
        },
        scales: {
          x: {
            display: true,
            grid: {
              display: false,
              color: `${this.colorScheme?.widgetBorderColor || '#000'}20`
            },
            ticks: {
              color: this.getTextColor(),
              font: { size: this.getTextFontSize() },
              maxTicksLimit: 5
            }
          },
          y: {
            display: true,
            grid: {
              color: `${this.colorScheme?.widgetBorderColor || '#000'}20`
            },
            ticks: {
              color: this.getTextColor(),
              font: { size: this.getTextFontSize() },
              maxTicksLimit: 4
            }
          }
        }
      }
    };

    // Destroy existing chart if any
    if (this.chart) {
      this.chart.destroy();
    }

    // Create new chart
    this.chart = new Chart(canvas, chartConfig);
    this.lastChartUpdate = Date.now();
  }

  private formatTime(date: Date): string {
    const hours = date.getHours().toString().padStart(2, '0');
    const minutes = date.getMinutes().toString().padStart(2, '0');
    return `${hours}:${minutes}`;
  }

  private getDefaultColor(index: number): string {
    // Use colors from the selected color scheme palette, skipping background/text colors
    // Prefer accent and palette colors that are suitable for charts
    const paletteColors = this.colorScheme?.palette || ['#ff0000', '#00ff00', '#0000ff', '#ffff00', '#ff00ff', '#00ffff'];
    // Filter out very light or very dark colors that might be text/background
    const chartColors = paletteColors.filter(c => c && c !== this.colorScheme?.background && c !== this.colorScheme?.canvasBackgroundColor);
    
    if (chartColors.length > 0) {
      return chartColors[index % chartColors.length];
    }
    
    // Fallback colors if palette is empty
    const fallbackColors = ['#ff0000', '#00ff00', '#0000ff', '#ffff00', '#ff00ff', '#00ffff'];
    return fallbackColors[index % fallbackColors.length];
  }

  getEntityState(entityId?: string) {
    if (!entityId || !this.entityStates) return null;
    return this.entityStates[entityId] ?? null;
  }

  getTitleColor(): string {
    return this.renderContext.titleColor;
  }

  getTextColor(): string {
    return this.renderContext.textColor;
  }

  getIconColor(): string {
    return this.renderContext.iconColor;
  }

  getTextFontSize(): number {
    return this.renderContext.textFontSize;
  }

  getHeaderFontSize(): number {
    return this.renderContext.titleFontSize;
  }

  getHeaderFontWeight(): number {
    return this.renderContext.titleFontWeight;
  }

  getTextFontWeight(): number {
    return this.renderContext.textFontWeight;
  }

  private get renderContext() {
    return resolveWidgetRenderContext(this.widget, this.colorScheme, this.designerSettings);
  }
}
