import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { RouterTestingModule } from '@angular/router/testing';
import { FormsModule } from '@angular/forms';
import { of, throwError } from 'rxjs';
import * as L from 'leaflet';

import { DashboardComponent } from './dashboard.component';
import { RouteService } from '../../services/route.service';
import { GeocodingService } from '../../services/geocoding.service';
import { CompareOptimizationResponse } from '../../models/route.models';

// ---------------------------------------------------------------------------
// Leaflet stubs
// ---------------------------------------------------------------------------
const createMapStub = () => ({
  on: jasmine.createSpy('on'),
  remove: jasmine.createSpy('remove'),
  fitBounds: jasmine.createSpy('fitBounds'),
  getCenter: jasmine.createSpy('getCenter').and.returnValue({ lat: -19.9, lng: -43.9 })
});

const createPolylineStub = () => {
  const stub: Record<string, jasmine.Spy> = {
    addTo: jasmine.createSpy('addTo'),
    remove: jasmine.createSpy('remove'),
    getLatLngs: jasmine.createSpy('getLatLngs').and.returnValue([])
  };
  stub['addTo'].and.returnValue(stub);
  return stub;
};

const createMarkerStub = () => {
  const stub: Record<string, jasmine.Spy> = {
    addTo: jasmine.createSpy('addTo'),
    remove: jasmine.createSpy('remove'),
    on: jasmine.createSpy('on')
  };
  stub['addTo'].and.returnValue(stub);
  stub['on'].and.returnValue(stub);
  return stub;
};

// ---------------------------------------------------------------------------
// Mock response
// ---------------------------------------------------------------------------
const mockCompareResponse: CompareOptimizationResponse = {
  results: [
    { algorithm: 'ACO', bestRouteOrder: [0, 1], totalDistance: 12.4, executionTimeMs: 320, routeCoordinates: [] },
    { algorithm: 'Nearest Neighbor', bestRouteOrder: [0, 1], totalDistance: 13.1, executionTimeMs: 18, routeCoordinates: [] },
    { algorithm: '2-Opt', bestRouteOrder: [0, 1], totalDistance: 12.6, executionTimeMs: 45, routeCoordinates: [] },
    { algorithm: 'Genetic Algorithm', bestRouteOrder: [0, 1], totalDistance: 12.5, executionTimeMs: 280, routeCoordinates: [] }
  ]
};

// ---------------------------------------------------------------------------
// Suite
// ---------------------------------------------------------------------------
describe('DashboardComponent', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;
  let routeServiceSpy: jasmine.SpyObj<RouteService>;
  let geocodingServiceSpy: jasmine.SpyObj<GeocodingService>;

  beforeEach(async () => {
    routeServiceSpy = jasmine.createSpyObj('RouteService', ['compareRoutes']);
    geocodingServiceSpy = jasmine.createSpyObj('GeocodingService', ['search']);
    geocodingServiceSpy.search.and.returnValue(of([]));

    spyOn(L, 'map').and.returnValue(createMapStub() as unknown as L.Map);
    spyOn(L, 'tileLayer').and.returnValue({ addTo: jasmine.createSpy() } as unknown as L.TileLayer);
    spyOn(L, 'marker').and.callFake(() => createMarkerStub() as unknown as L.Marker);
    (spyOn(L, 'polyline') as jasmine.Spy).and.callFake(() => createPolylineStub() as unknown as L.Polyline);
    spyOn(L, 'divIcon').and.returnValue({} as L.DivIcon);
    spyOn(L, 'latLngBounds').and.returnValue({} as L.LatLngBounds);

    await TestBed.configureTestingModule({
      imports: [RouterTestingModule, FormsModule],
      declarations: [DashboardComponent],
      providers: [
        { provide: RouteService, useValue: routeServiceSpy },
        { provide: GeocodingService, useValue: geocodingServiceSpy }
      ],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => {
    component.ngOnDestroy();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should disable compare button when fewer than 2 locations', () => {
    component.locations = [{ lat: -19.9, lng: -43.9 }];
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('.btn-primary.btn-block') as HTMLButtonElement;
    expect(button.disabled).toBeTrue();
  });

  it('should enable compare button when 2 or more locations are present', () => {
    component.locations = [
      { lat: -19.9, lng: -43.9 },
      { lat: -19.8, lng: -43.8 }
    ];
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('.btn-primary.btn-block') as HTMLButtonElement;
    expect(button.disabled).toBeFalse();
  });

  it('should display 4 result rows after a successful comparison', () => {
    routeServiceSpy.compareRoutes.and.returnValue(of(mockCompareResponse));

    component.locations = [
      { lat: -19.9, lng: -43.9 },
      { lat: -19.8, lng: -43.8 }
    ];
    component.compare();
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('.results-table tbody tr');
    expect(rows.length).toBe(4);
  });

  it('should mark the algorithm with the lowest distance as best', () => {
    routeServiceSpy.compareRoutes.and.returnValue(of(mockCompareResponse));

    component.locations = [
      { lat: -19.9, lng: -43.9 },
      { lat: -19.8, lng: -43.8 }
    ];
    component.compare();

    expect(component.bestAlgorithm).toBe('ACO');
  });

  it('should call compareRoutes with the current locations', () => {
    routeServiceSpy.compareRoutes.and.returnValue(of(mockCompareResponse));

    component.locations = [
      { lat: -19.9, lng: -43.9 },
      { lat: -19.8, lng: -43.8 }
    ];
    component.compare();

    expect(routeServiceSpy.compareRoutes).toHaveBeenCalledWith(
      jasmine.objectContaining({
        locations: [{ lat: -19.9, lng: -43.9 }, { lat: -19.8, lng: -43.8 }]
      })
    );
  });

  it('should set errorMessage on service error', () => {
    routeServiceSpy.compareRoutes.and.returnValue(throwError(() => new Error('Network error')));

    component.locations = [
      { lat: -19.9, lng: -43.9 },
      { lat: -19.8, lng: -43.8 }
    ];
    component.compare();

    expect(component.errorMessage).toBeTruthy();
    expect(component.isComparing).toBeFalse();
  });
});
