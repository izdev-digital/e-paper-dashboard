import { Component, Input, OnInit, OnChanges, SimpleChanges, ViewChild, ElementRef, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, HassEntityState, GraphConfig, GraphSeriesConfig, DashboardLayout } from '../../models/types';
import { HomeAssistantService } from '../../services/home-assistant.service';
import { Chart, ChartConfiguration, LineController, BarController, CategoryScale, LinearScale, PointElement, LineElement, BarElement, Legend, Tooltip, Filler } from 'chart.js';

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
    <div class="graph-widget" [style.color]="getTextColor()">
      @if (!hasValidEntities()) {
        <div class="empty-state">
          <i class="fa fa-chart-line" [style.color]="getIconColor()"></i>
          <p>Not configured</p>
        </div>
      }
      @if (hasValidEntities()) {
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

  constructor(private haService: HomeAssistantService) {
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

  private loadChartData(): void {
    if (!this.config.series || this.config.series.length === 0 || !this.dashboardId) {
      console.log('[Graph Widget] Using mock data. Series:', this.config.series?.length || 0, 'dashboardId:', this.dashboardId);
      // Fallback to mock data if no series or dashboard ID
      if (this.config.series && this.config.series.length > 0) {
        this.config.series.forEach(s => {
          if (s.entityId) {
            this.generateMockHistoricalData(s.entityId);
          }
        });
      }
      this.updateChart();
      return;
    }

    // Fetch real data from Home Assistant
    const entityIds = this.config.series
      .filter(e => e.entityId)
      .map(e => e.entityId);

    if (entityIds.length === 0) {
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

    console.log('[Graph Widget] Fetching history for entities:', entityIds, 'period:', this.config.period, 'dashboardId:', this.dashboardId);
    this.haService.getEntityHistory(this.dashboardId, entityIds, hours).subscribe({
      next: (historyData) => {
        console.log('[Graph Widget] Received history data:', Object.keys(historyData).length, 'entities');
        Object.entries(historyData).forEach(([id, states]) => {
          console.log(`  - ${id}: ${states.length} data points`);
        });
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
      error: (error) => {
        console.warn('Failed to fetch entity history, falling back to mock data:', error);
        // Fallback to mock data on error
        this.chartDataByEntity.clear();
        this.config.series.forEach(s => {
          if (s.entityId) {
            this.generateMockHistoricalData(s.entityId);
          }
        });
        this.updateChart();
      }
    });
  }

  private generateMockHistoricalData(entityId: string): void {
    const now = new Date();
    const period = this.config.period || '24h';
    let hoursBack = 24;

    switch (period) {
      case '1h': hoursBack = 1; break;
      case '6h': hoursBack = 6; break;
      case '24h': hoursBack = 24; break;
      case '7d': hoursBack = 24 * 7; break;
      case '30d': hoursBack = 24 * 30; break;
    }

    const currentEntityState = this.getEntityState(entityId);
    const baseValue = parseFloat(currentEntityState?.state || '0');

    const dataPoints: ChartDataPoint[] = [];
    const interval = Math.max(1, Math.floor(hoursBack / 20)); // Max 20 data points

    for (let i = hoursBack; i >= 0; i -= interval) {
      const timestamp = new Date(now.getTime() - i * 60 * 60 * 1000);
      // Add some variation to the data
      const noise = (Math.random() - 0.5) * (baseValue * 0.2);
      const value = Math.max(0, baseValue + noise);
      dataPoints.push({ timestamp, value });
    }

    this.chartDataByEntity.set(entityId, dataPoints);
  }

  private updateChart(): void {
    if (!this.canvasRef?.nativeElement || this.chartDataByEntity.size === 0) return;

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
    if (this.widget.colorOverrides?.widgetTitleTextColor) {
      return this.widget.colorOverrides.widgetTitleTextColor;
    }
    return this.colorScheme?.widgetTitleTextColor || this.colorScheme?.text || 'currentColor';
  }

  getTextColor(): string {
    if (this.widget.colorOverrides?.widgetTextColor) {
      return this.widget.colorOverrides.widgetTextColor;
    }
    return this.colorScheme?.widgetTextColor || this.colorScheme?.text || 'currentColor';
  }

  getIconColor(): string {
    if (this.widget.colorOverrides?.iconColor) {
      return this.widget.colorOverrides.iconColor;
    }
    return this.colorScheme?.iconColor || this.colorScheme?.accent || 'currentColor';
  }

  getTextFontSize(): number {
    return this.designerSettings?.textFontSize ?? 12;
  }
}
