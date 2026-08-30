import { Component, Input, SecurityContext } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer } from '@angular/platform-browser';
import { marked } from 'marked';
import { WidgetConfig, ColorScheme, DashboardLayout } from '../../models/types';
import { resolveWidgetRenderContext } from './widget-render-context';

@Component({
  selector: 'app-widget-markdown',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./markdown-widget.component.scss'],
  template: `
    <div class="markdown-widget"
      [style.color]="renderContext.textColor"
      [style.--markdown-title-font-size]="renderContext.titleFontSize + 'px'"
      [style.--markdown-text-font-size]="renderContext.textFontSize + 'px'"
      [style.--markdown-title-font-weight]="renderContext.titleFontWeight"
      [style.--markdown-text-font-weight]="renderContext.textFontWeight">
      <div class="markdown-content" [innerHTML]="parsedContent"></div>
    </div>
  `
})
export class MarkdownWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() designerSettings?: DashboardLayout;

  get parsedContent(): string {
    const content = this.asMarkdownConfig(this.widget.config).content || '';
    const html = marked(content) as string;
    return this.sanitizer.sanitize(SecurityContext.HTML, html) || '';
  }

  constructor(private sanitizer: DomSanitizer) { }

  asMarkdownConfig(config: any) { return config as any; }

  get renderContext() {
    return resolveWidgetRenderContext(this.widget, this.colorScheme, this.designerSettings);
  }
}
