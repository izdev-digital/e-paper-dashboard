import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LlmService, LlmConfig, UpdateLlmConfigRequest } from '../../services/llm.service';
import { ToastService } from '../../services/toast.service';

@Component({
  selector: 'app-ai-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './ai-settings.component.html'
})
export class AiSettingsComponent implements OnInit {
  private readonly llmService = inject(LlmService);
  private readonly toastService = inject(ToastService);

  readonly isLoading = signal(false);
  readonly isSaving = signal(false);
  readonly isTesting = signal(false);
  readonly testResult = signal<{ success: boolean; message: string } | null>(null);

  // Form fields
  enabled = false;
  providerType: string = 'none';
  baseUrl = '';
  model = '';
  apiKey = '';
  clearApiKey = false;
  hasExistingApiKey = false;
  temperature = 0.1;
  timeoutSeconds = 60;

  readonly providerTypes = [
    { value: 'ollama', label: 'Ollama' },
    { value: 'openai', label: 'OpenAI-compatible (OpenAI, LocalAI, LM Studio, vLLM…)' },
  ];

  get isNonLocalUrl(): boolean {
    if (!this.baseUrl) return false;
    try {
      const url = new URL(this.baseUrl);
      const host = url.hostname;
      // Allow known loopback and private ranges
      if (host === 'localhost') return false;
      // IPv6 loopback
      if (host === '::1' || host === '[::1]') return false;
      // 127.0.0.0/8 loopback
      if (/^127\./.test(host)) return false;
      // 10.0.0.0/8
      if (/^10\./.test(host)) return false;
      // 172.16.0.0/12 (172.16–172.31)
      if (/^172\.(1[6-9]|2\d|3[01])\./.test(host)) return false;
      // 192.168.0.0/16
      if (/^192\.168\./.test(host)) return false;
      // 169.254.0.0/16 link-local
      if (/^169\.254\./.test(host)) return false;
      return true;
    } catch {
      return false;
    }
  }

  ngOnInit(): void {
    this.loadConfig();
  }

  private loadConfig(): void {
    this.isLoading.set(true);
    this.llmService.getConfig().subscribe({
      next: (config: LlmConfig) => {
        this.enabled = config.enabled;
        this.providerType = config.providerType;
        this.baseUrl = config.baseUrl;
        this.model = config.model;
        this.hasExistingApiKey = config.hasApiKey;
        this.temperature = config.temperature;
        this.timeoutSeconds = config.timeoutSeconds;
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      }
    });
  }

  save(): void {
    this.isSaving.set(true);
    this.testResult.set(null);

    const request: UpdateLlmConfigRequest = {
      enabled: this.enabled,
      providerType: this.providerType,
      baseUrl: this.baseUrl,
      model: this.model,
      temperature: this.temperature,
      timeoutSeconds: this.timeoutSeconds,
    };

    if (this.apiKey) {
      request.apiKey = this.apiKey;
    } else if (this.clearApiKey) {
      request.clearApiKey = true;
    }

    this.llmService.saveConfig(request).subscribe({
      next: (updated: LlmConfig) => {
        this.hasExistingApiKey = updated.hasApiKey;
        this.apiKey = '';
        this.clearApiKey = false;
        this.toastService.show('AI settings saved.', 'success');
        this.isSaving.set(false);
      },
      error: () => {
        this.toastService.show('Failed to save AI settings.', 'error');
        this.isSaving.set(false);
      }
    });
  }

  testConnection(): void {
    this.isTesting.set(true);
    this.testResult.set(null);

    this.llmService.testConnection().subscribe({
      next: (result) => {
        this.testResult.set(result);
        this.isTesting.set(false);
      },
      error: (err) => {
        const message = err?.error?.message ?? 'Connection test failed.';
        this.testResult.set({ success: false, message });
        this.isTesting.set(false);
      }
    });
  }
}
