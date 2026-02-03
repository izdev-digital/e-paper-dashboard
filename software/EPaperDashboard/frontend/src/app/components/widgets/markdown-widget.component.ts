import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, DashboardLayout } from '../../models/types';

@Component({
  selector: 'app-widget-markdown',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./markdown-widget.component.scss'],
  template: `
    <div class="markdown-widget" [style.color]="getTextColor()">
      <div class="markdown-content">{{ asMarkdownConfig(widget.config).content }}</div>
    </div>
  `
})
export class MarkdownWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() designerSettings?: DashboardLayout;

  asMarkdownConfig(config: any) { return config as any; }

  getTextColor(): string {
    if (this.widget.colorOverrides?.widgetTextColor) {
      return this.widget.colorOverrides.widgetTextColor;
    }
    return this.colorScheme?.widgetTextColor || this.colorScheme?.text || 'currentColor';
  }
}
