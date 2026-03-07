import { Component, AfterViewInit, OnDestroy, ViewChild, ElementRef } from '@angular/core';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, takeUntil } from 'rxjs/operators';
import * as L from 'leaflet';
import { RouteService } from '../../services/route.service';
import { GeocodingService } from '../../services/geocoding.service';
import { LocationDto, IterationEvent } from '../../models/route.models';
import { GeocodingResult } from '../../models/geocoding.models';

@Component({
  selector: 'app-map',
  templateUrl: './map.component.html',
  styleUrls: ['./map.component.css']
})
export class MapComponent implements AfterViewInit, OnDestroy {
  private map!: L.Map;
  private markers: L.Marker[] = [];
  private routeLayer?: L.Polyline;
  private searchMarker?: L.Marker;
  private destroy$ = new Subject<void>();
  @ViewChild('toolsPanel') private toolsPanelRef!: ElementRef<HTMLElement>;

  locations: LocationDto[] = [];
  startIndex: number | null = null;
  isOptimizing = false;
  totalDistance: number | null = null;
  bestRouteOrder: number[] = [];
  errorMessage: string | null = null;

  searchQuery = '';
  searchResults: GeocodingResult[] = [];
  isSearching = false;
  showResults = false;
  hasSearchMarker = false;
  private searchSubject$ = new Subject<string>();

  locating = false;
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
    private routeService: RouteService,
    private geocodingService: GeocodingService
  ) {
    this.searchSubject$.pipe(
      debounceTime(400),
      distinctUntilChanged(),
      switchMap(query => {
        this.isSearching = query.trim().length >= 3;
        const center = this.map?.getCenter();
        const location = center ? { lat: center.lat, lng: center.lng } : undefined;
        return this.geocodingService.search(query, location);
      }),
      takeUntil(this.destroy$)
    ).subscribe(results => {
      this.searchResults = results;
      this.showResults = results.length > 0;
      this.isSearching = false;
    });
  }

  ngAfterViewInit(): void {
    this.initMap();
  }

  ngOnDestroy(): void {
    if (this.visualAbort) {
      this.visualAbort();
    }
    this.destroy$.next();
    this.destroy$.complete();
    this.map?.remove();
  }

  private initMap(): void {
    this.map = L.map('map', {
      center: [-19.9208, -43.9378],
      zoom: 13
    });

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    }).addTo(this.map);

    this.map.on('click', (e: L.LeafletMouseEvent) => {
      this.addPoint(e.latlng.lat, e.latlng.lng);
    });

    document.addEventListener('setStart', ((e: CustomEvent) => {
      this.setAsStart(e.detail);
    }) as EventListener);

    this.goToMyLocation();
  }

  goToMyLocation(): void {
    if (!('geolocation' in navigator)) return;

    this.locating = true;
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        this.locating = false;
        this.map.flyTo([pos.coords.latitude, pos.coords.longitude], 15, { duration: 1.2 });
      },
      () => {
        this.locating = false;
      },
      { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 }
    );
  }

  private addPoint(lat: number, lng: number): void {
    const index = this.locations.length;
    this.locations.push({ lat, lng });

    const isStart = this.startIndex === null;
    if (isStart) {
      this.startIndex = index;
    }

    const marker = L.marker([lat, lng], { icon: this.createMarkerIcon(index, index === this.startIndex) }).addTo(this.map);
    marker.bindPopup(this.createPopupContent(index, index === this.startIndex));
    this.markers.push(marker);

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
    this.clearIterationVisuals();

    for (const tour of event.antTours) {
      const latLngs: L.LatLngExpression[] = tour.map(i => [this.locations[i].lat, this.locations[i].lng]);
      const line = L.polyline(latLngs, {
        color: '#94a3b8',
        weight: 1.5,
        opacity: 0.3
      }).addTo(this.map);
      this.antLayers.push(line);
    }

    const bestLatLngs: L.LatLngExpression[] = event.bestTourSoFar.map(i => [this.locations[i].lat, this.locations[i].lng]);
    this.bestIterationLayer = L.polyline(bestLatLngs, {
      color: '#f59e0b',
      weight: 3,
      opacity: 0.85
    }).addTo(this.map);
  }

  private clearIterationVisuals(): void {
    this.antLayers.forEach(l => this.map.removeLayer(l));
    this.antLayers = [];
    if (this.bestIterationLayer) {
      this.map.removeLayer(this.bestIterationLayer);
      this.bestIterationLayer = undefined;
    }
  }

  private drawRoute(coordinates: LocationDto[]): void {
    if (this.routeLayer) {
      this.map.removeLayer(this.routeLayer);
      this.routeLayer = undefined;
    }

    const latLngs: L.LatLngExpression[] = coordinates.map(c => [c.lat, c.lng]);
    this.routeLayer = L.polyline(latLngs, {
      color: '#2563eb',
      weight: 4,
      opacity: 0.8
    }).addTo(this.map);

    this.map.fitBounds(this.routeLayer.getBounds(), { padding: [50, 50] });
  }

  private updateMarkerLabels(order: number[]): void {
    for (let newPos = 0; newPos < order.length; newPos++) {
      const originalIndex = order[newPos];
      const marker = this.markers[originalIndex];
      const isStart = newPos === 0 && this.startIndex !== null;
      const cssClass = isStart ? 'marker-pin start' : 'marker-pin optimized';
      const label = isStart ? '<i class="bi bi-flag-fill"></i>' : `${newPos + 1}`;
      const icon = L.divIcon({
        className: 'custom-marker',
        html: `<div class="${cssClass}">${label}</div>`,
        iconSize: [32, 32],
        iconAnchor: [16, 16]
      });
      marker.setIcon(icon);
    }
  }

  private clearRoute(): void {
    if (this.routeLayer) {
      this.map.removeLayer(this.routeLayer);
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
    this.markers.forEach(m => this.map.removeLayer(m));
    this.markers = [];
    this.locations = [];
    this.startIndex = null;
    this.clearRoute();
    this.clearSearchMarker();
    this.errorMessage = null;
  }

  removePoint(index: number): void {
    this.map.removeLayer(this.markers[index]);
    this.markers.splice(index, 1);
    this.locations.splice(index, 1);

    if (this.startIndex === index) {
      this.startIndex = this.locations.length > 0 ? 0 : null;
    } else if (this.startIndex !== null && this.startIndex > index) {
      this.startIndex--;
    }

    this.rebuildMarkers();
    this.clearRoute();
  }

  focusPoint(index: number): void {
    const loc = this.locations[index];
    this.map.setView([loc.lat, loc.lng], 17);
    this.markers[index].openPopup();
  }

  private rebuildMarkers(): void {
    this.markers.forEach(m => this.map.removeLayer(m));
    this.markers = [];

    for (let i = 0; i < this.locations.length; i++) {
      const loc = this.locations[i];
      const isStart = i === this.startIndex;
      const marker = L.marker([loc.lat, loc.lng], { icon: this.createMarkerIcon(i, isStart) }).addTo(this.map);
      marker.bindPopup(this.createPopupContent(i, isStart));
      this.markers.push(marker);
    }
  }

  fitAllPoints(): void {
    if (this.locations.length === 0) return;
    const group = L.featureGroup(this.markers);
    this.map.fitBounds(group.getBounds(), { padding: [50, 50] });
  }

  switchTab(tab: 'map' | 'points'): void {
    this.activeTab = tab;
    if (tab !== 'map') {
      this.toolsOpen = false;
    }
    if (tab === 'map') {
      setTimeout(() => this.map.invalidateSize(), 50);
    }
  }

  toggleTools(): void {
    this.toolsOpen = !this.toolsOpen;
  }

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

  getOptimizedPosition(originalIndex: number): number | null {
    if (this.bestRouteOrder.length === 0) return null;
    const pos = this.bestRouteOrder.indexOf(originalIndex);
    return pos >= 0 ? pos + 1 : null;
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

    if (waypoints.length > 0) {
      const wp = waypoints.map(w => `${w.lat},${w.lng}`).join('|');
      url += `&waypoints=${wp}`;
    }

    url += '&travelmode=driving';
    window.open(url, '_blank');
  }

  onSearchInput(): void {
    this.searchSubject$.next(this.searchQuery);
  }

  selectSearchResult(result: GeocodingResult): void {
    this.searchQuery = result.displayName;
    this.showResults = false;
    this.clearSearchMarker();

    const searchIcon = L.divIcon({
      className: 'custom-marker',
      html: '<div class="marker-pin search"><i class="bi bi-search"></i></div>',
      iconSize: [32, 32],
      iconAnchor: [16, 16]
    });

    this.searchMarker = L.marker([result.lat, result.lng], { icon: searchIcon }).addTo(this.map);
    this.hasSearchMarker = true;
    this.searchMarker.bindPopup(
      `<strong>${result.displayName.split(',')[0]}</strong><br>` +
      `Lat: ${result.lat.toFixed(5)}<br>Lng: ${result.lng.toFixed(5)}<br><br>` +
      `<em>Clique no mapa neste local para adicionar como ponto</em>`
    ).openPopup();

    this.map.setView([result.lat, result.lng], 16);
  }

  addSearchResultAsPoint(): void {
    if (!this.searchMarker) return;

    const latlng = this.searchMarker.getLatLng();
    this.addPoint(latlng.lat, latlng.lng);
    this.clearSearchMarker();
    this.searchQuery = '';
    this.searchResults = [];
  }

  hideSearchResults(): void {
    setTimeout(() => this.showResults = false, 200);
  }

  clearSearch(): void {
    this.searchQuery = '';
    this.searchResults = [];
    this.showResults = false;
    this.clearSearchMarker();
  }

  private clearSearchMarker(): void {
    if (this.searchMarker) {
      this.map.removeLayer(this.searchMarker);
      this.searchMarker = undefined;
    }
    this.hasSearchMarker = false;
  }
}
