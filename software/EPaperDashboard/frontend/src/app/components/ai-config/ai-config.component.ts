import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AiService } from '../../services/ai.service';
import { ToastService } from '../../services/toast.service';
import { AiConfig, AiConnectionMode, ConversationAgent } from '../../models/types';

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
  readonly conversationAgents = signal<ConversationAgent[]>([]);
  readonly isLoadingAgents = signal(false);

  ngOnInit(): void {
    this.loadConfig();
  }

  loadConfig(): void {
    this.isLoading.set(true);
    this.aiService.getConfig().subscribe({
      next: (config) => {
        this.aiConfig.set(config);
        this.isLoading.set(false);
        if (config.connectionMode === 'HomeAssistant') {
          this.loadConversationAgents();
        }
      },
      error: () => {
        this.isLoading.set(false);
      }
    });
  }

  onConnectionModeChange(mode: AiConnectionMode): void {
    this.aiConfig.update(c => ({ ...c, connectionMode: mode }));
    if (mode === 'HomeAssistant') {
      this.loadConversationAgents();
    }
  }

  loadConversationAgents(): void {
    this.isLoadingAgents.set(true);
    this.aiService.getConversationAgents().subscribe({
      next: (agents) => {
        this.conversationAgents.set(agents);
        this.isLoadingAgents.set(false);
      },
      error: () => {
        this.conversationAgents.set([]);
        this.isLoadingAgents.set(false);
      }
    });
  }

  updateField(field: keyof AiConfig, value: string): void {
    this.aiConfig.update(c => ({ ...c, [field]: value }));
  }

  save(): void {
    this.toastService.clear();
    this.isSaving.set(true);
    this.aiService.updateConfig(this.aiConfig()).subscribe({
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
