import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { RouterTestingModule } from '@angular/router/testing';
import { FormsModule } from '@angular/forms';
import { of, throwError, Subject } from 'rxjs';
import * as L from 'leaflet';

import { MapPageComponent } from './map-page.component';
import { RouteService } from '../../services/route.service';
import { GeocodingService } from '../../services/geocoding.service';
import { LocationStateService } from '../../services/location-state.service';
import { PointsStorageService, SavedPoints } from '../../services/points-storage.service';
import { MapViewComponent } from '../../components/map-view/map-view.component';
import { OptimizationResponse, IterationEvent, VisualOptimizationResult } from '../../models/route.models';
import { GeocodingResult } from '../../models/geocoding.models';

// ---------------------------------------------------------------------------
// Stubs
// ---------------------------------------------------------------------------
const createMapStub = () => ({
  on: jasmine.createSpy('on'),
  remove: jasmine.createSpy('remove'),
  removeLayer: jasmine.createSpy('removeLayer'),
  addLayer: jasmine.createSpy('addLayer'),
  flyTo: jasmine.createSpy('flyTo'),
  setView: jasmine.createSpy('setView'),
  fitBounds: jasmine.createSpy('fitBounds'),
  getCenter: jasmine.createSpy('getCenter').and.returnValue({ lat: -19.9, lng: -43.9 }),
  invalidateSize: jasmine.createSpy('invalidateSize')
});

const createMarkerStub = (): Record<string, jasmine.Spy> => {
  const stub: Record<string, jasmine.Spy> = {
    addTo: jasmine.createSpy('addTo'),
    bindPopup: jasmine.createSpy('bindPopup'),
    setIcon: jasmine.createSpy('setIcon'),
    setPopupContent: jasmine.createSpy('setPopupContent'),
    openPopup: jasmine.createSpy('openPopup'),
    getLatLng: jasmine.createSpy('getLatLng').and.returnValue({ lat: -19.9, lng: -43.9 }),
    remove: jasmine.createSpy('remove')
  };
  stub['addTo'].and.returnValue(stub);
  stub['bindPopup'].and.returnValue(stub);
  stub['setIcon'].and.returnValue(stub);
  stub['setPopupContent'].and.returnValue(stub);
  stub['openPopup'].and.returnValue(stub);
  return stub;
};

const createPolylineStub = () => {
  const stub: Record<string, jasmine.Spy> = {
    addTo: jasmine.createSpy('addTo'),
    getBounds: jasmine.createSpy('getBounds').and.returnValue({}),
    remove: jasmine.createSpy('remove')
  };
  stub['addTo'].and.returnValue(stub);
  return stub;
};

const createFeatureGroupStub = () => ({
  getBounds: jasmine.createSpy('getBounds').and.returnValue({})
});

// ---------------------------------------------------------------------------
// Test data
// ---------------------------------------------------------------------------
const mockOptimizationResponse: OptimizationResponse = {
  bestRouteOrder: [0, 1],
  totalDistance: 10.5,
  routeCoordinates: [{ lat: -19.9, lng: -43.9 }, { lat: -19.8, lng: -43.8 }]
};

const mockGeocodingResult: GeocodingResult = {
  name: 'Praça da Liberdade',
  type: 'Parque',
  address: 'Belo Horizonte, MG',
  displayName: 'Praça da Liberdade, Belo Horizonte, MG',
  lat: -19.9321,
  lng: -43.9386
};

// ---------------------------------------------------------------------------
// Suite
// ---------------------------------------------------------------------------
describe('MapPageComponent', () => {
  let component: MapPageComponent;
  let fixture: ComponentFixture<MapPageComponent>;
  let routeServiceSpy: jasmine.SpyObj<RouteService>;
  let geocodingServiceSpy: jasmine.SpyObj<GeocodingService>;
  let locationStateSpy: jasmine.SpyObj<LocationStateService>;
  let pointsStorageSpy: jasmine.SpyObj<PointsStorageService>;
  let mapViewStub: jasmine.SpyObj<MapViewComponent>;
  let mapStub: ReturnType<typeof createMapStub>;

  beforeEach(async () => {
    routeServiceSpy = jasmine.createSpyObj('RouteService', ['optimizeRoute', 'optimizeRouteVisual']);
    geocodingServiceSpy = jasmine.createSpyObj('GeocodingService', ['search']);
    geocodingServiceSpy.search.and.returnValue(of([]));
    locationStateSpy = jasmine.createSpyObj('LocationStateService', ['sync']);
    pointsStorageSpy = jasmine.createSpyObj('PointsStorageService', [
      'saveToLocalStorage', 'loadFromLocalStorage', 'hasSavedPoints', 'exportToCsv', 'parseCsv'
    ]);
    pointsStorageSpy.hasSavedPoints.and.returnValue(false);

    mapStub = createMapStub();
    mapViewStub = jasmine.createSpyObj('MapViewComponent', ['getMap', 'invalidateSize']);
    mapViewStub.getMap.and.returnValue(mapStub as unknown as L.Map);

    spyOn(L, 'marker').and.callFake(() => createMarkerStub() as unknown as L.Marker);
    (spyOn(L, 'polyline') as jasmine.Spy).and.callFake(() => createPolylineStub() as unknown as L.Polyline);
    spyOn(L, 'divIcon').and.returnValue({} as L.DivIcon);
    spyOn(L, 'featureGroup').and.returnValue(createFeatureGroupStub() as unknown as L.FeatureGroup);
    spyOn(L, 'latLngBounds').and.returnValue({} as L.LatLngBounds);

    await TestBed.configureTestingModule({
      imports: [RouterTestingModule, FormsModule],
      declarations: [MapPageComponent],
      providers: [
        { provide: RouteService, useValue: routeServiceSpy },
        { provide: GeocodingService, useValue: geocodingServiceSpy },
        { provide: LocationStateService, useValue: locationStateSpy },
        { provide: PointsStorageService, useValue: pointsStorageSpy }
      ],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();

    fixture = TestBed.createComponent(MapPageComponent);
    component = fixture.componentInstance;

    spyOn(component, 'goToMyLocation');
    fixture.detectChanges();

    // Override the @ViewChild after Angular's resolution (NO_ERRORS_SCHEMA yields undefined)
    (component as unknown as { mapView: MapViewComponent }).mapView = mapViewStub;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with empty locations', () => {
    expect(component.locations).toEqual([]);
    expect(component.startIndex).toBeNull();
  });

  // -------------------------------------------------------------------------
  // Point management
  // -------------------------------------------------------------------------
  describe('onMapClick()', () => {
    it('should add a location', () => {
      component.onMapClick({ lat: -19.9, lng: -43.9 });
      expect(component.locations.length).toBe(1);
    });

    it('should set startIndex to 0 for the first point', () => {
      component.onMapClick({ lat: -19.9, lng: -43.9 });
      expect(component.startIndex).toBe(0);
    });

    it('should NOT change startIndex when a second point is added', () => {
      component.onMapClick({ lat: -19.9, lng: -43.9 });
      component.onMapClick({ lat: -19.8, lng: -43.8 });
      expect(component.startIndex).toBe(0);
    });
  });

  describe('removePoint()', () => {
    beforeEach(() => {
      component.onMapClick({ lat: -19.9, lng: -43.9 });
      component.onMapClick({ lat: -19.8, lng: -43.8 });
    });

    it('should remove the correct location', () => {
      component.removePoint(0);
      expect(component.locations.length).toBe(1);
      expect(component.locations[0]).toEqual({ lat: -19.8, lng: -43.8 });
    });

    it('should decrement startIndex when a point before start is removed', () => {
      component.setAsStart(1);
      component.removePoint(0);
      expect(component.startIndex).toBe(0);
    });

    it('should set startIndex to 0 when start point is removed and locations remain', () => {
      expect(component.startIndex).toBe(0);
      component.removePoint(0);
      expect(component.startIndex).toBe(0);
    });
  });

  describe('setAsStart()', () => {
    beforeEach(() => {
      component.onMapClick({ lat: -19.9, lng: -43.9 });
      component.onMapClick({ lat: -19.8, lng: -43.8 });
    });

    it('should change startIndex to the given index', () => {
      component.setAsStart(1);
      expect(component.startIndex).toBe(1);
    });
  });

  describe('clearAll()', () => {
    beforeEach(() => {
      component.onMapClick({ lat: -19.9, lng: -43.9 });
    });

    it('should clear all locations', () => {
      component.clearAll();
      expect(component.locations.length).toBe(0);
    });

    it('should reset startIndex, totalDistance, and errorMessage', () => {
      component.totalDistance = 10;
      component.errorMessage = 'some error';
      component.clearAll();
      expect(component.startIndex).toBeNull();
      expect(component.totalDistance).toBeNull();
      expect(component.errorMessage).toBeNull();
    });
  });

  // -------------------------------------------------------------------------
  // Route optimization
  // -------------------------------------------------------------------------
  describe('optimizeRoute()', () => {
    it('should set errorMessage and NOT call service when fewer than 2 points', () => {
      component.onMapClick({ lat: -19.9, lng: -43.9 });
      component.optimizeRoute();
      expect(component.errorMessage).toBeTruthy();
      expect(routeServiceSpy.optimizeRoute).not.toHaveBeenCalled();
    });

    it('should call RouteService.optimizeRoute() with correct locations', () => {
      routeServiceSpy.optimizeRoute.and.returnValue(of(mockOptimizationResponse));
      component.onMapClick({ lat: -19.9, lng: -43.9 });
      component.onMapClick({ lat: -19.8, lng: -43.8 });
      component.optimizeRoute();
      expect(routeServiceSpy.optimizeRoute).toHaveBeenCalledWith(
        jasmine.objectContaining({ locations: component.locations })
      );
    });

    it('should update totalDistance and bestRouteOrder on success', () => {
      routeServiceSpy.optimizeRoute.and.returnValue(of(mockOptimizationResponse));
      component.onMapClick({ lat: -19.9, lng: -43.9 });
      component.onMapClick({ lat: -19.8, lng: -43.8 });
      component.optimizeRoute();
      expect(component.totalDistance).toBe(10.5);
      expect(component.bestRouteOrder).toEqual([0, 1]);
    });

    it('should set errorMessage on service error', () => {
      routeServiceSpy.optimizeRoute.and.returnValue(throwError(() => new Error('Network error')));
      component.onMapClick({ lat: -19.9, lng: -43.9 });
      component.onMapClick({ lat: -19.8, lng: -43.8 });
      component.optimizeRoute();
      expect(component.errorMessage).toBeTruthy();
      expect(component.isOptimizing).toBeFalse();
    });
  });

  describe('optimizeRoute() in visual mode', () => {
    it('should call optimizeRouteVisual when visualMode is true', () => {
      const events$ = new Subject<IterationEvent | VisualOptimizationResult>();
      routeServiceSpy.optimizeRouteVisual.and.returnValue(events$.asObservable());
      component.onMapClick({ lat: -19.9, lng: -43.9 });
      component.onMapClick({ lat: -19.8, lng: -43.8 });
      component.visualMode = true;
      component.optimizeRoute();
      expect(routeServiceSpy.optimizeRouteVisual).toHaveBeenCalled();
    });

    it('should update currentIteration on iteration event', () => {
      const events$ = new Subject<IterationEvent | VisualOptimizationResult>();
      routeServiceSpy.optimizeRouteVisual.and.returnValue(events$.asObservable());
      component.onMapClick({ lat: -19.9, lng: -43.9 });
      component.onMapClick({ lat: -19.8, lng: -43.8 });
      component.visualMode = true;
      component.optimizeRoute();

      const iterEvent: IterationEvent = {
        type: 'iteration', iteration: 4, totalIterations: 10,
        bestTourSoFar: [0, 1], bestDistanceSoFar: 15, antTours: [[0, 1]]
      };
      events$.next(iterEvent);

      expect(component.currentIteration).toBe(5);
      expect(component.totalIterations).toBe(10);
    });

    it('should finalize state on result event', () => {
      const events$ = new Subject<IterationEvent | VisualOptimizationResult>();
      routeServiceSpy.optimizeRouteVisual.and.returnValue(events$.asObservable());
      component.onMapClick({ lat: -19.9, lng: -43.9 });
      component.onMapClick({ lat: -19.8, lng: -43.8 });
      component.visualMode = true;
      component.optimizeRoute();

      events$.next({
        type: 'result', bestRouteOrder: [1, 0], totalDistance: 20,
        routeCoordinates: [{ lat: -19.8, lng: -43.8 }, { lat: -19.9, lng: -43.9 }]
      } as VisualOptimizationResult);

      expect(component.totalDistance).toBe(20);
      expect(component.isVisualizing).toBeFalse();
    });
  });

  // -------------------------------------------------------------------------
  // UI helpers
  // -------------------------------------------------------------------------
  describe('switchTab()', () => {
    it('should switch to the given tab', () => {
      component.switchTab('points');
      expect(component.activeTab).toBe('points');
    });

    it('should close tools drawer when switching away from map tab', () => {
      component.toolsOpen = true;
      component.switchTab('points');
      expect(component.toolsOpen).toBeFalse();
    });
  });

  describe('exportToGoogleMaps()', () => {
    it('should open a Google Maps URL', () => {
      spyOn(window, 'open');
      component.locations = [{ lat: -19.9, lng: -43.9 }, { lat: -19.8, lng: -43.8 }];
      component.bestRouteOrder = [0, 1];
      component.exportToGoogleMaps();
      expect(window.open).toHaveBeenCalledWith(
        jasmine.stringMatching(/origin=-19\.9,-43\.9/), '_blank'
      );
    });

    it('should NOT open URL when fewer than 2 points in bestRouteOrder', () => {
      spyOn(window, 'open');
      component.bestRouteOrder = [0];
      component.exportToGoogleMaps();
      expect(window.open).not.toHaveBeenCalled();
    });
  });

  // -------------------------------------------------------------------------
  // Storage
  // -------------------------------------------------------------------------
  describe('hasSavedPoints', () => {
    it('should delegate to PointsStorageService', () => {
      pointsStorageSpy.hasSavedPoints.and.returnValue(true);
      expect(component.hasSavedPoints).toBeTrue();
    });
  });

  describe('savePoints()', () => {
    it('should call saveToLocalStorage with current state', () => {
      component.onMapClick({ lat: -19.9, lng: -43.9 });
      component.savePoints();
      expect(pointsStorageSpy.saveToLocalStorage).toHaveBeenCalledWith(
        component.locations, component.startIndex
      );
    });
  });

  describe('loadSavedPoints()', () => {
    it('should apply saved locations', () => {
      const saved: SavedPoints = {
        locations: [{ lat: -19.9, lng: -43.9 }, { lat: -19.8, lng: -43.8 }],
        startIndex: 1
      };
      pointsStorageSpy.loadFromLocalStorage.and.returnValue(saved);
      component.loadSavedPoints();
      expect(component.locations.length).toBe(2);
    });

    it('should do nothing when loadFromLocalStorage returns null', () => {
      pointsStorageSpy.loadFromLocalStorage.and.returnValue(null);
      component.loadSavedPoints();
      expect(component.locations).toEqual([]);
    });
  });

  describe('onCsvFileSelected()', () => {
    it('should do nothing when no file provided', () => {
      const event = { target: { files: null, value: '' } } as unknown as Event;
      component.onCsvFileSelected(event);
      expect(pointsStorageSpy.parseCsv).not.toHaveBeenCalled();
    });

    it('should set errorMessage when parseCsv throws', (done) => {
      pointsStorageSpy.parseCsv.and.throwError('CSV inválido: o cabeçalho deve ser "lat,lng,is_start".');
      const csvContent = 'bad,header\n1,2,3';
      const mockReader = {
        onload: null as ((e: ProgressEvent<FileReader>) => void) | null,
        readAsText: jasmine.createSpy('readAsText').and.callFake(function(this: typeof mockReader) {
          if (this.onload) this.onload({ target: { result: csvContent } } as unknown as ProgressEvent<FileReader>);
        })
      };
      spyOn(window as Window & typeof globalThis, 'FileReader' as keyof Window).and.returnValue(mockReader as unknown as FileReader);
      const mockFile = new File([csvContent], 'bad.csv', { type: 'text/csv' });
      const inputEl = { files: [mockFile], value: '' } as unknown as HTMLInputElement;
      component.onCsvFileSelected({ target: inputEl } as unknown as Event);
      expect(component.errorMessage).toContain('CSV inválido');
      done();
    });
  });
});
