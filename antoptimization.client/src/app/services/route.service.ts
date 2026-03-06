import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { OptimizationRequest, OptimizationResponse, VisualEvent } from '../models/route.models';

@Injectable({
  providedIn: 'root'
})
export class RouteService {
  private readonly apiUrl = '/api/routes';

  constructor(private http: HttpClient) {}

  optimizeRoute(request: OptimizationRequest): Observable<OptimizationResponse> {
    return this.http.post<OptimizationResponse>(`${this.apiUrl}/optimize`, request);
  }

  optimizeRouteVisual(request: OptimizationRequest): Observable<VisualEvent> {
    return new Observable(observer => {
      const abortController = new AbortController();

      fetch(`${this.apiUrl}/optimize-visual`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
        signal: abortController.signal
      }).then(async response => {
        if (!response.ok) {
          observer.error(new Error(`HTTP ${response.status}`));
          return;
        }

        const reader = response.body!.getReader();
        const decoder = new TextDecoder();
        let buffer = '';

        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          buffer += decoder.decode(value, { stream: true });
          const parts = buffer.split('\n\n');
          buffer = parts.pop() || '';

          for (const part of parts) {
            const dataLine = part.trim();
            if (dataLine.startsWith('data: ')) {
              try {
                observer.next(JSON.parse(dataLine.substring(6)));
              } catch { }
            }
          }
        }

        observer.complete();
      }).catch(err => {
        if (err.name !== 'AbortError') {
          observer.error(err);
        }
      });

      return () => abortController.abort();
    });
  }
}
