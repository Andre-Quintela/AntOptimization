import { ComponentFixture, TestBed } from '@angular/core/testing';
import * as L from 'leaflet';
import { MapViewComponent } from './map-view.component';

const createMapStub = () => ({
  on: jasmine.createSpy('on'),
  remove: jasmine.createSpy('remove'),
  invalidateSize: jasmine.createSpy('invalidateSize')
});

describe('MapViewComponent', () => {
  let component: MapViewComponent;
  let fixture: ComponentFixture<MapViewComponent>;
  let mapStub: ReturnType<typeof createMapStub>;

  beforeEach(async () => {
    mapStub = createMapStub();

    spyOn(L, 'map').and.returnValue(mapStub as unknown as L.Map);
    spyOn(L, 'tileLayer').and.returnValue({ addTo: jasmine.createSpy() } as unknown as L.TileLayer);
    spyOn(L.control, 'zoom').and.returnValue({ addTo: jasmine.createSpy() } as unknown as L.Control.Zoom);

    await TestBed.configureTestingModule({
      declarations: [MapViewComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(MapViewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize Leaflet map in ngAfterViewInit', () => {
    expect(L.map).toHaveBeenCalled();
    expect(L.tileLayer).toHaveBeenCalled();
  });

  it('should emit mapClick with lat/lng on map click', () => {
    const emitted: { lat: number; lng: number }[] = [];
    component.mapClick.subscribe(v => emitted.push(v));

    const clickHandler = (mapStub.on as jasmine.Spy).calls.allArgs()
      .find((args: unknown[]) => args[0] === 'click')?.[1] as ((e: L.LeafletMouseEvent) => void) | undefined;

    expect(clickHandler).toBeDefined();
    clickHandler!({ latlng: { lat: -19.9, lng: -43.9 } } as L.LeafletMouseEvent);

    expect(emitted.length).toBe(1);
    expect(emitted[0]).toEqual({ lat: -19.9, lng: -43.9 });
  });

  it('should return the map instance from getMap()', () => {
    expect(component.getMap()).toBe(mapStub as unknown as L.Map);
  });

  it('should call map.invalidateSize() from invalidateSize()', () => {
    component.invalidateSize();
    expect(mapStub.invalidateSize).toHaveBeenCalled();
  });

  it('should call map.remove() on ngOnDestroy', () => {
    component.ngOnDestroy();
    expect(mapStub.remove).toHaveBeenCalled();
  });
});
