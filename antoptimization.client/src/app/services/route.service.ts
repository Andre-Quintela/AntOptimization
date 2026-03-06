import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { OptimizationRequest, OptimizationResponse } from '../models/route.models';

@Injectable({
  providedIn: 'root'
})
export class RouteService {
  private readonly apiUrl = '/api/routes';

  constructor(private http: HttpClient) {}

  optimizeRoute(request: OptimizationRequest): Observable<OptimizationResponse> {
    return this.http.post<OptimizationResponse>(`${this.apiUrl}/optimize`, request);
  }
}
