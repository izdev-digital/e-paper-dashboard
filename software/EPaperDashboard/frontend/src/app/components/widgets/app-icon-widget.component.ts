import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme } from '../../models/types';

@Component({
  selector: 'app-widget-app-icon',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="app-icon-widget" style="display:flex;align-items:center;justify-content:center;height:100%;">
      <img [src]="asAppIconConfig(widget.config).iconUrl || '/icon.svg'" [style.width.px]="asAppIconConfig(widget.config).size || 48" [style.height.px]="asAppIconConfig(widget.config).size || 48" alt="App Icon" style="object-fit:contain;" />
    </div>
  `
})
export class AppIconWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;

  asAppIconConfig(config: any) { return config as any; }
}
