import {
  Component,
  AfterViewInit,
  OnDestroy,
  ViewChild,
  ElementRef,
  ChangeDetectorRef
} from '@angular/core';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, takeUntil } from 'rxjs/operators';
import * as L from 'leaflet';
import { Chart, BarController, BarElement, CategoryScale, LinearScale, Tooltip, Legend } from 'chart.js';
import { RouteService } from '../../services/route.service';
import { GeocodingService } from '../../services/geocoding.service';
import { LocationStateService } from '../../services/location-state.service';
import { LocationDto, OptimizationRequest, AlgorithmResult } from '../../models/route.models';
import { GeocodingResult } from '../../models/geocoding.models';

Chart.register(BarController, BarElement, CategoryScale, LinearScale, Tooltip, Legend);

const ALGORITHM_COLORS: Record<string, string> = {
  'ACO': '#2563eb',
  'Nearest Neighbor': '#16a34a',
  '2-Opt': '#d97706',
  'Genetic Algorithm': '#9333ea'
};

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements AfterViewInit, OnDestroy {
  @ViewChild('miniMap') private miniMapRef!: ElementRef<HTMLElement>;
  @ViewChild('distanceChart') private distanceChartRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('timeChart') private timeChartRef!: ElementRef<HTMLCanvasElement>;

  private miniMap!: L.Map;
  private routeLayers: L.Polyline[] = [];
  private markers: L.Marker[] = [];
  private destroy$ = new Subject<void>();
  private distanceChartInstance?: Chart;
  private timeChartInstance?: Chart;

  locations: LocationDto[] = [];
  startIndex: number | null = null;
  isComparing = false;
  results: AlgorithmResult[] = [];
  errorMessage: string | null = null;

  searchQuery = '';
  searchResults: GeocodingResult[] = [];
  isSearching = false;
  showResults = false;
  private searchSubject$ = new Subject<string>();

  constructor(
    private router: Router,
    private routeService: RouteService,
    private geocodingService: GeocodingService,
    private locationState: LocationStateService,
    private cdr: ChangeDetectorRef
  ) {}

  ngAfterViewInit(): void {
    this.initMap();
    this.setupSearch();
    this.loadSavedLocations();
  }

  private loadSavedLocations(): void {
    const saved = this.locationState.locations;
    if (saved.length > 0) {
      this.locations = [...saved];
      this.startIndex = this.locationState.startIndex;
      this.rebuildMarkers();
      this.fitPoints();
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.distanceChartInstance?.destroy();
    this.timeChartInstance?.destroy();
    if (this.miniMap) this.miniMap.remove();
  }

  goBack(): void {
    this.router.navigate(['/']);
  }

  private initMap(): void {
    this.miniMap = L.map(this.miniMapRef.nativeElement, {
      center: [-19.917, -43.934],
      zoom: 13
    });

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '© OpenStreetMap contributors'
    }).addTo(this.miniMap);

    this.miniMap.on('click', (e: L.LeafletMouseEvent) => {
      this.addPoint(e.latlng.lat, e.latlng.lng);
    });
  }

  private setupSearch(): void {
    this.searchSubject$
      .pipe(
        debounceTime(400),
        distinctUntilChanged(),
        switchMap(query => {
          this.isSearching = true;
          return this.geocodingService.search(query);
        }),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: results => {
          this.searchResults = results;
          this.isSearching = false;
          this.showResults = true;
        },
        error: () => { this.isSearching = false; }
      });
  }

  onSearchInput(): void {
    if (this.searchQuery.length >= 2) {
      this.searchSubject$.next(this.searchQuery);
    } else {
      this.searchResults = [];
      this.showResults = false;
    }
  }

  hideSearchResults(): void {
    setTimeout(() => { this.showResults = false; }, 200);
  }

  clearSearch(): void {
    this.searchQuery = '';
    this.searchResults = [];
    this.showResults = false;
  }

  selectSearchResult(result: GeocodingResult): void {
    this.addPoint(result.lat, result.lng);
    this.searchQuery = '';
    this.showResults = false;
    this.searchResults = [];
  }

  addPoint(lat: number, lng: number): void {
    const index = this.locations.length;
    this.locations.push({ lat, lng });
    this.addMarker(lat, lng, index);
    this.fitPoints();
    this.results = [];
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
      .addTo(this.miniMap)
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
      this.miniMap.fitBounds(bounds, { padding: [40, 40], maxZoom: 15 });
    }
  }

  compare(): void {
    if (this.locations.length < 2 || this.isComparing) return;

    this.isComparing = true;
    this.errorMessage = null;
    this.results = [];
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
    this.routeLayers.forEach(l => l.remove());
    this.routeLayers = [];
  }

  private drawRoutes(): void {
    this.clearRouteLayers();
    for (const result of this.results) {
      if (result.routeCoordinates.length < 2) continue;
      const latlngs = result.routeCoordinates.map(c => [c.lat, c.lng] as L.LatLngTuple);
      const color = ALGORITHM_COLORS[result.algorithm] ?? '#64748b';
      const layer = L.polyline(latlngs, { color, weight: 3, opacity: 0.8 }).addTo(this.miniMap);
      this.routeLayers.push(layer);
    }
    if (this.routeLayers.length > 0) {
      const allLatLngs = this.routeLayers.flatMap(l => l.getLatLngs() as L.LatLng[]);
      this.miniMap.fitBounds(L.latLngBounds(allLatLngs), { padding: [40, 40] });
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
      data: {
        labels,
        datasets: [{
          label: 'Distância (km)',
          data: distances,
          backgroundColor: colors
        }]
      },
      options: {
        responsive: true,
        plugins: { legend: { display: false } },
        scales: { y: { beginAtZero: false, title: { display: true, text: 'km' } } }
      }
    });

    this.timeChartInstance = new Chart(this.timeChartRef.nativeElement, {
      type: 'bar',
      data: {
        labels,
        datasets: [{
          label: 'Tempo (ms)',
          data: times,
          backgroundColor: colors
        }]
      },
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
}
