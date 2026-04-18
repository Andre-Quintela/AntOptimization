import { Component, AfterViewInit, OnDestroy, Output, EventEmitter, ElementRef } from '@angular/core';
import * as L from 'leaflet';

@Component({
  selector: 'app-map-view',
  templateUrl: './map-view.component.html',
  styleUrls: ['./map-view.component.css']
})
export class MapViewComponent implements AfterViewInit, OnDestroy {
  @Output() mapClick = new EventEmitter<{ lat: number; lng: number }>();
  private map!: L.Map;

  constructor(private el: ElementRef) {}

  ngAfterViewInit(): void {
    const container = this.el.nativeElement.querySelector('.map-container-inner');
    this.map = L.map(container, {
      center: [-19.9208, -43.9378] as L.LatLngExpression,
      zoom: 13,
      zoomControl: false
    });
    if (window.innerWidth > 768) {
      L.control.zoom({ position: 'topleft' }).addTo(this.map);
    }
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    }).addTo(this.map);
    this.map.on('click', (e: L.LeafletMouseEvent) => {
      this.mapClick.emit({ lat: e.latlng.lat, lng: e.latlng.lng });
    });
  }

  ngOnDestroy(): void {
    this.map?.remove();
  }

  getMap(): L.Map { return this.map; }
  invalidateSize(): void { this.map?.invalidateSize(); }
}
