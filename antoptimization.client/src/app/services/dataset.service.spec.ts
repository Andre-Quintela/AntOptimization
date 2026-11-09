import { TestBed, fakeAsync, tick, discardPeriodicTasks } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { DatasetService } from './dataset.service';
import { CreateDatasetJobRequest, DatasetJob } from '../models/dataset.models';

const request: CreateDatasetJobRequest = {
  region: { south: -19.95, west: -43.97, north: -19.9, east: -43.92 },
  pointCounts: [6, 8],
  instancesPerPointCount: 1,
  distanceSource: 'Haversine',
  fixedStart: true,
  repetitions: 2,
  grid: { alpha: [1], beta: [2], numberOfAnts: [10], evaporationRate: [0.5], iterations: [50] }
};

const job = (status: DatasetJob['status']): DatasetJob => ({
  id: 'job-1', name: null, createdAt: '2026-01-01T00:00:00Z', startedAt: null, finishedAt: null,
  status, errorMessage: null, region: request.region, pointCounts: [6, 8], instancesPerPointCount: 1, instanceCount: 2,
  distanceSource: 'Haversine', fixedStart: true, repetitions: 2, masterSeed: 1, grid: request.grid,
  configurationCount: 1, totalRuns: 4, completedRuns: 0, processedInstances: 0, failedInstances: 0, progressPercent: 0
});

describe('DatasetService', () => {
  let service: DatasetService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(DatasetService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('posts the estimate request', () => {
    service.estimate(request).subscribe(result => expect(result.totalRuns).toBe(4));

    const req = httpMock.expectOne('/api/datasets/estimate');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ totalRuns: 4, errors: [], warnings: [], isValid: true });
  });

  it('creates a job', () => {
    service.createJob(request).subscribe(created => expect(created.id).toBe('job-1'));

    const req = httpMock.expectOne('/api/datasets/jobs');
    expect(req.request.method).toBe('POST');
    req.flush(job('Pending'));
  });

  it('uses the expected URLs for queries and commands', () => {
    service.listJobs().subscribe();
    service.getInstances('j').subscribe();
    service.getRuns('j', 'i').subscribe();
    service.getConfigSummaries('j').subscribe();
    service.cancelJob('j').subscribe();
    service.resumeJob('j').subscribe();
    service.deleteJob('j').subscribe();

    httpMock.expectOne({ method: 'GET', url: '/api/datasets/jobs' }).flush([]);
    httpMock.expectOne({ method: 'GET', url: '/api/datasets/jobs/j/instances' }).flush([]);
    httpMock.expectOne({ method: 'GET', url: '/api/datasets/jobs/j/instances/i/runs' }).flush([]);
    httpMock.expectOne({ method: 'GET', url: '/api/datasets/jobs/j/configs' }).flush([]);
    httpMock.expectOne({ method: 'POST', url: '/api/datasets/jobs/j/cancel' }).flush(null);
    httpMock.expectOne({ method: 'POST', url: '/api/datasets/jobs/j/resume' }).flush(null);
    httpMock.expectOne({ method: 'DELETE', url: '/api/datasets/jobs/j' }).flush(null);
    expect().nothing(); // expectations are the expectOne calls verified above
  });

  it('polls while the job is active and stops after the terminal state', fakeAsync(() => {
    const statuses: string[] = [];
    let completed = false;
    service.pollJob('job-1', 1000).subscribe({
      next: j => statuses.push(j.status),
      complete: () => completed = true
    });

    tick(0);
    httpMock.expectOne('/api/datasets/jobs/job-1').flush(job('Running'));
    tick(1000);
    httpMock.expectOne('/api/datasets/jobs/job-1').flush(job('Completed'));
    tick(1000);
    httpMock.expectNone('/api/datasets/jobs/job-1');

    expect(statuses).toEqual(['Running', 'Completed']);
    expect(completed).toBeTrue();
    discardPeriodicTasks();
  }));

  it('downloads an export with the server file name', () => {
    const anchor = document.createElement('a');
    spyOn(anchor, 'click');
    spyOn(document, 'createElement').and.returnValue(anchor);
    spyOn(URL, 'createObjectURL').and.returnValue('blob:x');
    spyOn(URL, 'revokeObjectURL');

    service.download('job-1', 'Runs').subscribe();

    const req = httpMock.expectOne(r => r.url === '/api/datasets/jobs/job-1/export');
    expect(req.request.params.get('level')).toBe('Runs');
    req.flush(new Blob(['a,b']), { headers: { 'Content-Disposition': 'attachment; filename="dataset_runs.csv"' } });

    expect(anchor.download).toBe('dataset_runs.csv');
    expect(anchor.click).toHaveBeenCalled();
  });

  it('downloads all jobs from the global export endpoint', () => {
    const anchor = document.createElement('a');
    spyOn(anchor, 'click');
    spyOn(document, 'createElement').and.returnValue(anchor);
    spyOn(URL, 'createObjectURL').and.returnValue('blob:x');
    spyOn(URL, 'revokeObjectURL');

    service.download(null, 'Zip').subscribe();

    const req = httpMock.expectOne(r => r.url === '/api/datasets/export');
    expect(req.request.params.get('level')).toBe('Zip');
    req.flush(new Blob(['zip']));
    expect(anchor.download).toBe('dataset_zip.zip');
  });
});
