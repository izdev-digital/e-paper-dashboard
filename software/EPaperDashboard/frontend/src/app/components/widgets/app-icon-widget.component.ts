import { Component, Input } from '@angular/core';
import { WidgetConfig, ColorScheme } from '../../models/types';

@Component({
  selector: 'app-widget-app-icon',
  standalone: true,
  styleUrls: ['./app-icon-widget.component.scss'],
  template: `<img class="app-icon" [src]="asAppIconConfig(widget.config).iconUrl || '/icon.svg'" [style.color]="getIconColor()" alt="App Icon" />`
})
export class AppIconWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;

  asAppIconConfig(config: any) { return config as any; }

  getIconColor(): string {
    if (this.widget.colorOverrides?.iconColor) {
      return this.widget.colorOverrides.iconColor;
    }
    return this.colorScheme?.iconColor || this.colorScheme?.accent || 'currentColor';
  }
}
