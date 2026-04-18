import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA, ChangeDetectorRef } from '@angular/core';
import { RouterTestingModule } from '@angular/router/testing';
import { of, throwError } from 'rxjs';

import { DashboardPageComponent } from './dashboard-page.component';
import { RouteService } from '../../services/route.service';
import { LocationStateService } from '../../services/location-state.service';
import { MapViewComponent } from '../../components/map-view/map-view.component';
import { CompareOptimizationResponse, LocationDto } from '../../models/route.models';
import * as L from 'leaflet';

// ---------------------------------------------------------------------------
// Stubs
// ---------------------------------------------------------------------------
const createMapStub = () => ({
  on: jasmine.createSpy('on'),
  remove: jasmine.createSpy('remove'),
  setView: jasmine.createSpy('setView'),
  fitBounds: jasmine.createSpy('fitBounds'),
  getCenter: jasmine.createSpy('getCenter').and.returnValue({ lat: -19.9, lng: -43.9 })
});

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

const createPolylineStub = () => {
  const stub: Record<string, jasmine.Spy> = {
    addTo: jasmine.createSpy('addTo'),
    remove: jasmine.createSpy('remove'),
    getLatLngs: jasmine.createSpy('getLatLngs').and.returnValue([])
  };
  stub['addTo'].and.returnValue(stub);
  return stub;
};

// ---------------------------------------------------------------------------
// Test data
// ---------------------------------------------------------------------------
const mockCompareResponse: CompareOptimizationResponse = {
  results: [
    { algorithm: 'ACO', bestRouteOrder: [0, 1], totalDistance: 12.4, executionTimeMs: 320, routeCoordinates: [], relativeGapPercent: 0.0 },
    { algorithm: 'Nearest Neighbor', bestRouteOrder: [0, 1], totalDistance: 13.1, executionTimeMs: 18, routeCoordinates: [], relativeGapPercent: 5.65 },
    { algorithm: '2-Opt', bestRouteOrder: [0, 1], totalDistance: 12.6, executionTimeMs: 45, routeCoordinates: [], relativeGapPercent: 1.61 },
    { algorithm: 'Genetic Algorithm', bestRouteOrder: [0, 1], totalDistance: 12.5, executionTimeMs: 280, routeCoordinates: [], relativeGapPercent: 0.81 }
  ]
};

const twoLocations: LocationDto[] = [
  { lat: -19.9, lng: -43.9 },
  { lat: -19.8, lng: -43.8 }
];

// ---------------------------------------------------------------------------
// Suite
// ---------------------------------------------------------------------------
describe('DashboardPageComponent', () => {
  let component: DashboardPageComponent;
  let fixture: ComponentFixture<DashboardPageComponent>;
  let routeServiceSpy: jasmine.SpyObj<RouteService>;
  let locationStateSpy: jasmine.SpyObj<LocationStateService>;
  let mapViewStub: jasmine.SpyObj<MapViewComponent>;
  let mapStub: ReturnType<typeof createMapStub>;

  beforeEach(async () => {
    routeServiceSpy = jasmine.createSpyObj('RouteService', ['compareRoutes']);
    locationStateSpy = jasmine.createSpyObj('LocationStateService', [], {
      locations: [],
      startIndex: null
    });

    mapStub = createMapStub();
    mapViewStub = jasmine.createSpyObj('MapViewComponent', ['getMap', 'invalidateSize']);
    mapViewStub.getMap.and.returnValue(mapStub as unknown as L.Map);

    spyOn(L, 'marker').and.callFake(() => createMarkerStub() as unknown as L.Marker);
    (spyOn(L, 'polyline') as jasmine.Spy).and.callFake(() => createPolylineStub() as unknown as L.Polyline);
    spyOn(L, 'divIcon').and.returnValue({} as L.DivIcon);
    spyOn(L, 'latLngBounds').and.returnValue({} as L.LatLngBounds);

    await TestBed.configureTestingModule({
      imports: [RouterTestingModule],
      declarations: [DashboardPageComponent],
      providers: [
        { provide: RouteService, useValue: routeServiceSpy },
        { provide: LocationStateService, useValue: locationStateSpy }
      ],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardPageComponent);
    component = fixture.componentInstance;

    // Stub out CDR so internal cdr.detectChanges() doesn't re-evaluate @ViewChild bindings
    const mockCdr = { detectChanges: jasmine.createSpy('detectChanges') };
    (component as unknown as { cdr: object }).cdr = mockCdr;

    fixture.detectChanges();

    // Override the @ViewChild after Angular's resolution (NO_ERRORS_SCHEMA yields undefined)
    (component as unknown as { mapView: MapViewComponent }).mapView = mapViewStub;
  });

  afterEach(() => {
    component.ngOnDestroy();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with empty results', () => {
    expect(component.results).toEqual([]);
    expect(component.isComparing).toBeFalse();
  });

  // -------------------------------------------------------------------------
  // Point management
  // -------------------------------------------------------------------------
  describe('removePoint()', () => {
    it('should remove the point at the given index', () => {
      component.locations = [...twoLocations];
      component.removePoint(0);
      expect(component.locations.length).toBe(1);
    });

    it('should clear startIndex if the start point is removed', () => {
      component.locations = [...twoLocations];
      component.startIndex = 0;
      component.removePoint(0);
      expect(component.startIndex).toBeNull();
    });

    it('should decrement startIndex when a point before start is removed', () => {
      component.locations = [...twoLocations];
      component.startIndex = 1;
      component.removePoint(0);
      expect(component.startIndex).toBe(0);
    });
  });

  describe('setAsStart()', () => {
    it('should toggle startIndex off when the same index is passed', () => {
      component.startIndex = 1;
      component.setAsStart(1);
      expect(component.startIndex).toBeNull();
    });

    it('should set startIndex to the given index', () => {
      (component as unknown as { startIndex: number | null }).startIndex = null;
      component.setAsStart(0);
      expect(component.startIndex as number | null).toEqual(0);
    });
  });

  // -------------------------------------------------------------------------
  // Algorithm comparison
  // -------------------------------------------------------------------------
  describe('compare()', () => {
    it('should disable compare button when fewer than 2 locations', () => {
      component.locations = [{ lat: -19.9, lng: -43.9 }];
      fixture.detectChanges();
      const btn = fixture.nativeElement.querySelector('.btn-primary.btn-block') as HTMLButtonElement;
      expect(btn.disabled).toBeTrue();
    });

    it('should enable compare button with 2 or more locations', () => {
      component.locations = [...twoLocations];
      fixture.detectChanges();
      const btn = fixture.nativeElement.querySelector('.btn-primary.btn-block') as HTMLButtonElement;
      expect(btn.disabled).toBeFalse();
    });

    it('should call compareRoutes with current locations', () => {
      routeServiceSpy.compareRoutes.and.returnValue(of(mockCompareResponse));
      component.locations = [...twoLocations];
      component.compare();
      expect(routeServiceSpy.compareRoutes).toHaveBeenCalledWith(
        jasmine.objectContaining({ locations: twoLocations })
      );
    });

    it('should populate results after successful comparison', () => {
      routeServiceSpy.compareRoutes.and.returnValue(of(mockCompareResponse));
      component.locations = [...twoLocations];
      component.compare();
      expect(component.results.length).toBe(4);
    });

    it('should set errorMessage and stop isComparing on error', () => {
      routeServiceSpy.compareRoutes.and.returnValue(throwError(() => new Error('fail')));
      component.locations = [...twoLocations];
      component.compare();
      expect(component.errorMessage).toBeTruthy();
      expect(component.isComparing).toBeFalse();
    });
  });

  describe('bestAlgorithm', () => {
    it('should return the algorithm with the lowest totalDistance', () => {
      routeServiceSpy.compareRoutes.and.returnValue(of(mockCompareResponse));
      component.locations = [...twoLocations];
      component.compare();
      expect(component.bestAlgorithm).toBe('ACO');
    });

    it('should return empty string when results are empty', () => {
      expect(component.bestAlgorithm).toBe('');
    });
  });

  // -------------------------------------------------------------------------
  // Quality bar helpers
  // -------------------------------------------------------------------------
  describe('getQualityBarWidth()', () => {
    it('should return 100 for relativeGapPercent = 0', () => {
      expect(component.getQualityBarWidth(0)).toBe(100);
    });

    it('should return less than 100 for positive gap', () => {
      expect(component.getQualityBarWidth(10)).toBeLessThan(100);
    });
  });

  describe('getQualityBarColor()', () => {
    it('should return success color for gap = 0', () => {
      expect(component.getQualityBarColor(0)).toContain('success');
    });

    it('should return warning color for gap <= 10', () => {
      expect(component.getQualityBarColor(5)).toContain('warning');
    });

    it('should return danger color for gap > 10', () => {
      expect(component.getQualityBarColor(15)).toContain('danger');
    });
  });

  describe('getAlgorithmColor()', () => {
    it('should return blue for ACO', () => {
      expect(component.getAlgorithmColor('ACO')).toBe('#2563eb');
    });

    it('should return fallback color for unknown algorithm', () => {
      expect(component.getAlgorithmColor('Unknown')).toBe('#64748b');
    });
  });

  // -------------------------------------------------------------------------
  // Quality label rendering
  // -------------------------------------------------------------------------
  it('should display "Ótimo" quality label for the best algorithm after comparison', () => {
    routeServiceSpy.compareRoutes.and.returnValue(of(mockCompareResponse));
    component.locations = [...twoLocations];
    component.compare();
    fixture.detectChanges();

    const labels = Array.from(
      fixture.nativeElement.querySelectorAll('.quality-label')
    ).map((el: unknown) => (el as HTMLElement).textContent?.trim());
    expect(labels).toContain('Ótimo');
  });

  // -------------------------------------------------------------------------
  // Map expand
  // -------------------------------------------------------------------------
  describe('toggleMapExpand()', () => {
    it('should toggle isMapExpanded', () => {
      expect(component.isMapExpanded).toBeFalse();
      component.toggleMapExpand();
      expect(component.isMapExpanded).toBeTrue();
      component.toggleMapExpand();
      expect(component.isMapExpanded).toBeFalse();
    });

    it('should call mapView.invalidateSize()', () => {
      jasmine.clock().install();
      component.toggleMapExpand();
      jasmine.clock().tick(50);
      expect(mapViewStub.invalidateSize).toHaveBeenCalled();
      jasmine.clock().uninstall();
    });
  });

  // -------------------------------------------------------------------------
  // Algorithm filter
  // -------------------------------------------------------------------------
  describe('selectAlgorithmFilter()', () => {
    beforeEach(() => {
      routeServiceSpy.compareRoutes.and.returnValue(of(mockCompareResponse));
      component.locations = [...twoLocations];
      component.compare();
    });

    it('should set selectedAlgorithm to the given algorithm', () => {
      component.selectAlgorithmFilter('ACO');
      expect(component.selectedAlgorithm).toBe('ACO');
    });

    it('should reset selectedAlgorithm to null when called with null', () => {
      component.selectAlgorithmFilter('ACO');
      component.selectAlgorithmFilter(null);
      expect(component.selectedAlgorithm).toBeNull();
    });

    it('should reset selectedAlgorithm to null at the start of compare()', () => {
      component.selectAlgorithmFilter('ACO');
      routeServiceSpy.compareRoutes.and.returnValue(of(mockCompareResponse));
      component.compare();
      expect(component.selectedAlgorithm).toBeNull();
    });
  });
});
