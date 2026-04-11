import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { marked } from 'marked';
import { WidgetConfig, ColorScheme, DashboardLayout, AiContentConfig } from '../../models/types';

@Component({
  selector: 'app-widget-ai-content',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./markdown-widget.component.scss'],
  template: `
    <div class="markdown-widget" [style.color]="getTextColor()">
      @if (hasContent()) {
        <div class="markdown-content" [innerHTML]="parsedContent"></div>
      } @else {
        <div class="ai-content-empty">
          <i class="fa fa-wand-magic-sparkles"></i>
          <p>Configure a prompt and generate content</p>
        </div>
      }
    </div>
  `,
  styles: [`
    .ai-content-empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      height: 100%;
      opacity: 0.5;
      text-align: center;
      font-size: 0.8rem;

      i {
        font-size: 1.5rem;
        margin-bottom: 0.5rem;
      }

      p {
        margin: 0;
      }
    }
  `]
})
export class AiContentWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() designerSettings?: DashboardLayout;

  constructor(private sanitizer: DomSanitizer) {}

  get config(): AiContentConfig {
    return this.widget.config as AiContentConfig;
  }

  hasContent(): boolean {
    return !!this.config.content?.trim();
  }

  get parsedContent(): SafeHtml {
    const content = this.config.content || '';
    const html = marked(content) as string;
    return this.sanitizer.bypassSecurityTrustHtml(html);
  }

  getTextColor(): string {
    if (this.widget.colorOverrides?.widgetTextColor) {
      return this.widget.colorOverrides.widgetTextColor;
    }
    return this.colorScheme?.widgetTextColor || this.colorScheme?.text || 'currentColor';
  }
}
