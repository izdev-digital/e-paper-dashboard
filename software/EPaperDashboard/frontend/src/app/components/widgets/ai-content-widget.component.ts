import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, DashboardLayout, AiContentConfig } from '../../models/types';

@Component({
  selector: 'app-widget-ai-content',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./markdown-widget.component.scss'],
  template: `
    <div class="markdown-widget" [style.color]="getTextColor()">
      <div class="ai-content-empty">
        <i class="fa fa-wand-magic-sparkles"></i>
        @if (config.prompt.trim()) {
          <p class="ai-prompt-preview">{{ config.prompt }}</p>
        } @else {
          <p>Configure a prompt to generate AI content</p>
        }
      </div>
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

      .ai-prompt-preview {
        font-style: italic;
        max-height: 3.6em;
        overflow: hidden;
        text-overflow: ellipsis;
      }
    }
  `]
})
export class AiContentWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() designerSettings?: DashboardLayout;

  get config(): AiContentConfig {
    return this.widget.config as AiContentConfig;
  }

  getTextColor(): string {
    if (this.widget.colorOverrides?.widgetTextColor) {
      return this.widget.colorOverrides.widgetTextColor;
    }
    return this.colorScheme?.widgetTextColor || this.colorScheme?.text || 'currentColor';
  }
}
