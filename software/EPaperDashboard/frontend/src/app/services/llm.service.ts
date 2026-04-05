import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface LlmConfig {
  enabled: boolean;
  providerType: 'none' | 'ollama' | 'openai';
  baseUrl: string;
  model: string;
  hasApiKey: boolean;
  temperature: number;
  timeoutSeconds: number;
}

export interface UpdateLlmConfigRequest {
  enabled: boolean;
  providerType: string;
  baseUrl: string;
  model: string;
  apiKey?: string;
  clearApiKey?: boolean;
  temperature: number;
  timeoutSeconds: number;
}

export interface TestConnectionResult {
  success: boolean;
  message: string;
}

@Injectable({
  providedIn: 'root'
})
export class LlmService {
  private readonly http = inject(HttpClient);

  getConfig(): Observable<LlmConfig> {
    return this.http.get<LlmConfig>('/api/llm/config');
  }

  saveConfig(request: UpdateLlmConfigRequest): Observable<LlmConfig> {
    return this.http.put<LlmConfig>('/api/llm/config', request);
  }

  testConnection(): Observable<TestConnectionResult> {
    return this.http.post<TestConnectionResult>('/api/llm/test-connection', {});
  }
}
