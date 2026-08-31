import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, ImageConfig, DashboardLayout } from '../../models/types';
import { ResolveUrlPipe } from '../../pipes/resolve-url.pipe';
import { resolveWidgetRenderContext } from './widget-render-context';

@Component({
  selector: 'app-widget-image',
  standalone: true,
  imports: [CommonModule, ResolveUrlPipe],
  template: `
    <div class="image-widget-wrapper"
      [style.--widget-title-font-size]="renderContext.titleFontSize + 'px'"
      [style.--widget-title-font-weight]="renderContext.titleFontWeight"
      [style.--widget-title-color]="renderContext.titleColor">
      @if (widget.showTitle !== false && widget.titleOverride) {
        <h4 class="widget-frame-title">{{ widget.titleOverride }}</h4>
      }
      <div class="image-widget-container">
        <img
          [src]="cfg.imageUrl | resolveUrl"
          alt="Image"
          [style.width.%]="(cfg.zoom ?? 1) * 100"
          [style.height.%]="(cfg.zoom ?? 1) * 100"
          [style.left.%]="-((cfg.zoom ?? 1) - 1) * ((cfg.offsetX ?? 0) + 1) * 50"
          [style.top.%]="-((cfg.zoom ?? 1) - 1) * ((cfg.offsetY ?? 0) + 1) * 50"
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
  @Input() designerSettings?: DashboardLayout;

  get cfg(): ImageConfig {
    return this.widget.config as ImageConfig;
  }

  get renderContext() {
    return resolveWidgetRenderContext(this.widget, this.colorScheme, this.designerSettings);
  }
}
