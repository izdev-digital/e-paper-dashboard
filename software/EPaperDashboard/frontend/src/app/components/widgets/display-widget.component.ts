import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, DisplayConfig, DashboardLayout } from '../../models/types';

@Component({
  selector: 'app-widget-display',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="display-widget" [ngStyle]="{'font-size.px': fontSize, color: color}">
      {{ text }}
    </div>
  `
})
export class DisplayWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() designerSettings?: DashboardLayout;

  get config(): DisplayConfig {
    return (this.widget?.config || {}) as DisplayConfig;
  }

  get text(): string {
    return this.config.text || '';
  }

  get fontSize(): number {
    return this.designerSettings?.textFontSize ?? 18;
  }

  get color(): string {
    return this.config.color || (this.colorScheme && this.colorScheme.foreground) || 'currentColor';
  }
}
