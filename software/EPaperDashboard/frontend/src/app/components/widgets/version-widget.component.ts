import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { WidgetConfig, ColorScheme, DashboardLayout } from '../../models/types';

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
    return this.designerSettings?.textFontSize ?? 14;
  }

  getTextFontWeight(): number {
    return this.designerSettings?.textFontWeight ?? 400;
  }

  getTextColor(): string {
    if (this.widget.colorOverrides?.widgetTextColor) {
      return this.widget.colorOverrides.widgetTextColor;
    }
    return this.colorScheme?.widgetTextColor || this.colorScheme?.text || 'currentColor';
  }
}
