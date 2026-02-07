import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { WidgetConfig, ColorScheme, HassEntityState, RssFeedConfig, DashboardLayout } from '../../models/types';
import QRCode from 'qrcode';

interface RssEntry {
  title: string;
  link: string;
  published?: string;
  summary?: string;
}

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
      [style.--iconColor]="getIconColor()"
      [style.--titleColor]="getTitleColor()"
      [style.--textColor]="getTextColor()"
      [style.--qrCodeDarkColor]="getTextColor()"
      [style.--qrCodeLightColor]="getQrCodeBackgroundColor()"
      [style.color]="getTextColor()">
      @if (!isDataFetched()) {
        <div class="preview-state">
          <i class="fa fa-rss"></i>
          <p>RSS Feed</p>
        </div>
      }
      @if (isDataFetched()) {
        <div class="rss-feed-content">
          @if (widget.titleOverride || config.title) {
            <h3 class="feed-title">{{ widget.titleOverride || config.title }}</h3>
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
          } @else {
            <div class="empty-state">
              <i class="fa fa-rss"></i>
              <p>No RSS entries found</p>
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
  @Input() entityStates: Record<string, HassEntityState> | null = null;
  @Input() designerSettings?: DashboardLayout;

  qrCodeDataUrl: string | null = null;

  get config(): RssFeedConfig {
    return (this.widget?.config || {}) as RssFeedConfig;
  }

  ngOnInit(): void {
    this.generateQRCode();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['entityStates'] || changes['widget']) {
      this.generateQRCode();
    }
  }

  getTitleFontSize(): number {
    return this.designerSettings?.titleFontSize ?? 16;
  }

  getTextFontSize(): number {
    return this.designerSettings?.textFontSize ?? 12;
  }

  /**
   * Checks if RSS feed data has been fetched for the configured entity.
   */
  isDataFetched(): boolean {
    const entityId = this.config.entityId;
    if (!entityId) return false;

    const state = this.getEntityState(entityId);
    if (!state || !state.attributes) return false;

    // Check if we have RSS entry data in attributes
    const attrs = state.attributes;
    return !!(attrs['title'] || attrs['link'] || attrs['description']);
  }

  getIconColor(): string {
    return this.widget.colorOverrides?.iconColor ||
      this.colorScheme.iconColor ||
      this.colorScheme.accent;
  }

  getTitleColor(): string {
    return this.widget.colorOverrides?.widgetTitleTextColor ||
      this.colorScheme.widgetTitleTextColor ||
      this.colorScheme.text;
  }

  getTextColor(): string {
    return this.widget.colorOverrides?.widgetTextColor ||
      this.colorScheme.widgetTextColor ||
      this.colorScheme.text;
  }

  getQrCodeBackgroundColor(): string {
    return this.widget.colorOverrides?.widgetBackgroundColor ||
      this.colorScheme.widgetBackgroundColor ||
      this.colorScheme.background;
  }

  getEntityState(entityId?: string): HassEntityState | null {
    if (!entityId || !this.entityStates) return null;
    return this.entityStates[entityId] ?? null;
  }

  getRssEntries(entityId?: string): RssEntry[] {
    if (!entityId) return [];

    const state = this.getEntityState(entityId);
    if (!state) return [];

    const attrs = state.attributes || {};

    // Home Assistant feedreader event entities store the latest entry data directly in attributes
    // with keys: title, link, description, content
    // We return a single-item array since event entities only store the latest entry
    const title = attrs['title'];
    const link = attrs['link'];

    if (!title && !link) {
      return [];
    }

    return [{
      title: (title || 'No Title') as string,
      link: (link || '') as string,
      published: attrs['published'] as string | undefined,
      summary: (attrs['description'] || attrs['summary'] || attrs['content']) as string | undefined
    }];
  }

  getCurrentEntry(): RssEntry | null {
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
