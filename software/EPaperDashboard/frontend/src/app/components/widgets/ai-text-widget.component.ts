import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, DashboardLayout, AiTextConfig } from '../../models/types';

@Component({
  selector: 'app-widget-ai-text',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="ai-text-widget" [style.color]="getTextColor()">
      <div class="ai-text-placeholder d-flex flex-column align-items-center justify-content-center h-100 text-center p-2">
        <i class="fa fa-robot mb-2" [style.color]="getIconColor()" style="font-size: 1.5rem;"></i>
        <small class="text-muted">{{ getPlaceholderText() }}</small>
      </div>
    </div>
  `,
  styles: [`
    .ai-text-widget {
      height: 100%;
      width: 100%;
      overflow: hidden;
    }
    .ai-text-placeholder {
      height: 100%;
    }
  `]
})
export class AiTextWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() designerSettings?: DashboardLayout;
  @Input() llmConfigured = false;

  get aiTextConfig(): AiTextConfig {
    return this.widget.config as AiTextConfig;
  }

  getPlaceholderText(): string {
    if (!this.llmConfigured) {
      return 'Configure LLM provider in AI Settings';
    }
    const prompt = this.aiTextConfig?.prompt;
    return prompt ? `AI: "${prompt.substring(0, 60)}${prompt.length > 60 ? '…' : ''}"` : 'AI Text widget';
  }

  getTextColor(): string {
    return this.widget.colorOverrides?.widgetTextColor
      ?? this.colorScheme?.widgetTextColor
      ?? this.colorScheme?.text
      ?? 'currentColor';
  }

  getIconColor(): string {
    return this.widget.colorOverrides?.iconColor
      ?? this.colorScheme?.iconColor
      ?? this.colorScheme?.accent
      ?? 'currentColor';
  }
}
