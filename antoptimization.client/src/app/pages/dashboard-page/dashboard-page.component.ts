import {
  Component,
  OnInit,
  AfterViewInit,
  OnDestroy,
  ViewChild,
  ElementRef,
  ChangeDetectorRef
} from '@angular/core';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import * as L from 'leaflet';
import { Chart, BarController, BarElement, CategoryScale, LinearScale, Tooltip, Legend } from 'chart.js';
import { RouteService } from '../../services/route.service';
import { LocationStateService } from '../../services/location-state.service';
import { LocationDto, OptimizationRequest, AlgorithmResult } from '../../models/route.models';
import { MapViewComponent } from '../../components/map-view/map-view.component';

Chart.register(BarController, BarElement, CategoryScale, LinearScale, Tooltip, Legend);

const ALGORITHM_COLORS: Record<string, string> = {
  'ACO': '#2563eb',
  'Nearest Neighbor': '#16a34a',
  '2-Opt': '#d97706',
  'Genetic Algorithm': '#9333ea'
};

@Component({
  selector: 'app-dashboard-page',
  templateUrl: './dashboard-page.component.html',
  styleUrls: ['./dashboard-page.component.css']
})
export class DashboardPageComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild(MapViewComponent) private mapView!: MapViewComponent;
  @ViewChild('distanceChart') private distanceChartRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('timeChart') private timeChartRef!: ElementRef<HTMLCanvasElement>;

  private routeLayerMap = new Map<string, L.Polyline>();
  private markers: L.Marker[] = [];
  private destroy$ = new Subject<void>();
  private distanceChartInstance?: Chart;
  private timeChartInstance?: Chart;

  locations: LocationDto[] = [];
  startIndex: number | null = null;
  isComparing = false;
  isMapExpanded = false;
  results: AlgorithmResult[] = [];
  selectedAlgorithm: string | null = null;
  errorMessage: string | null = null;

  constructor(
    private router: Router,
    private routeService: RouteService,
    private locationState: LocationStateService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const saved = this.locationState.locations;
    if (saved.length > 0) {
      this.locations = [...saved];
      this.startIndex = this.locationState.startIndex;
    }
  }

  ngAfterViewInit(): void {
    if (this.locations.length > 0) {
      this.rebuildMarkers();
      this.fitPoints();
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.distanceChartInstance?.destroy();
    this.timeChartInstance?.destroy();
    this.routeLayerMap.forEach(l => l.remove());
    this.routeLayerMap.clear();
  }

  goBack(): void { this.router.navigate(['/']); }

  toggleMapExpand(): void {
    this.isMapExpanded = !this.isMapExpanded;
    setTimeout(() => this.mapView.invalidateSize(), 50);
  }

  selectAlgorithmFilter(algo: string | null): void {
    this.selectedAlgorithm = algo;
    this.applyRouteFilter();
  }

  private applyRouteFilter(): void {
    const map = this.mapView.getMap();
    this.routeLayerMap.forEach((layer, algo) => {
      if (this.selectedAlgorithm === null || this.selectedAlgorithm === algo) {
        layer.addTo(map);
      } else {
        layer.remove();
      }
    });
  }

  onFocusPoint(loc: LocationDto): void {
    this.mapView.getMap().setView([loc.lat, loc.lng], 15);
  }

  removePoint(index: number): void {
    this.locations.splice(index, 1);
    if (this.startIndex !== null) {
      if (this.startIndex === index) this.startIndex = null;
      else if (this.startIndex > index) this.startIndex--;
    }
    this.rebuildMarkers();
    this.results = [];
  }

  setAsStart(index: number): void {
    this.startIndex = this.startIndex === index ? null : index;
    this.rebuildMarkers();
  }

  private addMarker(lat: number, lng: number, index: number): void {
    const isStart = index === this.startIndex;
    const icon = L.divIcon({
      className: '',
      html: isStart
        ? `<div class="db-marker db-marker--start">S</div>`
        : `<div class="db-marker">${index + 1}</div>`,
      iconSize: [30, 30],
      iconAnchor: [15, 15]
    });
    const marker = L.marker([lat, lng], { icon })
      .addTo(this.mapView.getMap())
      .on('click', () => this.setAsStart(index));
    this.markers.push(marker);
  }

  private rebuildMarkers(): void {
    this.markers.forEach(m => m.remove());
    this.markers = [];
    this.locations.forEach((loc, i) => this.addMarker(loc.lat, loc.lng, i));
  }

  private fitPoints(): void {
    if (this.locations.length > 0) {
      const bounds = L.latLngBounds(this.locations.map(l => [l.lat, l.lng] as L.LatLngTuple));
      this.mapView.getMap().fitBounds(bounds, { padding: [40, 40], maxZoom: 15 });
    }
  }

  compare(): void {
    if (this.locations.length < 2 || this.isComparing) return;
    this.isComparing = true;
    this.errorMessage = null;
    this.results = [];
    this.selectedAlgorithm = null;
    this.clearRouteLayers();

    const request: OptimizationRequest = {
      locations: this.locations,
      startLocationIndex: this.startIndex ?? undefined
    };

    this.routeService.compareRoutes(request)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: response => {
          this.results = response.results;
          this.isComparing = false;
          this.cdr.detectChanges();
          this.drawRoutes();
          this.renderCharts();
        },
        error: () => {
          this.errorMessage = 'Erro ao comparar algoritmos. Tente novamente.';
          this.isComparing = false;
        }
      });
  }

  get bestAlgorithm(): string {
    if (this.results.length === 0) return '';
    return this.results.reduce((a, b) => a.totalDistance < b.totalDistance ? a : b).algorithm;
  }

  private clearRouteLayers(): void {
    this.routeLayerMap.forEach(l => l.remove());
    this.routeLayerMap.clear();
  }

  private drawRoutes(): void {
    this.clearRouteLayers();
    const map = this.mapView.getMap();
    for (const result of this.results) {
      if (result.routeCoordinates.length < 2) continue;
      const latlngs = result.routeCoordinates.map(c => [c.lat, c.lng] as L.LatLngTuple);
      const color = ALGORITHM_COLORS[result.algorithm] ?? '#64748b';
      const layer = L.polyline(latlngs, { color, weight: 4, opacity: 0.85 }).addTo(map);
      this.routeLayerMap.set(result.algorithm, layer);
    }
    if (this.routeLayerMap.size > 0) {
      const allLatLngs: L.LatLng[] = [];
      this.routeLayerMap.forEach(l => allLatLngs.push(...(l.getLatLngs() as L.LatLng[])));
      map.fitBounds(L.latLngBounds(allLatLngs), { padding: [40, 40] });
    }
  }

  private renderCharts(): void {
    if (!this.distanceChartRef?.nativeElement || !this.timeChartRef?.nativeElement) return;
    const labels = this.results.map(r => r.algorithm);
    const distances = this.results.map(r => r.totalDistance);
    const times = this.results.map(r => r.executionTimeMs);
    const colors = labels.map(l => ALGORITHM_COLORS[l] ?? '#64748b');

    this.distanceChartInstance?.destroy();
    this.timeChartInstance?.destroy();

    this.distanceChartInstance = new Chart(this.distanceChartRef.nativeElement, {
      type: 'bar',
      data: { labels, datasets: [{ label: 'Distância (km)', data: distances, backgroundColor: colors }] },
      options: {
        responsive: true,
        plugins: { legend: { display: false } },
        scales: { y: { beginAtZero: false, title: { display: true, text: 'km' } } }
      }
    });

    this.timeChartInstance = new Chart(this.timeChartRef.nativeElement, {
      type: 'bar',
      data: { labels, datasets: [{ label: 'Tempo (ms)', data: times, backgroundColor: colors }] },
      options: {
        responsive: true,
        plugins: { legend: { display: false } },
        scales: { y: { beginAtZero: true, title: { display: true, text: 'ms' } } }
      }
    });
  }

  getAlgorithmColor(algorithm: string): string {
    return ALGORITHM_COLORS[algorithm] ?? '#64748b';
  }

  getQualityBarWidth(relativeGapPercent: number): number {
    return Math.round(100 / (1 + relativeGapPercent / 100));
  }

  getQualityBarColor(relativeGapPercent: number): string {
    if (relativeGapPercent === 0) return 'var(--color-success)';
    if (relativeGapPercent <= 10) return 'var(--color-warning)';
    return 'var(--color-danger)';
  }
}
