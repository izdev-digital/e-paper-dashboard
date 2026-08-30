import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { WidgetConfig, ColorScheme, DashboardLayout } from '../../models/types';
import { resolveWidgetRenderContext } from './widget-render-context';

@Component({
  selector: 'app-widget-version',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./version-widget.component.scss'],
  template: `
    <div class="version-widget" [style.color]="getTextColor()" [style.fontSize.px]="getTextFontSize()" [style.fontWeight]="getTextFontWeight()">
      v{{ version || 'Loading...' }}
    </div>
  `
})
export class VersionWidgetComponent implements OnInit {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() designerSettings?: DashboardLayout;

  version: string | null = null;

  constructor(private httpClient: HttpClient) { }

  ngOnInit(): void {
    this.loadVersion();
  }

  private loadVersion(): void {
    this.httpClient.get<{ version: string }>('/api/app/version')
      .subscribe({
        next: (response) => {
          this.version = response.version;
        },
        error: (error) => {
          this.version = 'Unknown';
        }
      });
  }

  getTextFontSize(): number {
    return this.renderContext.textFontSize;
  }

  getTextFontWeight(): number {
    return this.renderContext.textFontWeight;
  }

  getTextColor(): string {
    return this.renderContext.textColor;
  }

  private get renderContext() {
    return resolveWidgetRenderContext(this.widget, this.colorScheme, this.designerSettings);
  }
}
