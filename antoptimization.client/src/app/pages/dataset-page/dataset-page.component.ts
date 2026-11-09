import { AfterViewInit, ChangeDetectorRef, Component, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { Subject, Subscription } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import * as L from 'leaflet';
import { MapViewComponent } from '../../components/map-view/map-view.component';
import { DatasetService } from '../../services/dataset.service';
import {
  AcoRun,
  ACTIVE_JOB_STATUSES,
  ConfigSummary,
  CreateDatasetJobRequest,
  DatasetExportLevel,
  DatasetInstance,
  DatasetJob,
  RegionDto
} from '../../models/dataset.models';

type PanelTab = 'new' | 'history';
type DrawState = 'idle' | 'first-corner' | 'second-corner';

const EARTH_KM_PER_DEGREE = 111.195;

@Component({
  selector: 'app-dataset-page',
  templateUrl: './dataset-page.component.html',
  styleUrls: ['./dataset-page.component.css']
})
export class DatasetPageComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild(MapViewComponent) private mapView!: MapViewComponent;

  activeTab: PanelTab = 'new';
  drawState: DrawState = 'idle';
  region: RegionDto | null = null;

  jobs: DatasetJob[] = [];
  loadingJobs = false;
  selectedJob: DatasetJob | null = null;
  instances: DatasetInstance[] = [];
  configs: ConfigSummary[] = [];
  selectedInstanceId: string | null = null;
  runs: AcoRun[] = [];

  submitting = false;
  busy = false;
  errorMessage: string | null = null;

  private firstCorner: L.LatLng | null = null;
  private cornerMarker?: L.CircleMarker;
  private regionLayer?: L.Rectangle;
  private jobRegionLayer?: L.Rectangle;
  private instanceLayer?: L.LayerGroup;
  private pollSubscription?: Subscription;
  private readonly destroy$ = new Subject<void>();

  constructor(
    private router: Router,
    private datasetService: DatasetService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadJobs();
  }

  ngAfterViewInit(): void {
    this.mapView?.getMap()?.on('mousemove', (e: L.LeafletMouseEvent) => this.previewRegion(e.latlng));
  }

  ngOnDestroy(): void {
    this.pollSubscription?.unsubscribe();
    this.destroy$.next();
    this.destroy$.complete();
  }

  goBack(): void {
    this.router.navigate(['/']);
  }

  switchTab(tab: PanelTab): void {
    this.activeTab = tab;
    if (tab === 'history') this.loadJobs();
  }

  // ===== Region selection =====

  startDrawing(): void {
    this.drawState = 'first-corner';
    this.firstCorner = null;
    this.cornerMarker?.remove();
  }

  cancelDrawing(): void {
    this.drawState = 'idle';
    this.firstCorner = null;
    this.cornerMarker?.remove();
    this.drawRegion();
  }

  useVisibleArea(): void {
    const bounds = this.mapView.getMap()?.getBounds();
    if (!bounds) return;
    this.drawState = 'idle';
    this.setRegion(bounds.getSouthWest(), bounds.getNorthEast());
  }

  clearRegion(): void {
    this.region = null;
    this.regionLayer?.remove();
    this.regionLayer = undefined;
  }

  onMapClick(point: { lat: number; lng: number }): void {
    const latLng = L.latLng(point.lat, point.lng);
    const map = this.mapView.getMap();

    if (this.drawState === 'first-corner') {
      this.firstCorner = latLng;
      this.cornerMarker = L.circleMarker(latLng, { radius: 5, color: '#2563eb', fillOpacity: 1 }).addTo(map);
      this.drawState = 'second-corner';
    } else if (this.drawState === 'second-corner' && this.firstCorner) {
      this.setRegion(this.firstCorner, latLng);
      this.cornerMarker?.remove();
      this.drawState = 'idle';
    }
  }

  get regionWidthKm(): number {
    if (!this.region) return 0;
    const midLat = (this.region.north + this.region.south) / 2;
    return (this.region.east - this.region.west) * EARTH_KM_PER_DEGREE * Math.cos(midLat * Math.PI / 180);
  }

  get regionHeightKm(): number {
    return this.region ? (this.region.north - this.region.south) * EARTH_KM_PER_DEGREE : 0;
  }

  private setRegion(a: L.LatLng, b: L.LatLng): void {
    const south = Math.min(a.lat, b.lat);
    const north = Math.max(a.lat, b.lat);
    const west = Math.min(a.lng, b.lng);
    const east = Math.max(a.lng, b.lng);
    if (north - south < 1e-4 || east - west < 1e-4) {
      this.errorMessage = 'A região selecionada é pequena demais. Escolha dois cantos mais distantes.';
      return;
    }
    this.errorMessage = null;
    this.region = { south, west, north, east };
    this.drawRegion();
  }

  private previewRegion(cursor: L.LatLng): void {
    if (this.drawState !== 'second-corner' || !this.firstCorner) return;
    const bounds = L.latLngBounds(this.firstCorner, cursor);
    if (this.regionLayer) {
      this.regionLayer.setBounds(bounds);
    } else {
      this.regionLayer = L.rectangle(bounds, this.regionStyle()).addTo(this.mapView.getMap());
    }
  }

  private drawRegion(): void {
    const map = this.mapView?.getMap();
    if (!map) return;
    this.regionLayer?.remove();
    this.regionLayer = undefined;
    if (!this.region) return;
    const bounds = L.latLngBounds([this.region.south, this.region.west], [this.region.north, this.region.east]);
    this.regionLayer = L.rectangle(bounds, this.regionStyle()).addTo(map);
  }

  private regionStyle(): L.PathOptions {
    return { color: '#2563eb', weight: 2, fillOpacity: 0.08, dashArray: '6 4' };
  }

  // ===== Jobs =====

  loadJobs(): void {
    this.loadingJobs = true;
    this.datasetService.listJobs().pipe(takeUntil(this.destroy$)).subscribe({
      next: jobs => {
        this.jobs = jobs;
        this.loadingJobs = false;
      },
      error: () => {
        this.loadingJobs = false;
        this.errorMessage = 'Não foi possível carregar o histórico de datasets.';
      }
    });
  }

  createJob(request: CreateDatasetJobRequest): void {
    this.submitting = true;
    this.errorMessage = null;
    this.datasetService.createJob(request).pipe(takeUntil(this.destroy$)).subscribe({
      next: job => {
        this.submitting = false;
        this.jobs = [job, ...this.jobs];
        this.selectJob(job);
      },
      error: err => {
        this.submitting = false;
        const errors: string[] | undefined = err?.error?.errors;
        this.errorMessage = errors?.length ? errors.join(' ') : 'Falha ao criar o job de geração do dataset.';
      }
    });
  }

  selectJob(job: DatasetJob): void {
    this.pollSubscription?.unsubscribe();
    if (this.selectedJob?.id !== job.id) {
      this.instances = [];
      this.configs = [];
      this.runs = [];
      this.selectedInstanceId = null;
      this.clearInstanceLayer();
    }
    this.selectedJob = job;
    this.drawJobRegion(job, true);

    let wasActive = this.isActive(job);
    this.pollSubscription = this.datasetService.pollJob(job.id).pipe(takeUntil(this.destroy$)).subscribe({
      next: updated => {
        this.selectedJob = updated;
        this.replaceJobInList(updated);
        this.loadInstances(updated.id);
        const active = this.isActive(updated);
        if (!active) {
          this.loadConfigs(updated.id);
          if (wasActive) this.loadJobs();
        }
        wasActive = active;
      },
      error: () => this.errorMessage = 'Falha ao consultar o progresso do job.'
    });
  }

  cancelJob(): void {
    this.runCommand(id => this.datasetService.cancelJob(id));
  }

  resumeJob(): void {
    this.runCommand(id => this.datasetService.resumeJob(id));
  }

  deleteJob(): void {
    if (!this.selectedJob) return;
    if (!confirm('Excluir este job e todos os seus dados? Esta ação não pode ser desfeita.')) return;
    const id = this.selectedJob.id;
    this.busy = true;
    this.datasetService.deleteJob(id).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.busy = false;
        this.pollSubscription?.unsubscribe();
        this.jobs = this.jobs.filter(j => j.id !== id);
        this.selectedJob = null;
        this.instances = [];
        this.configs = [];
        this.runs = [];
        this.selectedInstanceId = null;
        this.clearInstanceLayer();
        this.jobRegionLayer?.remove();
      },
      error: err => this.handleCommandError(err)
    });
  }

  download(level: DatasetExportLevel, jobId: string | null = this.selectedJob?.id ?? null): void {
    this.datasetService.download(jobId, level).pipe(takeUntil(this.destroy$)).subscribe({
      error: () => this.errorMessage = 'Falha ao exportar o dataset.'
    });
  }

  // ===== Instances =====

  selectInstance(instance: DatasetInstance): void {
    this.selectedInstanceId = instance.id;
    this.runs = [];
    this.drawInstance(instance);
    if (instance.status === 'Completed' && this.selectedJob) {
      this.datasetService.getRuns(this.selectedJob.id, instance.id).pipe(takeUntil(this.destroy$))
        .subscribe(runs => this.runs = runs);
    }
  }

  private loadInstances(jobId: string): void {
    this.datasetService.getInstances(jobId).pipe(takeUntil(this.destroy$)).subscribe(instances => {
      if (this.selectedJob?.id === jobId) this.instances = instances;
    });
  }

  private loadConfigs(jobId: string): void {
    this.datasetService.getConfigSummaries(jobId).pipe(takeUntil(this.destroy$)).subscribe(configs => {
      if (this.selectedJob?.id === jobId) {
        this.configs = configs;
        this.cdr.detectChanges();
      }
    });
  }

  private drawInstance(instance: DatasetInstance): void {
    const map = this.mapView.getMap();
    this.clearInstanceLayer();
    if (instance.coordinates.length === 0) return;

    const group = L.layerGroup();
    const tour = instance.optimalTour;
    if (tour.length > 1) {
      const path = tour.map(i => [instance.coordinates[i].lat, instance.coordinates[i].lng] as L.LatLngExpression);
      L.polyline(path, { color: '#16a34a', weight: 3, opacity: 0.85, dashArray: '8 6' }).addTo(group);
    }

    instance.coordinates.forEach((point, index) => {
      const order = tour.indexOf(index);
      const isStart = tour.length > 0 ? order === 0 : index === instance.fixedStartIndex;
      const label = tour.length > 0 ? order + 1 : index + 1;
      L.marker([point.lat, point.lng], {
        icon: L.divIcon({
          className: 'custom-marker',
          html: `<div class="marker-pin ${isStart ? 'start' : 'optimized'}">${label}</div>`,
          iconSize: [32, 32],
          iconAnchor: [16, 16]
        })
      }).bindPopup(`<strong>Ponto ${index + 1}</strong><br>Ordem ótima: ${label}`).addTo(group);
    });

    this.instanceLayer = group.addTo(map);
    const bounds = L.latLngBounds(instance.coordinates.map(c => [c.lat, c.lng] as L.LatLngTuple));
    map.fitBounds(bounds, { padding: [40, 40] });
  }

  private drawJobRegion(job: DatasetJob, fit: boolean): void {
    const map = this.mapView?.getMap();
    if (!map) return;
    this.jobRegionLayer?.remove();
    const bounds = L.latLngBounds([job.region.south, job.region.west], [job.region.north, job.region.east]);
    this.jobRegionLayer = L.rectangle(bounds, { color: '#6b7280', weight: 1.5, fillOpacity: 0.04, dashArray: '2 4' }).addTo(map);
    if (fit) map.fitBounds(bounds, { padding: [30, 30] });
  }

  private clearInstanceLayer(): void {
    this.instanceLayer?.remove();
    this.instanceLayer = undefined;
  }

  // ===== Helpers =====

  isActive(job: DatasetJob): boolean {
    return ACTIVE_JOB_STATUSES.includes(job.status);
  }

  private replaceJobInList(job: DatasetJob): void {
    this.jobs = this.jobs.map(j => j.id === job.id ? job : j);
  }

  private runCommand(command: (id: string) => ReturnType<DatasetService['cancelJob']>): void {
    if (!this.selectedJob) return;
    const job = this.selectedJob;
    this.busy = true;
    this.errorMessage = null;
    command(job.id).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.busy = false;
        // Re-select to restart polling and pick up the new status.
        this.datasetService.getJob(job.id).pipe(takeUntil(this.destroy$))
          .subscribe(updated => this.selectJob(updated));
      },
      error: err => this.handleCommandError(err)
    });
  }

  private handleCommandError(err: { error?: unknown }): void {
    this.busy = false;
    this.errorMessage = typeof err?.error === 'string' ? err.error : 'Não foi possível concluir a operação.';
  }
}
