import { DOCUMENT } from '@angular/common';
import { inject, Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class ClipboardService {
  private readonly document = inject(DOCUMENT);

  async copy(text: string): Promise<boolean> {
    const win = this.document.defaultView;

    // Only use the async Clipboard API in a secure context (HTTPS / localhost).
    // Attempting it over plain HTTP and awaiting the rejection loses the
    // user-gesture context, which prevents the synchronous fallback from working.
    if (win?.isSecureContext && win.navigator?.clipboard?.writeText) {
      try {
        await win.navigator.clipboard.writeText(text);
        return true;
      } catch {
        // Fall through to fallback
      }
    }

    // Synchronous fallback — must run inside the original user gesture
    return this.copyViaSelection(text);
  }

  private copyViaSelection(text: string): boolean {
    const textarea = this.document.createElement('textarea');
    textarea.value = text;

    // Prevent scrolling and keep offscreen, but do NOT set opacity/visibility
    // — some browsers skip copy for hidden elements
    textarea.style.position = 'fixed';
    textarea.style.left = '0';
    textarea.style.top = '0';
    textarea.style.width = '1px';
    textarea.style.height = '1px';
    textarea.style.padding = '0';
    textarea.style.border = 'none';
    textarea.style.outline = 'none';

    this.document.body.appendChild(textarea);
    textarea.focus();
    textarea.select();

    let success = false;
    try {
      success = this.document.execCommand('copy');
    } catch {
      // execCommand can throw in some browsers
    }

    this.document.body.removeChild(textarea);
    return success;
  }
}
