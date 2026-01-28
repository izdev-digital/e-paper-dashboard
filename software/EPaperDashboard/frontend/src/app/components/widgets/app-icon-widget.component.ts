import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme } from '../../models/types';

@Component({
  selector: 'app-widget-app-icon',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./app-icon-widget.component.scss'],
  template: `
    <div class="app-icon-widget">
      <img [src]="asAppIconConfig(widget.config).iconUrl || '/icon.svg'" [style.width.px]="asAppIconConfig(widget.config).size || 48" [style.height.px]="asAppIconConfig(widget.config).size || 48" alt="App Icon" />
    </div>
  `
})
export class AppIconWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;

  asAppIconConfig(config: any) { return config as any; }
}
