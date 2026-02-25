import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, ImageConfig } from '../../models/types';

@Component({
  selector: 'app-widget-image',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="image-widget-wrapper">
      @if (widget.showTitle !== false && widget.titleOverride) {
        <h4 class="image-title">{{ widget.titleOverride }}</h4>
      }
      <div class="image-widget-container">
        <img
          [src]="cfg.imageUrl"
          alt="Image"
          [style.width.%]="(cfg.zoom ?? 1) * 100"
          [style.height.%]="(cfg.zoom ?? 1) * 100"
          [style.left.%]="-((cfg.zoom ?? 1) - 1) * ((cfg.offsetX ?? 0) + 1) * 50"
          [style.top.%]="-((cfg.zoom ?? 1) - 1) * ((cfg.offsetY ?? 0) + 1) * 50"
          [style.object-fit]="(cfg.zoom ?? 1) > 1 ? 'cover' : 'contain'"
        />
      </div>
    </div>
  `,
  styles: [`
    .image-widget-wrapper {
      width: 100%;
      height: 100%;
      display: flex;
      flex-direction: column;
    }
    
    .image-title {
      margin: 0;
      padding: 8px 12px 4px 12px;
      font-size: 15px;
      font-weight: 600;
      text-align: center;
      line-height: 1.2;
      flex-shrink: 0;
    }
    
    .image-widget-container {
      width: 100%;
      flex: 1;
      overflow: hidden;
      min-height: 0;
      position: relative;
    }
    
    img {
      position: absolute;
      object-fit: contain;
    }
  `]
})
export class ImageWidgetComponent {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;

  get cfg(): ImageConfig {
    return this.widget.config as ImageConfig;
  }
}
