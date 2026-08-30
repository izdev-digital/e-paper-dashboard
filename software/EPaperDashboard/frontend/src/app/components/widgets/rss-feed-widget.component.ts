import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, RssFeedConfig, DashboardLayout } from '../../models/types';
import type { RssFeedEntryData } from '../../services/dashboard-preview-data.service';
import QRCode from 'qrcode';
import { resolveWidgetRenderContext } from './widget-render-context';

@Component({
  selector: 'app-widget-rss-feed',
  standalone: true,
  imports: [CommonModule],
  styleUrls: ['./rss-feed-widget.component.scss'],
  template: `
    <div 
      class="rss-feed-widget" 
      [style.--titleFontSize]="getTitleFontSize() + 'px'"
      [style.--textFontSize]="getTextFontSize() + 'px'"
      [style.--titleFontWeight]="getTitleFontWeight()"
      [style.--textFontWeight]="getTextFontWeight()"
      [style.--iconColor]="getIconColor()"
      [style.--titleColor]="getTitleColor()"
      [style.--textColor]="getTextColor()"
      [style.--qrCodeDarkColor]="getTextColor()"
      [style.--qrCodeLightColor]="getQrCodeBackgroundColor()"
      [style.--widget-title-font-size]="getTitleFontSize() + 'px'"
      [style.--widget-title-font-weight]="getTitleFontWeight()"
      [style.--widget-title-color]="getTitleColor()"
      [style.color]="getTextColor()">
      @if (!isDataFetched()) {
        <div class="preview-state">
          <i class="fa fa-rss"></i>
          <p>RSS Feed</p>
        </div>
      }
      @if (isDataFetched()) {
        <div class="rss-feed-content">
          @if (widget.showTitle !== false && (widget.titleOverride || config.title)) {
            <h3 class="widget-frame-title">{{ widget.titleOverride || config.title }}</h3>
          }
          @if (getCurrentEntry()) {
            <div class="rss-entry">
              <div class="entry-title-container">
                <h4 class="entry-title">{{ getCurrentEntry()?.title || 'No Title' }}</h4>
              </div>
              @if (qrCodeDataUrl) {
                <div class="qr-code-container">
                  <img [src]="qrCodeDataUrl" alt="QR Code" class="qr-code" />
                </div>
              }
            </div>
          }
        </div>
      }
    </div>
  `
})
export class RssFeedWidgetComponent implements OnInit, OnChanges {
  @Input() widget!: WidgetConfig;
  @Input() colorScheme!: ColorScheme;
  @Input() rssFeedEntriesByEntityId?: Record<string, RssFeedEntryData[]>;
  @Input() designerSettings?: DashboardLayout;

  qrCodeDataUrl: string | null = null;

  get config(): RssFeedConfig {
    return (this.widget?.config || {}) as RssFeedConfig;
  }

  ngOnInit(): void {
    this.generateQRCode();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['rssFeedEntriesByEntityId'] || changes['widget'] || changes['colorScheme'] || changes['designerSettings']) {
      this.generateQRCode();
    }
  }

  getTitleFontSize(): number {
    return this.renderContext.titleFontSize;
  }

  getTextFontSize(): number {
    return this.renderContext.textFontSize;
  }

  getTitleFontWeight(): number {
    return this.renderContext.titleFontWeight;
  }

  getTextFontWeight(): number {
    return this.renderContext.textFontWeight;
  }

  /**
   * Checks if RSS feed data has been fetched for the configured entity.
   */
  isDataFetched(): boolean {
    const entityId = this.config.entityId;
    if (!entityId) return false;

    return !!this.rssFeedEntriesByEntityId && entityId in this.rssFeedEntriesByEntityId;
  }

  getIconColor(): string {
    return this.renderContext.iconColor;
  }

  getTitleColor(): string {
    return this.renderContext.titleColor;
  }

  getTextColor(): string {
    return this.renderContext.textColor;
  }

  getQrCodeBackgroundColor(): string {
    return this.renderContext.backgroundColor;
  }

  private get renderContext() {
    return resolveWidgetRenderContext(this.widget, this.colorScheme, this.designerSettings);
  }

  getRssEntries(entityId?: string): RssFeedEntryData[] {
    if (!entityId || !this.rssFeedEntriesByEntityId) return [];
    return this.rssFeedEntriesByEntityId[entityId] ?? [];
  }

  getCurrentEntry(): RssFeedEntryData | null {
    const entries = this.getRssEntries(this.config.entityId);
    if (entries.length === 0) return null;

    // Return the first (most recent) entry
    return entries[0];
  }

  async generateQRCode(): Promise<void> {
    const entry = this.getCurrentEntry();

    if (!entry || !entry.link) {
      this.qrCodeDataUrl = null;
      return;
    }

    try {
      const qrDataUrl = await QRCode.toDataURL(entry.link, {
        width: 200,
        margin: 1,
        color: {
          dark: this.colorScheme.text,
          light: this.getQrCodeBackgroundColor()
        }
      });

      this.qrCodeDataUrl = qrDataUrl;
    } catch (error) {
      this.qrCodeDataUrl = null;
    }
  }
}
