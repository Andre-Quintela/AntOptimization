import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map, switchMap, takeWhile, timer } from 'rxjs';
import {
  AcoRun,
  ACTIVE_JOB_STATUSES,
  ConfigSummary,
  CreateDatasetJobRequest,
  DatasetEstimate,
  DatasetExportLevel,
  DatasetInstance,
  DatasetJob
} from '../models/dataset.models';

@Injectable({ providedIn: 'root' })
export class DatasetService {
  private readonly apiUrl = '/api/datasets';

  constructor(private http: HttpClient) {}

  estimate(request: CreateDatasetJobRequest): Observable<DatasetEstimate> {
    return this.http.post<DatasetEstimate>(`${this.apiUrl}/estimate`, request);
  }

  createJob(request: CreateDatasetJobRequest): Observable<DatasetJob> {
    return this.http.post<DatasetJob>(`${this.apiUrl}/jobs`, request);
  }

  listJobs(): Observable<DatasetJob[]> {
    return this.http.get<DatasetJob[]>(`${this.apiUrl}/jobs`);
  }

  getJob(id: string): Observable<DatasetJob> {
    return this.http.get<DatasetJob>(`${this.apiUrl}/jobs/${id}`);
  }

  getInstances(jobId: string): Observable<DatasetInstance[]> {
    return this.http.get<DatasetInstance[]>(`${this.apiUrl}/jobs/${jobId}/instances`);
  }

  getRuns(jobId: string, instanceId: string): Observable<AcoRun[]> {
    return this.http.get<AcoRun[]>(`${this.apiUrl}/jobs/${jobId}/instances/${instanceId}/runs`);
  }

  getConfigSummaries(jobId: string): Observable<ConfigSummary[]> {
    return this.http.get<ConfigSummary[]>(`${this.apiUrl}/jobs/${jobId}/configs`);
  }

  cancelJob(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/jobs/${id}/cancel`, {});
  }

  resumeJob(id: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/jobs/${id}/resume`, {});
  }

  deleteJob(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/jobs/${id}`);
  }

  /** Emits the job every `intervalMs` while it is active, including the final (terminal) state. */
  pollJob(id: string, intervalMs = 1500): Observable<DatasetJob> {
    return timer(0, intervalMs).pipe(
      switchMap(() => this.getJob(id)),
      takeWhile(job => ACTIVE_JOB_STATUSES.includes(job.status), true)
    );
  }

  /** Downloads an export (CSV or ZIP) of one job, or of all jobs when `jobId` is null. */
  download(jobId: string | null, level: DatasetExportLevel): Observable<void> {
    const url = jobId ? `${this.apiUrl}/jobs/${jobId}/export` : `${this.apiUrl}/export`;
    const params = new HttpParams().set('level', level);
    return this.http.get(url, { params, responseType: 'blob', observe: 'response' }).pipe(
      map(response => {
        const disposition = response.headers.get('Content-Disposition') ?? '';
        const match = /filename="?([^";]+)"?/.exec(disposition);
        const fallback = `dataset_${level.toLowerCase()}.${level === 'Zip' ? 'zip' : 'csv'}`;
        this.saveBlob(response.body!, match?.[1] ?? fallback);
      })
    );
  }

  private saveBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
