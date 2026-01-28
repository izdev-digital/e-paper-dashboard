import { Component, Input } from '@angular/core';
import { WidgetConfig, ColorScheme } from '../../models/types';

@Component({
  selector: 'app-widget-app-icon',
  standalone: true,
  styleUrls: ['./app-icon-widget.component.scss'],
  template: `<img class="app-icon" [src]="asAppIconConfig(widget.config).iconUrl || '/icon.svg'" alt="App Icon" />`
})
export class AppIconWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;

  asAppIconConfig(config: any) { return config as any; }
}
