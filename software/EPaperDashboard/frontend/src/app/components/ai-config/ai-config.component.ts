import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AiService } from '../../services/ai.service';
import { ToastService } from '../../services/toast.service';
import { AiConfig, AiConnectionMode } from '../../models/types';

@Component({
  selector: 'app-ai-config',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './ai-config.component.html'
})
export class AiConfigComponent implements OnInit {
  private readonly aiService = inject(AiService);
  private readonly toastService = inject(ToastService);

  readonly aiConfig = signal<AiConfig>({ connectionMode: 'None' });
  readonly isLoading = signal(false);
  readonly isSaving = signal(false);
  readonly availableModels = signal<{ id: string }[]>([]);
  readonly isLoadingModels = signal(false);

  ngOnInit(): void {
    this.loadConfig();
  }

  loadConfig(): void {
    this.isLoading.set(true);
    this.aiService.getGlobalConfig().subscribe({
      next: (config) => {
        this.aiConfig.set(config);
        this.isLoading.set(false);
        if (config.connectionMode === 'Direct' && config.directEndpoint) {
          this.fetchModels();
        }
      },
      error: () => {
        this.isLoading.set(false);
      }
    });
  }

  onConnectionModeChange(mode: AiConnectionMode): void {
    this.aiConfig.update(c => ({ ...c, connectionMode: mode }));
    if (mode === 'Direct') {
      const endpoint = this.aiConfig().directEndpoint;
      if (endpoint) {
        this.fetchModels();
      }
    }
  }

  updateField(field: keyof AiConfig, value: string): void {
    this.aiConfig.update(c => ({ ...c, [field]: value }));
  }

  fetchModels(): void {
    const config = this.aiConfig();
    if (!config.directEndpoint) {
      return;
    }
    this.isLoadingModels.set(true);
    this.aiService.getAvailableModels(config.directEndpoint, config.directApiKey ?? undefined).subscribe({
      next: (models) => {
        this.availableModels.set(models);
        this.isLoadingModels.set(false);
      },
      error: () => {
        this.availableModels.set([]);
        this.isLoadingModels.set(false);
      }
    });
  }

  save(): void {
    this.toastService.clear();
    this.isSaving.set(true);
    this.aiService.updateGlobalConfig(this.aiConfig()).subscribe({
      next: (config) => {
        this.aiConfig.set(config);
        this.toastService.success('AI configuration saved.');
        this.isSaving.set(false);
      },
      error: (err) => {
        this.toastService.error(err.error?.message || 'Failed to save AI configuration.');
        this.isSaving.set(false);
      }
    });
  }
}
