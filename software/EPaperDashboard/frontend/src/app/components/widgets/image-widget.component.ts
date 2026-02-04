import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme } from '../../models/types';

@Component({
  selector: 'app-widget-image',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="image-widget-container">
      <img [src]="asImageConfig(widget.config).imageUrl" alt="Image" [style.object-fit]="asImageConfig(widget.config).fit || 'contain'" />
    </div>
  `,
  styles: [`
    .image-widget-container {
      width: 100%;
      height: 100%;
      display: flex;
      align-items: center;
      justify-content: center;
      overflow: hidden;
    }
    
    img {
      width: 100%;
      height: 100%;
      object-position: center;
    }
  `]
})
export class ImageWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;

  asImageConfig(config: any) { return config as any; }
}
