import { Component, Input, OnChanges, OnDestroy, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { WidgetConfig, ColorScheme, DashboardLayout, AiContentConfig } from '../../models/types';
import { AiService } from '../../services/ai.service';
import { MarkdownWidgetComponent } from './markdown-widget.component';
import { resolveWidgetRenderContext } from './widget-render-context';

@Component({
  selector: 'app-widget-ai-content',
  standalone: true,
  imports: [CommonModule, MarkdownWidgetComponent],
  template: `
    <div class="ai-content-widget"
      [style.color]="getTextColor()"
      [style.--widget-title-font-size]="renderContext.titleFontSize + 'px'"
      [style.--widget-title-font-weight]="renderContext.titleFontWeight"
      [style.--widget-title-color]="getTitleColor()">
      @if (widget.showTitle !== false) {
        <h4 class="widget-frame-title">{{ widget.titleOverride || 'AI Content' }}</h4>
      }

      @if (content) {
        <app-widget-markdown
          [widget]="contentWidget"
          [colorScheme]="colorScheme"
          [designerSettings]="designerSettings">
        </app-widget-markdown>
      } @else {
        <div class="ai-content-empty">
          <i class="fa fa-wand-magic-sparkles"></i>
          @if (config.prompt.trim()) {
            <p class="ai-prompt-preview">No generated content cached</p>
          } @else {
            <p>Configure a prompt to generate AI content</p>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .ai-content-widget {
      display: flex;
      flex-direction: column;
      width: 100%;
      height: 100%;
      overflow: hidden;
    }

    app-widget-markdown {
      display: block;
      flex: 1;
      min-height: 0;
      overflow: hidden;
    }

    .ai-content-empty {
      display: flex;
      flex: 1;
      min-height: 0;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      opacity: 0.5;
      text-align: center;
      font-size: 0.8rem;

    }

    .ai-content-empty i {
      font-size: 1.5rem;
      margin-bottom: 0.5rem;
    }

    .ai-content-empty p {
      margin: 0;
    }

    .ai-prompt-preview {
      font-style: italic;
    }
  `]
})
export class AiContentWidgetComponent implements OnChanges, OnDestroy {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() designerSettings?: DashboardLayout;
  @Input() dashboardId?: string;
  @Input() generatedContent = '';

  content = '';
  private readonly generatedSubscription: Subscription;

  constructor(private readonly aiService: AiService) {
    this.generatedSubscription = this.aiService.widgetContentGenerated$.subscribe(event => {
      if (event.dashboardId === this.dashboardId && event.widgetId === this.widget?.id) {
        this.content = event.content;
      }
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['generatedContent'] || changes['widget']) {
      this.content = this.generatedContent;
    }
  }

  ngOnDestroy(): void {
    this.generatedSubscription.unsubscribe();
  }

  get config(): AiContentConfig {
    return this.widget.config as AiContentConfig;
  }

  get contentWidget(): WidgetConfig {
    return {
      ...this.widget,
      type: 'markdown',
      showTitle: false,
      titleOverride: undefined,
      config: { content: this.content }
    };
  }

  getTextColor(): string {
    return this.renderContext.textColor;
  }

  getTitleColor(): string {
    return this.renderContext.titleColor;
  }

  get renderContext() {
    return resolveWidgetRenderContext(this.widget, this.colorScheme, this.designerSettings);
  }
}
