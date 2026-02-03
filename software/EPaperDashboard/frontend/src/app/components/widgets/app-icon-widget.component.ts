import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme } from '../../models/types';

@Component({
  selector: 'app-widget-app-icon',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./app-icon-widget.component.scss'],
  template: `
    @if (inlineSvg) {
      <div class="app-icon"
        [innerHTML]="inlineSvg"
        [style.width.px]="asAppIconConfig(widget.config).size ?? 64"
        [style.height.px]="asAppIconConfig(widget.config).size ?? 64"
        [style.max-width]="'100%'"
        [style.max-height]="'100%'"
        [style.--accent-color]="getIconColor()"></div>
    }
  `
})
export class AppIconWidgetComponent implements OnInit, OnChanges {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;

  inlineSvg: SafeHtml | null = null;

  constructor(private sanitizer: DomSanitizer) {}

  ngOnInit(): void {
    this.loadSvg();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['widget'] || changes['colorScheme']) {
      this.loadSvg();
    }
  }

  private async loadSvg(): Promise<void> {
    try {
      const response = await fetch('/icon-tab-dynamic.svg');
      if (response.ok) {
        let svgText = await response.text();
        // Replace the default accent color in the SVG to prevent flicker
        const accentColor = this.getIconColor();
        svgText = svgText.replace(/--accent-color: #[0-9a-f]{6};/i, `--accent-color: ${accentColor};`);
        this.inlineSvg = this.sanitizer.bypassSecurityTrustHtml(svgText);
      }
    } catch (error) {
      console.error('Failed to load SVG:', error);
    }
  }

  asAppIconConfig(config: any) { return config as any; }

  getIconColor(): string {
    if (this.widget.colorOverrides?.iconColor) {
      return this.widget.colorOverrides.iconColor;
    }
    return this.colorScheme?.iconColor || this.colorScheme?.accent || 'currentColor';
  }
}
