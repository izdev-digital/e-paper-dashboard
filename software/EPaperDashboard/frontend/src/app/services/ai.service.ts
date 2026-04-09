import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AiConfig, AiGenerationResult, ConversationAgent } from '../models/types';

@Injectable({
  providedIn: 'root'
})
export class AiService {
  private readonly http = inject(HttpClient);

  getConfig(): Observable<AiConfig> {
    return this.http.get<AiConfig>('/api/ai/config');
  }

  updateConfig(config: AiConfig): Observable<AiConfig> {
    return this.http.put<AiConfig>('/api/ai/config', config);
  }

  getConversationAgents(): Observable<ConversationAgent[]> {
    return this.http.get<ConversationAgent[]>('/api/ai/conversation-agents');
  }

  generateDashboard(dashboardId: string): Observable<AiGenerationResult> {
    return this.http.post<AiGenerationResult>(`/api/ai/dashboards/${dashboardId}/generate`, {});
  }

  getGeneratedWidgets(dashboardId: string): Observable<AiGenerationResult> {
    return this.http.get<AiGenerationResult>(`/api/ai/dashboards/${dashboardId}/generated`);
  }

  clearGeneratedWidgets(dashboardId: string): Observable<void> {
    return this.http.delete<void>(`/api/ai/dashboards/${dashboardId}/generated`);
  }
}
