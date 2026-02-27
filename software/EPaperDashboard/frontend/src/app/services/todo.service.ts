import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';

export interface TodoItem {
  summary: string;
  status: string;
  uid: string;
}

@Injectable({
  providedIn: 'root'
})
export class TodoService {
  private readonly http = inject(HttpClient);

  getTodoItems(dashboardId: string, todoEntityId: string): Observable<TodoItem[]> {
    return this.http.get<{ data: TodoItem[] }>(`/api/dashboards/${dashboardId}/todo-items/${todoEntityId}`).pipe(
      map(response => response.data || [])
    );
  }
}
