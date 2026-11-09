import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { RouterTestingModule } from '@angular/router/testing';
import { of, throwError } from 'rxjs';
import * as L from 'leaflet';
import { DatasetPageComponent } from './dataset-page.component';
import { DatasetService } from '../../services/dataset.service';
import { MapViewComponent } from '../../components/map-view/map-view.component';
import { DatasetInstance, DatasetJob } from '../../models/dataset.models';

const layerStub = () => {
  const stub: Record<string, jasmine.Spy> = {
    addTo: jasmine.createSpy('addTo'),
    remove: jasmine.createSpy('remove'),
    setBounds: jasmine.createSpy('setBounds'),
    bindPopup: jasmine.createSpy('bindPopup')
  };
  Object.values(stub).forEach(spy => spy.and.returnValue(stub));
  return stub;
};

const job = (status: DatasetJob['status'], id = 'job-1'): DatasetJob => ({
  id, name: 'teste', createdAt: '2026-01-01T00:00:00Z', startedAt: null, finishedAt: null,
  status, errorMessage: null, region: { south: -20, west: -44, north: -19.9, east: -43.9 },
  pointCounts: [3], instancesPerPointCount: 1, instanceCount: 1, distanceSource: 'Haversine', fixedStart: true, repetitions: 1,
  masterSeed: 1, grid: { alpha: [1], beta: [2], numberOfAnts: [5], evaporationRate: [0.5], iterations: [10] },
  configurationCount: 1, totalRuns: 1, completedRuns: 1, processedInstances: 1, failedInstances: 0, progressPercent: 100
});

const instance: DatasetInstance = {
  id: 'inst-1', index: 0, pointCount: 3, seed: 7, status: 'Completed', errorMessage: null, generationAttempts: 1,
  coordinates: [{ lat: -19.95, lng: -43.95 }, { lat: -19.94, lng: -43.94 }, { lat: -19.93, lng: -43.96 }],
  fixedStartIndex: 0, features: null, optimalDistanceM: 1000, optimalTour: [0, 2, 1],
  exactMethod: 'BruteForce', exactTimeMs: 0.1, exactOperations: 3, runCount: 1, bestGapPercent: 0, meanGapPercent: 0
};

describe('DatasetPageComponent', () => {
  let fixture: ComponentFixture<DatasetPageComponent>;
  let component: DatasetPageComponent;
  let datasetService: jasmine.SpyObj<DatasetService>;
  let mapStub: Record<string, jasmine.Spy>;

  beforeEach(async () => {
    datasetService = jasmine.createSpyObj('DatasetService', [
      'listJobs', 'createJob', 'pollJob', 'getInstances', 'getConfigSummaries', 'getRuns',
      'getJob', 'cancelJob', 'resumeJob', 'deleteJob', 'download'
    ]);
    datasetService.listJobs.and.returnValue(of([]));
    datasetService.pollJob.and.returnValue(of(job('Completed')));
    datasetService.getInstances.and.returnValue(of([instance]));
    datasetService.getConfigSummaries.and.returnValue(of([]));
    datasetService.getRuns.and.returnValue(of([]));

    mapStub = {
      on: jasmine.createSpy('on'),
      fitBounds: jasmine.createSpy('fitBounds'),
      getBounds: jasmine.createSpy('getBounds').and.returnValue({
        getSouthWest: () => L.latLng(-20, -44),
        getNorthEast: () => L.latLng(-19.9, -43.9)
      })
    };
    const mapView = jasmine.createSpyObj<MapViewComponent>('MapViewComponent', ['getMap', 'invalidateSize']);
    mapView.getMap.and.returnValue(mapStub as unknown as L.Map);

    spyOn(L, 'rectangle').and.callFake(() => layerStub() as unknown as L.Rectangle);
    spyOn(L, 'circleMarker').and.callFake(() => layerStub() as unknown as L.CircleMarker);
    (spyOn(L, 'polyline') as jasmine.Spy).and.callFake(() => layerStub() as unknown as L.Polyline);
    spyOn(L, 'marker').and.callFake(() => layerStub() as unknown as L.Marker);
    spyOn(L, 'layerGroup').and.callFake(() => layerStub() as unknown as L.LayerGroup);

    await TestBed.configureTestingModule({
      imports: [RouterTestingModule],
      declarations: [DatasetPageComponent],
      providers: [{ provide: DatasetService, useValue: datasetService }],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();

    fixture = TestBed.createComponent(DatasetPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    (component as unknown as { mapView: MapViewComponent }).mapView = mapView;
  });

  it('loads the job history on init', () => {
    expect(datasetService.listJobs).toHaveBeenCalled();
  });

  it('selects a region with two map clicks, normalizing the corners', () => {
    component.startDrawing();
    component.onMapClick({ lat: -19.9, lng: -43.9 });
    expect(component.drawState).toBe('second-corner');

    component.onMapClick({ lat: -20, lng: -44 });

    expect(component.drawState).toBe('idle');
    expect(component.region).toEqual({ south: -20, west: -44, north: -19.9, east: -43.9 });
    expect(component.regionWidthKm).toBeGreaterThan(10);
    expect(component.regionHeightKm).toBeCloseTo(11.1, 1);
  });

  it('rejects degenerate regions', () => {
    component.startDrawing();
    component.onMapClick({ lat: -19.9, lng: -43.9 });
    component.onMapClick({ lat: -19.9, lng: -43.9 });

    expect(component.region).toBeNull();
    expect(component.errorMessage).toContain('pequena demais');
  });

  it('uses the visible map area as region', () => {
    component.useVisibleArea();
    expect(component.region).toEqual({ south: -20, west: -44, north: -19.9, east: -43.9 });
  });

  it('creates a job, then polls it and loads instances and configurations', () => {
    datasetService.createJob.and.returnValue(of(job('Pending')));

    component.createJob({} as never);

    expect(component.selectedJob?.status).toBe('Completed');
    // Finishing a job that was active reloads the history from the server.
    expect(datasetService.listJobs).toHaveBeenCalledTimes(2);
    expect(datasetService.pollJob).toHaveBeenCalledWith('job-1');
    expect(component.instances).toEqual([instance]);
    expect(datasetService.getConfigSummaries).toHaveBeenCalledWith('job-1');
  });

  it('shows validation errors returned by the API', () => {
    datasetService.createJob.and.returnValue(throwError(() => ({ error: { errors: ['Selecione uma região válida no mapa.'] } })));

    component.createJob({} as never);

    expect(component.errorMessage).toBe('Selecione uma região válida no mapa.');
    expect(component.submitting).toBeFalse();
  });

  it('draws the selected instance and loads its runs', () => {
    component.selectJob(job('Completed'));

    component.selectInstance(instance);

    expect(L.marker).toHaveBeenCalledTimes(3);
    expect(L.polyline).toHaveBeenCalled();
    expect(datasetService.getRuns).toHaveBeenCalledWith('job-1', 'inst-1');
    expect(component.selectedInstanceId).toBe('inst-1');
  });

  it('previews the rectangle while choosing the second corner and can cancel', () => {
    component.startDrawing();
    component.onMapClick({ lat: -19.9, lng: -43.9 });
    const preview = (component as unknown as { previewRegion(p: L.LatLng): void });

    preview.previewRegion(L.latLng(-19.95, -43.95));
    preview.previewRegion(L.latLng(-19.96, -43.96));

    expect(L.rectangle).toHaveBeenCalledTimes(1);
    component.cancelDrawing();
    expect(component.drawState).toBe('idle');
    expect(component.region).toBeNull();
  });

  it('ignores map clicks and previews outside drawing mode', () => {
    component.onMapClick({ lat: -19.9, lng: -43.9 });
    (component as unknown as { previewRegion(p: L.LatLng): void }).previewRegion(L.latLng(-19.9, -43.9));

    expect(component.region).toBeNull();
    expect(L.rectangle).not.toHaveBeenCalled();
    expect(component.regionWidthKm).toBe(0);
    expect(component.regionHeightKm).toBe(0);
  });

  it('clears the selected region', () => {
    component.useVisibleArea();
    component.clearRegion();
    expect(component.region).toBeNull();
  });

  it('reloads the history when switching to the history tab', () => {
    component.switchTab('history');
    expect(component.activeTab).toBe('history');
    expect(datasetService.listJobs).toHaveBeenCalledTimes(2);
  });

  it('reports a failure to load the history', () => {
    datasetService.listJobs.and.returnValue(throwError(() => new Error('down')));
    component.loadJobs();
    expect(component.errorMessage).toContain('histórico');
    expect(component.loadingJobs).toBeFalse();
  });

  it('uses a generic message when job creation fails without details', () => {
    datasetService.createJob.and.returnValue(throwError(() => ({})));
    component.createJob({} as never);
    expect(component.errorMessage).toBe('Falha ao criar o job de geração do dataset.');
  });

  it('keeps polling while active and refreshes configurations only when finished', () => {
    datasetService.pollJob.and.returnValue(of(job('Running')));

    component.selectJob(job('Running'));

    expect(component.selectedJob?.status).toBe('Running');
    expect(datasetService.getConfigSummaries).not.toHaveBeenCalled();
    expect(component.isActive(job('Running'))).toBeTrue();
  });

  it('reports polling failures', () => {
    datasetService.pollJob.and.returnValue(throwError(() => new Error('down')));
    component.selectJob(job('Running'));
    expect(component.errorMessage).toContain('progresso');
  });

  it('resets instance state when switching to another job', () => {
    component.selectJob(job('Completed'));
    component.selectInstance(instance);

    datasetService.pollJob.and.returnValue(of(job('Completed', 'job-2')));
    component.selectJob(job('Completed', 'job-2'));

    expect(component.selectedInstanceId).toBeNull();
    expect(component.runs).toEqual([]);
  });

  it('does not load runs for instances that were not completed', () => {
    component.selectJob(job('Completed'));
    component.selectInstance({ ...instance, status: 'Failed', optimalTour: [] });
    expect(datasetService.getRuns).not.toHaveBeenCalled();
  });

  it('cancels and resumes through the API and re-selects the job', () => {
    component.selectJob(job('Running'));
    datasetService.cancelJob.and.returnValue(of(undefined));
    datasetService.resumeJob.and.returnValue(of(undefined));
    datasetService.getJob.and.returnValue(of(job('Cancelled')));
    datasetService.pollJob.and.returnValue(of(job('Cancelled')));

    component.cancelJob();
    expect(datasetService.cancelJob).toHaveBeenCalledWith('job-1');
    expect(component.selectedJob?.status).toBe('Cancelled');

    component.resumeJob();
    expect(datasetService.resumeJob).toHaveBeenCalledWith('job-1');
    expect(component.busy).toBeFalse();
  });

  it('ignores commands when no job is selected', () => {
    component.cancelJob();
    component.deleteJob();
    expect(datasetService.cancelJob).not.toHaveBeenCalled();
    expect(datasetService.deleteJob).not.toHaveBeenCalled();
  });

  it('deletes the selected job after confirmation', () => {
    component.jobs = [job('Completed')];
    component.selectJob(job('Completed'));
    spyOn(window, 'confirm').and.returnValue(true);
    datasetService.deleteJob.and.returnValue(of(undefined));

    component.deleteJob();

    expect(datasetService.deleteJob).toHaveBeenCalledWith('job-1');
    expect(component.selectedJob).toBeNull();
    expect(component.jobs).toEqual([]);
  });

  it('keeps the job when deletion is not confirmed', () => {
    component.selectJob(job('Completed'));
    spyOn(window, 'confirm').and.returnValue(false);

    component.deleteJob();

    expect(datasetService.deleteJob).not.toHaveBeenCalled();
  });

  it('shows a generic message when deletion fails', () => {
    component.selectJob(job('Completed'));
    spyOn(window, 'confirm').and.returnValue(true);
    datasetService.deleteJob.and.returnValue(throwError(() => ({ error: { title: 'x' } })));

    component.deleteJob();

    expect(component.errorMessage).toBe('Não foi possível concluir a operação.');
  });

  it('downloads exports for the selected job or for all jobs', () => {
    datasetService.download.and.returnValue(of(undefined));
    component.selectJob(job('Completed'));

    component.download('Runs');
    component.download('Zip', null);

    expect(datasetService.download).toHaveBeenCalledWith('job-1', 'Runs');
    expect(datasetService.download).toHaveBeenCalledWith(null, 'Zip');
  });

  it('reports export failures', () => {
    datasetService.download.and.returnValue(throwError(() => new Error('x')));
    component.download('Runs', null);
    expect(component.errorMessage).toContain('exportar');
  });

  it('navigates back to the map', () => {
    const navigate = spyOn((component as unknown as { router: { navigate: (c: string[]) => void } }).router, 'navigate');
    component.goBack();
    expect(navigate).toHaveBeenCalledWith(['/']);
  });

  it('shows the conflict message when a command fails', () => {
    component.selectJob(job('Completed'));
    datasetService.resumeJob.and.returnValue(throwError(() => ({ error: 'Somente jobs interrompidos podem ser retomados.' })));

    component.resumeJob();

    expect(component.errorMessage).toBe('Somente jobs interrompidos podem ser retomados.');
    expect(component.busy).toBeFalse();
  });
});
