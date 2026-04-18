import { Component, AfterViewInit, OnDestroy, ViewChild, ElementRef } from '@angular/core';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, takeUntil } from 'rxjs/operators';
import * as L from 'leaflet';
import { RouteService } from '../../services/route.service';
import { GeocodingService } from '../../services/geocoding.service';
import { LocationStateService } from '../../services/location-state.service';
import { PointsStorageService } from '../../services/points-storage.service';
import { LocationDto, IterationEvent } from '../../models/route.models';
import { GeocodingResult } from '../../models/geocoding.models';
import { MapViewComponent } from '../../components/map-view/map-view.component';
import { SearchBoxComponent } from '../../components/search-box/search-box.component';

@Component({
  selector: 'app-map-page',
  templateUrl: './map-page.component.html',
  styleUrls: ['./map-page.component.css']
})
export class MapPageComponent implements AfterViewInit, OnDestroy {
  @ViewChild(MapViewComponent) private mapView!: MapViewComponent;
  @ViewChild('searchBox') private searchBoxRef!: SearchBoxComponent;
  @ViewChild('toolsPanel') private toolsPanelRef!: ElementRef<HTMLElement>;
  @ViewChild('csvFileInput') private csvFileInputRef!: ElementRef<HTMLInputElement>;

  private markers: L.Marker[] = [];
  private routeLayer?: L.Polyline;
  private searchMarker?: L.Marker;
  private destroy$ = new Subject<void>();
  private setStartHandler = ((e: CustomEvent) => {
    this.setAsStart(e.detail);
  }) as EventListener;

  locations: LocationDto[] = [];
  startIndex: number | null = null;
  isOptimizing = false;
  totalDistance: number | null = null;
  bestRouteOrder: number[] = [];
  errorMessage: string | null = null;
  hasSearchMarker = false;

  mobileSearchQuery = '';
  mobileSearchResults: GeocodingResult[] = [];
  isMobileSearching = false;
  showMobileResults = false;
  private mobileSearchSubject$ = new Subject<string>();

  locating = false;
  mapCenter: { lat: number; lng: number } | undefined;
  activeTab: 'map' | 'points' = 'map';
  toolsOpen = false;
  private drawerDragStartY = 0;
  private drawerDragDeltaY = 0;
  private drawerIsDragging = false;
  private drawerDragStartTime = 0;

  visualMode = false;
  isVisualizing = false;
  currentIteration = 0;
  totalIterations = 0;
  private antLayers: L.Polyline[] = [];
  private bestIterationLayer?: L.Polyline;
  private visualAbort?: () => void;

  constructor(
    private router: Router,
    private routeService: RouteService,
    private geocodingService: GeocodingService,
    private locationState: LocationStateService,
    private pointsStorage: PointsStorageService
  ) {
    this.mobileSearchSubject$.pipe(
      debounceTime(400),
      distinctUntilChanged(),
      switchMap(query => {
        this.isMobileSearching = query.trim().length >= 3;
        const center = this.mapView?.getMap()?.getCenter();
        const location = center ? { lat: center.lat, lng: center.lng } : undefined;
        return this.geocodingService.search(query, location);
      }),
      takeUntil(this.destroy$)
    ).subscribe(results => {
      this.mobileSearchResults = results;
      this.showMobileResults = results.length > 0;
      this.isMobileSearching = false;
    });
  }

  ngAfterViewInit(): void {
    document.addEventListener('setStart', this.setStartHandler);
    Promise.resolve().then(() => {
      this.goToMyLocation();
      const center = this.mapView?.getMap()?.getCenter();
      if (center) this.mapCenter = { lat: center.lat, lng: center.lng };
    });
  }

  ngOnDestroy(): void {
    if (this.visualAbort) this.visualAbort();
    document.removeEventListener('setStart', this.setStartHandler);
    this.destroy$.next();
    this.destroy$.complete();
  }

  onMapClick(e: { lat: number; lng: number }): void {
    this.addPoint(e.lat, e.lng);
  }

  onLocationSelected(result: GeocodingResult): void {
    this.clearSearchMarker();
    const map = this.mapView.getMap();
    const searchIcon = L.divIcon({
      className: 'custom-marker',
      html: '<div class="marker-pin search"><i class="bi bi-search"></i></div>',
      iconSize: [32, 32],
      iconAnchor: [16, 16]
    });
    this.searchMarker = L.marker([result.lat, result.lng], { icon: searchIcon }).addTo(map);
    this.hasSearchMarker = true;
    this.searchMarker.bindPopup(
      `<strong>${result.displayName.split(',')[0]}</strong><br>` +
      `Lat: ${result.lat.toFixed(5)}<br>Lng: ${result.lng.toFixed(5)}<br><br>` +
      `<em>Clique no mapa neste local para adicionar como ponto</em>`
    ).openPopup();
    map.setView([result.lat, result.lng], 16);
    this.switchTab('map');
  }

  onFocusPoint(loc: LocationDto): void {
    const index = this.locations.indexOf(loc);
    const map = this.mapView.getMap();
    map.setView([loc.lat, loc.lng], 17);
    if (index >= 0) this.markers[index].openPopup();
    this.switchTab('map');
  }

  goToDashboard(): void {
    this.router.navigate(['/dashboard']);
  }

  goToMyLocation(): void {
    if (!('geolocation' in navigator)) return;
    this.locating = true;
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        this.locating = false;
        this.mapView.getMap().flyTo([pos.coords.latitude, pos.coords.longitude], 15, { duration: 1.2 });
      },
      () => { this.locating = false; },
      { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 }
    );
  }

  private addPoint(lat: number, lng: number): void {
    const map = this.mapView.getMap();
    const index = this.locations.length;
    this.locations.push({ lat, lng });
    const isStart = this.startIndex === null;
    if (isStart) this.startIndex = index;
    const marker = L.marker([lat, lng], { icon: this.createMarkerIcon(index, index === this.startIndex) }).addTo(map);
    marker.bindPopup(this.createPopupContent(index, index === this.startIndex));
    this.markers.push(marker);
    this.locationState.sync(this.locations, this.startIndex);
    this.clearRoute();
  }

  setAsStart(index: number): void {
    const previousStart = this.startIndex;
    this.startIndex = index;
    if (previousStart !== null && previousStart < this.markers.length) {
      this.markers[previousStart].setIcon(this.createMarkerIcon(previousStart, false));
      this.markers[previousStart].setPopupContent(this.createPopupContent(previousStart, false));
    }
    this.markers[index].setIcon(this.createMarkerIcon(index, true));
    this.markers[index].setPopupContent(this.createPopupContent(index, true));
    this.locationState.sync(this.locations, this.startIndex);
    this.clearRoute();
  }

  private createMarkerIcon(index: number, isStart: boolean): L.DivIcon {
    const cssClass = isStart ? 'marker-pin start' : 'marker-pin';
    const label = isStart ? '<i class="bi bi-flag-fill"></i>' : `${index + 1}`;
    return L.divIcon({
      className: 'custom-marker',
      html: `<div class="${cssClass}">${label}</div>`,
      iconSize: [32, 32],
      iconAnchor: [16, 16]
    });
  }

  private createPopupContent(index: number, isStart: boolean): string {
    const loc = this.locations[index];
    const startLabel = isStart ? '<strong style="color:#16a34a">Ponto de partida</strong><br>' : '';
    const startBtn = isStart ? '' : `<br><a href="#" onclick="document.dispatchEvent(new CustomEvent('setStart', {detail: ${index}})); return false;">Definir como partida</a>`;
    return `${startLabel}Ponto ${index + 1}<br><span style="font-family:monospace;font-size:12px;color:#6b7280">` +
      `${loc.lat.toFixed(5)}, ${loc.lng.toFixed(5)}</span>${startBtn}`;
  }

  optimizeRoute(): void {
    if (this.locations.length < 2) {
      this.errorMessage = 'Adicione pelo menos 2 pontos no mapa.';
      return;
    }
    this.errorMessage = null;
    this.isOptimizing = true;
    if (this.visualMode) {
      this.optimizeRouteVisual();
      return;
    }
    this.routeService.optimizeRoute({
      locations: this.locations,
      startLocationIndex: this.startIndex ?? undefined
    }).subscribe({
      next: (response) => {
        this.isOptimizing = false;
        this.totalDistance = response.totalDistance;
        this.bestRouteOrder = response.bestRouteOrder;
        this.drawRoute(response.routeCoordinates);
        this.updateMarkerLabels(response.bestRouteOrder);
      },
      error: (err) => {
        this.isOptimizing = false;
        this.errorMessage = 'Erro ao otimizar rota. Tente novamente.';
        console.error(err);
      }
    });
  }

  private optimizeRouteVisual(): void {
    this.isVisualizing = true;
    this.currentIteration = 0;
    this.totalIterations = 0;
    this.clearRoute();
    const subscription = this.routeService.optimizeRouteVisual({
      locations: this.locations,
      startLocationIndex: this.startIndex ?? undefined
    }).subscribe({
      next: (event) => {
        if (event.type === 'iteration') {
          this.currentIteration = event.iteration + 1;
          this.totalIterations = event.totalIterations;
          this.renderIteration(event);
        } else if (event.type === 'result') {
          this.clearIterationVisuals();
          this.isOptimizing = false;
          this.isVisualizing = false;
          this.totalDistance = event.totalDistance;
          this.bestRouteOrder = event.bestRouteOrder;
          this.drawRoute(event.routeCoordinates);
          this.updateMarkerLabels(event.bestRouteOrder);
        }
      },
      error: (err) => {
        this.clearIterationVisuals();
        this.isOptimizing = false;
        this.isVisualizing = false;
        this.errorMessage = 'Erro ao otimizar rota. Tente novamente.';
        console.error(err);
      }
    });
    this.visualAbort = () => subscription.unsubscribe();
  }

  private renderIteration(event: IterationEvent): void {
    const map = this.mapView.getMap();
    this.clearIterationVisuals();
    for (const tour of event.antTours) {
      const latLngs: L.LatLngExpression[] = tour.map(i => [this.locations[i].lat, this.locations[i].lng]);
      const line = L.polyline(latLngs, { color: '#94a3b8', weight: 1.5, opacity: 0.3 }).addTo(map);
      this.antLayers.push(line);
    }
    const bestLatLngs: L.LatLngExpression[] = event.bestTourSoFar.map(i => [this.locations[i].lat, this.locations[i].lng]);
    this.bestIterationLayer = L.polyline(bestLatLngs, { color: '#f59e0b', weight: 3, opacity: 0.85 }).addTo(map);
  }

  private clearIterationVisuals(): void {
    const map = this.mapView?.getMap();
    if (!map) return;
    this.antLayers.forEach(l => map.removeLayer(l));
    this.antLayers = [];
    if (this.bestIterationLayer) {
      map.removeLayer(this.bestIterationLayer);
      this.bestIterationLayer = undefined;
    }
  }

  private drawRoute(coordinates: LocationDto[]): void {
    const map = this.mapView.getMap();
    if (this.routeLayer) {
      map.removeLayer(this.routeLayer);
      this.routeLayer = undefined;
    }
    const latLngs: L.LatLngExpression[] = coordinates.map(c => [c.lat, c.lng]);
    this.routeLayer = L.polyline(latLngs, { color: '#2563eb', weight: 4, opacity: 0.8 }).addTo(map);
    map.fitBounds(this.routeLayer.getBounds(), { padding: [50, 50] });
  }

  private updateMarkerLabels(order: number[]): void {
    for (let newPos = 0; newPos < order.length; newPos++) {
      const originalIndex = order[newPos];
      const isStart = newPos === 0 && this.startIndex !== null;
      const cssClass = isStart ? 'marker-pin start' : 'marker-pin optimized';
      const label = isStart ? '<i class="bi bi-flag-fill"></i>' : `${newPos + 1}`;
      this.markers[originalIndex].setIcon(L.divIcon({
        className: 'custom-marker',
        html: `<div class="${cssClass}">${label}</div>`,
        iconSize: [32, 32],
        iconAnchor: [16, 16]
      }));
    }
  }

  private clearRoute(): void {
    const map = this.mapView?.getMap();
    if (this.routeLayer && map) {
      map.removeLayer(this.routeLayer);
      this.routeLayer = undefined;
    }
    this.clearIterationVisuals();
    this.totalDistance = null;
    this.bestRouteOrder = [];
  }

  clearAll(): void {
    if (this.visualAbort) {
      this.visualAbort();
      this.visualAbort = undefined;
    }
    this.isOptimizing = false;
    this.isVisualizing = false;
    const map = this.mapView.getMap();
    this.markers.forEach(m => map.removeLayer(m));
    this.markers = [];
    this.locations = [];
    this.startIndex = null;
    this.locationState.sync([], null);
    this.clearRoute();
    this.clearSearchMarker();
    this.errorMessage = null;
  }

  removePoint(index: number): void {
    const map = this.mapView.getMap();
    map.removeLayer(this.markers[index]);
    this.markers.splice(index, 1);
    this.locations.splice(index, 1);
    if (this.startIndex === index) {
      this.startIndex = this.locations.length > 0 ? 0 : null;
    } else if (this.startIndex !== null && this.startIndex > index) {
      this.startIndex--;
    }
    this.rebuildMarkers();
    this.locationState.sync(this.locations, this.startIndex);
    this.clearRoute();
  }

  private rebuildMarkers(): void {
    const map = this.mapView.getMap();
    this.markers.forEach(m => map.removeLayer(m));
    this.markers = [];
    for (let i = 0; i < this.locations.length; i++) {
      const loc = this.locations[i];
      const isStart = i === this.startIndex;
      const marker = L.marker([loc.lat, loc.lng], { icon: this.createMarkerIcon(i, isStart) }).addTo(map);
      marker.bindPopup(this.createPopupContent(i, isStart));
      this.markers.push(marker);
    }
  }

  fitAllPoints(): void {
    if (this.locations.length === 0) return;
    const group = L.featureGroup(this.markers);
    this.mapView.getMap().fitBounds(group.getBounds(), { padding: [50, 50] });
  }

  switchTab(tab: 'map' | 'points'): void {
    this.activeTab = tab;
    if (tab !== 'map') this.toolsOpen = false;
    if (tab === 'map') setTimeout(() => this.mapView?.invalidateSize(), 50);
  }

  toggleTools(): void { this.toolsOpen = !this.toolsOpen; }

  onDrawerDragStart(event: TouchEvent): void {
    if (!this.toolsOpen) return;
    this.drawerIsDragging = true;
    this.drawerDragStartY = event.touches[0].clientY;
    this.drawerDragDeltaY = 0;
    this.drawerDragStartTime = Date.now();
    const el = this.toolsPanelRef?.nativeElement;
    if (el) el.style.transition = 'none';
  }

  onDrawerDragMove(event: TouchEvent): void {
    if (!this.drawerIsDragging) return;
    const delta = Math.max(0, event.touches[0].clientY - this.drawerDragStartY);
    this.drawerDragDeltaY = delta;
    const el = this.toolsPanelRef?.nativeElement;
    if (el) el.style.transform = `translateY(${delta}px)`;
  }

  onDrawerDragEnd(): void {
    if (!this.drawerIsDragging) return;
    this.drawerIsDragging = false;
    const el = this.toolsPanelRef?.nativeElement;
    if (!el) return;
    const elapsed = Date.now() - this.drawerDragStartTime;
    const velocity = elapsed > 0 ? this.drawerDragDeltaY / elapsed : 0;
    const shouldClose = this.drawerDragDeltaY > 80 || velocity > 0.4;
    el.style.transition = '';
    el.style.transform = shouldClose ? 'translateY(100%)' : 'translateY(0)';
    el.addEventListener('transitionend', () => {
      el.style.transform = '';
      if (shouldClose) this.toolsOpen = false;
    }, { once: true });
  }

  exportToGoogleMaps(): void {
    if (this.bestRouteOrder.length < 2) return;
    const ordered = this.bestRouteOrder.map(i => this.locations[i]);
    const origin = ordered[0];
    const destination = ordered[ordered.length - 1];
    const waypoints = ordered.slice(1, -1);
    let url = 'https://www.google.com/maps/dir/?api=1';
    url += `&origin=${origin.lat},${origin.lng}`;
    url += `&destination=${destination.lat},${destination.lng}`;
    if (waypoints.length > 0) url += `&waypoints=${waypoints.map(w => `${w.lat},${w.lng}`).join('|')}`;
    url += '&travelmode=driving';
    window.open(url, '_blank');
  }

  get hasSavedPoints(): boolean { return this.pointsStorage.hasSavedPoints(); }
  savePoints(): void { this.pointsStorage.saveToLocalStorage(this.locations, this.startIndex); }
  loadSavedPoints(): void {
    const saved = this.pointsStorage.loadFromLocalStorage();
    if (saved) this.applyLocations(saved.locations, saved.startIndex);
  }
  exportCsv(): void { this.pointsStorage.exportToCsv(this.locations, this.startIndex); }
  triggerCsvImport(): void { this.csvFileInputRef.nativeElement.click(); }

  onCsvFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = (e) => {
      try {
        const saved = this.pointsStorage.parseCsv(e.target!.result as string);
        this.applyLocations(saved.locations, saved.startIndex);
      } catch (err: any) {
        this.errorMessage = err.message ?? 'Erro ao importar CSV.';
      }
      (event.target as HTMLInputElement).value = '';
    };
    reader.readAsText(file);
  }

  private applyLocations(locations: LocationDto[], startIndex: number | null): void {
    const map = this.mapView.getMap();
    this.markers.forEach(m => map.removeLayer(m));
    this.markers = [];
    if (this.routeLayer) { map.removeLayer(this.routeLayer); this.routeLayer = undefined; }
    this.clearIterationVisuals();
    this.totalDistance = null;
    this.bestRouteOrder = [];
    this.errorMessage = null;
    this.locations = [];
    this.startIndex = null;
    locations.forEach(loc => this.addPoint(loc.lat, loc.lng));
    if (startIndex !== null && startIndex < this.locations.length) {
      this.startIndex = startIndex;
      this.rebuildMarkers();
    }
    this.locationState.sync(this.locations, this.startIndex);
    if (this.locations.length > 0) this.fitAllPoints();
  }

  addSearchResultAsPoint(): void {
    if (!this.searchMarker) return;
    const latlng = this.searchMarker.getLatLng();
    this.addPoint(latlng.lat, latlng.lng);
    this.clearSearchMarker();
    this.searchBoxRef?.clear();
  }

  private clearSearchMarker(): void {
    if (this.searchMarker) {
      this.mapView?.getMap()?.removeLayer(this.searchMarker);
      this.searchMarker = undefined;
    }
    this.hasSearchMarker = false;
  }

  onMobileSearchInput(): void { this.mobileSearchSubject$.next(this.mobileSearchQuery); }

  selectMobileSearchResult(result: GeocodingResult): void {
    this.mobileSearchQuery = result.displayName;
    this.showMobileResults = false;
    this.clearSearchMarker();
    const map = this.mapView.getMap();
    const searchIcon = L.divIcon({
      className: 'custom-marker',
      html: '<div class="marker-pin search"><i class="bi bi-search"></i></div>',
      iconSize: [32, 32],
      iconAnchor: [16, 16]
    });
    this.searchMarker = L.marker([result.lat, result.lng], { icon: searchIcon }).addTo(map);
    this.hasSearchMarker = true;
    map.setView([result.lat, result.lng], 16);
  }

  hideMobileSearchResults(): void { setTimeout(() => { this.showMobileResults = false; }, 200); }

  clearMobileSearch(): void {
    this.mobileSearchQuery = '';
    this.mobileSearchResults = [];
    this.showMobileResults = false;
    this.clearSearchMarker();
  }
}
