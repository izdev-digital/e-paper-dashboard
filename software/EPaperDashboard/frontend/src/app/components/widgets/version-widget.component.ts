import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, DashboardLayout } from '../../models/types';
import { resolveWidgetRenderContext } from './widget-render-context';

@Component({
  selector: 'app-widget-version',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./version-widget.component.scss'],
  template: `
    <div class="version-widget" [style.color]="getTextColor()" [style.fontSize.px]="getTextFontSize()" [style.fontWeight]="getTextFontWeight()">
      v{{ version || '?' }}
    </div>
  `
})
export class VersionWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() designerSettings?: DashboardLayout;

  @Input() version = '';

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
