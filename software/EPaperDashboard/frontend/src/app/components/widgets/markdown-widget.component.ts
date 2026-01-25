import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme } from '../../models/types';

@Component({
  selector: 'app-widget-markdown',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="markdown-widget">
      <div class="markdown-content">{{ asMarkdownConfig(widget.config).content }}</div>
    </div>
  `
})
export class MarkdownWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;

  asMarkdownConfig(config: any) { return config as any; }
}
