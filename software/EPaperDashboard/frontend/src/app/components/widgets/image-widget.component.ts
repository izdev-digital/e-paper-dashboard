import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme } from '../../models/types';

@Component({
  selector: 'app-widget-image',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="image-widget-wrapper">
      @if (widget.titleOverride) {
        <h4 class="image-title">{{ widget.titleOverride }}</h4>
      }
      <div class="image-widget-container">
        <img [src]="asImageConfig(widget.config).imageUrl" alt="Image" [style.object-fit]="asImageConfig(widget.config).fit || 'contain'" />
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
      display: flex;
      align-items: center;
      justify-content: center;
      overflow: hidden;
      min-height: 0;
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
