import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { marked } from 'marked';
import { WidgetConfig, ColorScheme, DashboardLayout } from '../../models/types';

@Component({
  selector: 'app-widget-markdown',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./markdown-widget.component.scss'],
  template: `
    <div class="markdown-widget" [style.color]="getTextColor()">
      <div class="markdown-content" [innerHTML]="parsedContent"></div>
    </div>
  `
})
export class MarkdownWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() designerSettings?: DashboardLayout;

  get parsedContent(): SafeHtml {
    const content = this.asMarkdownConfig(this.widget.config).content || '';
    const html = marked(content) as string;
    return this.sanitizer.bypassSecurityTrustHtml(html);
  }

  constructor(private sanitizer: DomSanitizer) {}

  asMarkdownConfig(config: any) { return config as any; }

  getTextColor(): string {
    if (this.widget.colorOverrides?.widgetTextColor) {
      return this.widget.colorOverrides.widgetTextColor;
    }
    return this.colorScheme?.widgetTextColor || this.colorScheme?.text || 'currentColor';
  }
}
