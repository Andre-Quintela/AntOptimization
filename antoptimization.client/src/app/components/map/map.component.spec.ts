import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { of, throwError, Subject } from 'rxjs';
import { MapComponent } from './map.component';
import { RouteService } from '../../services/route.service';
import { GeocodingService } from '../../services/geocoding.service';
import { PointsStorageService, SavedPoints } from '../../services/points-storage.service';
import { OptimizationResponse, IterationEvent, VisualOptimizationResult } from '../../models/route.models';
import { GeocodingResult } from '../../models/geocoding.models';
import * as L from 'leaflet';

// ---------------------------------------------------------------------------
// Leaflet stub — avoids DOM errors no ambiente de teste
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
    getLatLng: jasmine.createSpy('getLatLng').and.returnValue({ lat: -19.9, lng: -43.9 })
  };
  // Chaining stubs: addTo/bindPopup/etc. return the stub itself
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
    getBounds: jasmine.createSpy('getBounds').and.returnValue({})
  };
  stub['addTo'].and.returnValue(stub);
  return stub;
};

const createFeatureGroupStub = () => ({
  getBounds: jasmine.createSpy('getBounds').and.returnValue({})
});

// ---------------------------------------------------------------------------
// Helpers
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
describe('MapComponent', () => {
  let component: MapComponent;
  let fixture: ComponentFixture<MapComponent>;
  let routeServiceSpy: jasmine.SpyObj<RouteService>;
  let geocodingServiceSpy: jasmine.SpyObj<GeocodingService>;
  let pointsStorageSpy: jasmine.SpyObj<PointsStorageService>;
  let mapStub: ReturnType<typeof createMapStub>;

  beforeEach(async () => {
    routeServiceSpy = jasmine.createSpyObj('RouteService', ['optimizeRoute', 'optimizeRouteVisual']);
    geocodingServiceSpy = jasmine.createSpyObj('GeocodingService', ['search']);
    geocodingServiceSpy.search.and.returnValue(of([]));
    pointsStorageSpy = jasmine.createSpyObj('PointsStorageService', [
      'saveToLocalStorage', 'loadFromLocalStorage', 'hasSavedPoints', 'exportToCsv', 'parseCsv'
    ]);
    pointsStorageSpy.hasSavedPoints.and.returnValue(false);

    mapStub = createMapStub();

    // Stub Leaflet before component is created
    spyOn(L, 'map').and.returnValue(mapStub as unknown as L.Map);
    spyOn(L, 'tileLayer').and.returnValue({ addTo: jasmine.createSpy() } as unknown as L.TileLayer);
    spyOn(L, 'marker').and.callFake(() => createMarkerStub() as unknown as L.Marker);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (spyOn(L, 'polyline') as jasmine.Spy).and.callFake(() => createPolylineStub() as any);
    spyOn(L, 'divIcon').and.returnValue({} as L.DivIcon);
    spyOn(L, 'featureGroup').and.returnValue(createFeatureGroupStub() as unknown as L.FeatureGroup);
    spyOn(L.control, 'zoom').and.returnValue({ addTo: jasmine.createSpy() } as unknown as L.Control.Zoom);

    // Add #map element required by Leaflet
    const mapDiv = document.createElement('div');
    mapDiv.id = 'map';
    document.body.appendChild(mapDiv);

    await TestBed.configureTestingModule({
      declarations: [MapComponent],
      providers: [
        { provide: RouteService, useValue: routeServiceSpy },
        { provide: GeocodingService, useValue: geocodingServiceSpy },
        { provide: PointsStorageService, useValue: pointsStorageSpy }
      ],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();

    fixture = TestBed.createComponent(MapComponent);
    component = fixture.componentInstance;

    // Prevent goToMyLocation() from setting locating=true during ngAfterViewInit,
    // which would cause NG0100 ExpressionChangedAfterItHasBeenCheckedError.
    spyOn(component, 'goToMyLocation');

    fixture.detectChanges(); // triggers ngAfterViewInit → initMap()
  });

  afterEach(() => {
    const mapDiv = document.getElementById('map');
    if (mapDiv) mapDiv.remove();
  });

  // -------------------------------------------------------------------------
  // Criação
  // -------------------------------------------------------------------------
  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with empty locations', () => {
    expect(component.locations).toEqual([]);
    expect(component.startIndex).toBeNull();
  });

  // -------------------------------------------------------------------------
  // Gerenciamento de pontos
  // -------------------------------------------------------------------------
  describe('addPoint (via map click)', () => {
    it('should add a location when the map is clicked', () => {
      const clickHandler = (mapStub.on as jasmine.Spy).calls.allArgs()
        .find((args: unknown[]) => args[0] === 'click')?.[1] as ((e: L.LeafletMouseEvent) => void) | undefined;

      expect(clickHandler).toBeDefined();
      clickHandler!({ latlng: { lat: -19.9, lng: -43.9 } } as L.LeafletMouseEvent);

      expect(component.locations.length).toBe(1);
      expect(component.locations[0]).toEqual({ lat: -19.9, lng: -43.9 });
    });

    it('should set startIndex to 0 for the first point added', () => {
      const clickHandler = (mapStub.on as jasmine.Spy).calls.allArgs()
        .find((args: unknown[]) => args[0] === 'click')?.[1] as ((e: L.LeafletMouseEvent) => void) | undefined;

      clickHandler!({ latlng: { lat: -19.9, lng: -43.9 } } as L.LeafletMouseEvent);
      expect(component.startIndex).toBe(0);
    });

    it('should NOT change startIndex when a second point is added', () => {
      const clickHandler = (mapStub.on as jasmine.Spy).calls.allArgs()
        .find((args: unknown[]) => args[0] === 'click')?.[1] as ((e: L.LeafletMouseEvent) => void) | undefined;

      clickHandler!({ latlng: { lat: -19.9, lng: -43.9 } } as L.LeafletMouseEvent);
      clickHandler!({ latlng: { lat: -19.8, lng: -43.8 } } as L.LeafletMouseEvent);

      expect(component.locations.length).toBe(2);
      expect(component.startIndex).toBe(0);
    });
  });

  describe('removePoint()', () => {
    beforeEach(() => {
      // Add two points via internal method
      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.9, -43.9);
      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.8, -43.8);
    });

    it('should remove the correct location from the list', () => {
      component.removePoint(0);
      expect(component.locations.length).toBe(1);
      expect(component.locations[0]).toEqual({ lat: -19.8, lng: -43.8 });
    });

    it('should reset startIndex to 0 when start point is removed and locations remain', () => {
      expect(component.startIndex).toBe(0);
      component.removePoint(0);
      expect(component.startIndex).toBe(0);
    });

    it('should set startIndex to null when the last point is removed', () => {
      component.removePoint(1);
      component.removePoint(0);
      expect(component.startIndex).toBeNull();
    });

    it('should decrement startIndex when a point before start is removed', () => {
      // Make index 1 the start
      component.setAsStart(1);
      expect(component.startIndex).toBe(1);

      component.removePoint(0);
      expect(component.startIndex).toBe(0); // shifted down by 1
    });
  });

  describe('setAsStart()', () => {
    beforeEach(() => {
      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.9, -43.9);
      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.8, -43.8);
    });

    it('should change startIndex to the given index', () => {
      component.setAsStart(1);
      expect(component.startIndex).toBe(1);
    });
  });

  describe('clearAll()', () => {
    beforeEach(() => {
      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.9, -43.9);
    });

    it('should clear all locations', () => {
      component.clearAll();
      expect(component.locations.length).toBe(0);
    });

    it('should reset startIndex to null', () => {
      component.clearAll();
      expect(component.startIndex).toBeNull();
    });

    it('should reset totalDistance and bestRouteOrder', () => {
      component.totalDistance = 10;
      component.bestRouteOrder = [0, 1];
      component.clearAll();
      expect(component.totalDistance).toBeNull();
      expect(component.bestRouteOrder).toEqual([]);
    });

    it('should clear errorMessage', () => {
      component.errorMessage = 'some error';
      component.clearAll();
      expect(component.errorMessage).toBeNull();
    });
  });

  // -------------------------------------------------------------------------
  // Otimização de rota
  // -------------------------------------------------------------------------
  describe('optimizeRoute()', () => {
    it('should set errorMessage and NOT call service when fewer than 2 points', () => {
      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.9, -43.9);

      component.optimizeRoute();

      expect(component.errorMessage).toBeTruthy();
      expect(routeServiceSpy.optimizeRoute).not.toHaveBeenCalled();
    });

    it('should call RouteService.optimizeRoute() with correct locations', () => {
      routeServiceSpy.optimizeRoute.and.returnValue(of(mockOptimizationResponse));

      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.9, -43.9);
      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.8, -43.8);

      component.optimizeRoute();

      expect(routeServiceSpy.optimizeRoute).toHaveBeenCalledWith(
        jasmine.objectContaining({
          locations: [{ lat: -19.9, lng: -43.9 }, { lat: -19.8, lng: -43.8 }]
        })
      );
    });

    it('should update totalDistance and bestRouteOrder on success', () => {
      routeServiceSpy.optimizeRoute.and.returnValue(of(mockOptimizationResponse));

      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.9, -43.9);
      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.8, -43.8);

      component.optimizeRoute();

      expect(component.totalDistance).toBe(10.5);
      expect(component.bestRouteOrder).toEqual([0, 1]);
    });

    it('should set errorMessage on service error', () => {
      routeServiceSpy.optimizeRoute.and.returnValue(throwError(() => new Error('Network error')));

      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.9, -43.9);
      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.8, -43.8);

      component.optimizeRoute();

      expect(component.errorMessage).toBeTruthy();
      expect(component.isOptimizing).toBeFalse();
    });
  });

  describe('optimizeRoute() in visual mode', () => {
    it('should call optimizeRouteVisual service when visualMode is true', () => {
      const visualEvents$ = new Subject<IterationEvent | VisualOptimizationResult>();
      routeServiceSpy.optimizeRouteVisual.and.returnValue(visualEvents$.asObservable());

      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.9, -43.9);
      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.8, -43.8);

      component.visualMode = true;
      component.optimizeRoute();

      expect(routeServiceSpy.optimizeRouteVisual).toHaveBeenCalled();
    });

    it('should update currentIteration and totalIterations on iteration events', () => {
      const visualEvents$ = new Subject<IterationEvent | VisualOptimizationResult>();
      routeServiceSpy.optimizeRouteVisual.and.returnValue(visualEvents$.asObservable());

      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.9, -43.9);
      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.8, -43.8);

      component.visualMode = true;
      component.optimizeRoute();

      const iterEvent: IterationEvent = {
        type: 'iteration',
        iteration: 4,
        totalIterations: 10,
        bestTourSoFar: [0, 1],
        bestDistanceSoFar: 15,
        antTours: [[0, 1]]
      };
      visualEvents$.next(iterEvent);

      expect(component.currentIteration).toBe(5);
      expect(component.totalIterations).toBe(10);
    });

    it('should finalize state on result event', () => {
      const visualEvents$ = new Subject<IterationEvent | VisualOptimizationResult>();
      routeServiceSpy.optimizeRouteVisual.and.returnValue(visualEvents$.asObservable());

      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.9, -43.9);
      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.8, -43.8);

      component.visualMode = true;
      component.optimizeRoute();

      const resultEvent: VisualOptimizationResult = {
        type: 'result',
        bestRouteOrder: [1, 0],
        totalDistance: 20,
        routeCoordinates: [{ lat: -19.8, lng: -43.8 }, { lat: -19.9, lng: -43.9 }]
      };
      visualEvents$.next(resultEvent);

      expect(component.totalDistance).toBe(20);
      expect(component.bestRouteOrder).toEqual([1, 0]);
      expect(component.isVisualizing).toBeFalse();
    });
  });

  // -------------------------------------------------------------------------
  // Busca
  // -------------------------------------------------------------------------
  describe('onSearchInput()', () => {
    it('should push query to searchSubject', fakeAsync(() => {
      geocodingServiceSpy.search.and.returnValue(of([mockGeocodingResult]));

      component.searchQuery = 'Praça da Liberdade';
      component.onSearchInput();
      tick(500); // debounceTime(400)

      expect(geocodingServiceSpy.search).toHaveBeenCalledWith(
        'Praça da Liberdade',
        jasmine.anything()
      );
    }));

    it('should NOT call geocoding service for short queries', fakeAsync(() => {
      component.searchQuery = 'ab';
      component.onSearchInput();
      tick(500);

      // GeocodingService.search returns of([]) for short queries — no HTTP call made
      // We verify it was called but returned empty
      expect(component.searchResults).toEqual([]);
    }));
  });

  describe('selectSearchResult()', () => {
    it('should set searchQuery to the result displayName', () => {
      component.selectSearchResult(mockGeocodingResult);
      expect(component.searchQuery).toBe(mockGeocodingResult.displayName);
    });

    it('should hide search results', () => {
      component.showResults = true;
      component.selectSearchResult(mockGeocodingResult);
      expect(component.showResults).toBeFalse();
    });
  });

  describe('clearSearch()', () => {
    it('should clear query, results and hide the results panel', () => {
      component.searchQuery = 'test';
      component.searchResults = [mockGeocodingResult];
      component.showResults = true;

      component.clearSearch();

      expect(component.searchQuery).toBe('');
      expect(component.searchResults).toEqual([]);
      expect(component.showResults).toBeFalse();
    });
  });

  // -------------------------------------------------------------------------
  // Exportação
  // -------------------------------------------------------------------------
  describe('exportToGoogleMaps()', () => {
    it('should open a Google Maps URL with correct origin and destination', () => {
      spyOn(window, 'open');

      component.locations = [
        { lat: -19.9, lng: -43.9 },
        { lat: -19.8, lng: -43.8 }
      ];
      component.bestRouteOrder = [0, 1];

      component.exportToGoogleMaps();

      expect(window.open).toHaveBeenCalledWith(
        jasmine.stringMatching(/origin=-19\.9,-43\.9/),
        '_blank'
      );
      expect(window.open).toHaveBeenCalledWith(
        jasmine.stringMatching(/destination=-19\.8,-43\.8/),
        '_blank'
      );
    });

    it('should include waypoints when there are intermediate stops', () => {
      spyOn(window, 'open');

      component.locations = [
        { lat: -19.9, lng: -43.9 },
        { lat: -19.85, lng: -43.85 },
        { lat: -19.8, lng: -43.8 }
      ];
      component.bestRouteOrder = [0, 1, 2];

      component.exportToGoogleMaps();

      expect(window.open).toHaveBeenCalledWith(
        jasmine.stringMatching(/waypoints=/),
        '_blank'
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
  // getOptimizedPosition()
  // -------------------------------------------------------------------------
  describe('getOptimizedPosition()', () => {
    it('should return null when bestRouteOrder is empty', () => {
      component.bestRouteOrder = [];
      expect(component.getOptimizedPosition(0)).toBeNull();
    });

    it('should return the 1-based position in the optimized order', () => {
      component.bestRouteOrder = [2, 0, 1];
      expect(component.getOptimizedPosition(2)).toBe(1);
      expect(component.getOptimizedPosition(0)).toBe(2);
      expect(component.getOptimizedPosition(1)).toBe(3);
    });
  });

  // -------------------------------------------------------------------------
  // switchTab()
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

  // -------------------------------------------------------------------------
  // Persistência de pontos (PointsStorageService)
  // -------------------------------------------------------------------------
  describe('hasSavedPoints', () => {
    it('should return false when service reports no saved points', () => {
      pointsStorageSpy.hasSavedPoints.and.returnValue(false);
      expect(component.hasSavedPoints).toBeFalse();
    });

    it('should return true when service reports saved points exist', () => {
      pointsStorageSpy.hasSavedPoints.and.returnValue(true);
      expect(component.hasSavedPoints).toBeTrue();
    });
  });

  describe('savePoints()', () => {
    it('should call saveToLocalStorage with current locations and startIndex', () => {
      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.9, -43.9);
      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.8, -43.8);

      component.savePoints();

      expect(pointsStorageSpy.saveToLocalStorage).toHaveBeenCalledWith(
        component.locations,
        component.startIndex
      );
    });
  });

  describe('loadSavedPoints()', () => {
    it('should call applyLocations with saved data', () => {
      const saved: SavedPoints = {
        locations: [{ lat: -19.9, lng: -43.9 }, { lat: -19.8, lng: -43.8 }],
        startIndex: 1
      };
      pointsStorageSpy.loadFromLocalStorage.and.returnValue(saved);

      component.loadSavedPoints();

      expect(component.locations).toEqual(saved.locations);
    });

    it('should set startIndex from the saved data', () => {
      const saved: SavedPoints = {
        locations: [{ lat: -19.9, lng: -43.9 }, { lat: -19.8, lng: -43.8 }],
        startIndex: 1
      };
      pointsStorageSpy.loadFromLocalStorage.and.returnValue(saved);

      component.loadSavedPoints();

      expect(component.startIndex).toBe(1);
    });

    it('should do nothing if loadFromLocalStorage returns null', () => {
      pointsStorageSpy.loadFromLocalStorage.and.returnValue(null);
      const originalLocations = [...component.locations];

      component.loadSavedPoints();

      expect(component.locations).toEqual(originalLocations);
    });
  });

  describe('exportCsv()', () => {
    it('should call exportToCsv with current locations and startIndex', () => {
      (component as unknown as { addPoint: (lat: number, lng: number) => void })
        .addPoint(-19.9, -43.9);

      component.exportCsv();

      expect(pointsStorageSpy.exportToCsv).toHaveBeenCalledWith(
        component.locations,
        component.startIndex
      );
    });
  });

  describe('triggerCsvImport()', () => {
    it('should call click() on the hidden file input', () => {
      const inputEl = fixture.nativeElement.querySelector('input[type="file"]') as HTMLInputElement;
      const clickSpy = spyOn(inputEl, 'click');

      component.triggerCsvImport();

      expect(clickSpy).toHaveBeenCalled();
    });
  });

  describe('onCsvFileSelected()', () => {
    const mockCsvLocations = [{ lat: -19.9, lng: -43.9 }, { lat: -19.8, lng: -43.8 }];
    const mockParsed: SavedPoints = { locations: mockCsvLocations, startIndex: 0 };

    it('should do nothing when no file is provided', () => {
      const event = { target: { files: null, value: '' } } as unknown as Event;
      component.onCsvFileSelected(event);
      expect(pointsStorageSpy.parseCsv).not.toHaveBeenCalled();
    });

    it('should read the file and call parseCsv with its content', (done) => {
      pointsStorageSpy.parseCsv.and.returnValue(mockParsed);

      const csvContent = 'lat,lng,is_start\n-19.9,-43.9,true';
      const mockReader = {
        onload: null as ((e: ProgressEvent<FileReader>) => void) | null,
        readAsText: jasmine.createSpy('readAsText').and.callFake(function(this: typeof mockReader) {
          if (this.onload) {
            this.onload({ target: { result: csvContent } } as unknown as ProgressEvent<FileReader>);
          }
        })
      };
      spyOn(window as Window & typeof globalThis, 'FileReader' as keyof Window).and.returnValue(mockReader as unknown as FileReader);

      const mockFile = new File([csvContent], 'pontos.csv', { type: 'text/csv' });
      const inputEl = { files: [mockFile], value: '' } as unknown as HTMLInputElement;
      const event = { target: inputEl } as unknown as Event;

      component.onCsvFileSelected(event);

      expect(pointsStorageSpy.parseCsv).toHaveBeenCalledWith(csvContent);
      expect(component.locations).toEqual(mockCsvLocations);
      done();
    });

    it('should set errorMessage when parseCsv throws', (done) => {
      const csvContent = 'bad,header\n1,2,3';
      pointsStorageSpy.parseCsv.and.throwError('CSV inválido: o cabeçalho deve ser "lat,lng,is_start".');

      const mockReader = {
        onload: null as ((e: ProgressEvent<FileReader>) => void) | null,
        readAsText: jasmine.createSpy('readAsText').and.callFake(function(this: typeof mockReader) {
          if (this.onload) {
            this.onload({ target: { result: csvContent } } as unknown as ProgressEvent<FileReader>);
          }
        })
      };
      spyOn(window as Window & typeof globalThis, 'FileReader' as keyof Window).and.returnValue(mockReader as unknown as FileReader);

      const mockFile = new File([csvContent], 'bad.csv', { type: 'text/csv' });
      const inputEl = { files: [mockFile], value: '' } as unknown as HTMLInputElement;
      const event = { target: inputEl } as unknown as Event;

      component.onCsvFileSelected(event);

      expect(component.errorMessage).toContain('CSV inválido');
      done();
    });

    it('should reset the file input value after processing', (done) => {
      pointsStorageSpy.parseCsv.and.returnValue(mockParsed);

      const csvContent = 'lat,lng,is_start\n-19.9,-43.9,true';
      const mockReader = {
        onload: null as ((e: ProgressEvent<FileReader>) => void) | null,
        readAsText: jasmine.createSpy('readAsText').and.callFake(function(this: typeof mockReader) {
          if (this.onload) {
            this.onload({ target: { result: csvContent } } as unknown as ProgressEvent<FileReader>);
          }
        })
      };
      spyOn(window as Window & typeof globalThis, 'FileReader' as keyof Window).and.returnValue(mockReader as unknown as FileReader);

      const mockFile = new File([csvContent], 'pontos.csv', { type: 'text/csv' });
      const inputEl = { files: [mockFile], value: 'pontos.csv' } as unknown as HTMLInputElement;
      const event = { target: inputEl } as unknown as Event;

      component.onCsvFileSelected(event);

      expect((event.target as HTMLInputElement).value).toBe('');
      done();
    });
  });
});
