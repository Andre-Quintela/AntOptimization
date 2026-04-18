import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PointsListComponent } from './points-list.component';
import { LocationDto } from '../../models/route.models';

const loc1: LocationDto = { lat: -19.9, lng: -43.9 };
const loc2: LocationDto = { lat: -19.8, lng: -43.8 };
const loc3: LocationDto = { lat: -19.7, lng: -43.7 };

describe('PointsListComponent', () => {
  let component: PointsListComponent;
  let fixture: ComponentFixture<PointsListComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [PointsListComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(PointsListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render one row per point', () => {
    component.points = [loc1, loc2, loc3];
    fixture.detectChanges();
    const rows = fixture.nativeElement.querySelectorAll('.point-item');
    expect(rows.length).toBe(3);
  });

  it('should show empty state when points list is empty', () => {
    component.points = [];
    fixture.detectChanges();
    const empty = fixture.nativeElement.querySelector('.empty-state');
    expect(empty).not.toBeNull();
  });

  it('should apply .point-start class to the start index row', () => {
    component.points = [loc1, loc2];
    component.startIndex = 1;
    fixture.detectChanges();
    const rows = fixture.nativeElement.querySelectorAll('.point-item');
    expect(rows[1].classList).toContain('point-start');
    expect(rows[0].classList).not.toContain('point-start');
  });

  it('should emit setStart with the correct index', () => {
    component.points = [loc1, loc2];
    component.startIndex = 0; // make index 0 the start so index 1 shows the flag button
    fixture.detectChanges();

    const emitted: number[] = [];
    component.setStart.subscribe((i: number) => emitted.push(i));

    const btn = fixture.nativeElement.querySelector('button[title="Definir como partida"]') as HTMLButtonElement;
    btn.click();

    expect(emitted).toEqual([1]);
  });

  it('should emit remove with the correct index', () => {
    component.points = [loc1, loc2];
    fixture.detectChanges();

    const emitted: number[] = [];
    component.remove.subscribe((i: number) => emitted.push(i));

    const buttons = fixture.nativeElement.querySelectorAll('button[title="Remover"]');
    (buttons[0] as HTMLButtonElement).click();

    expect(emitted).toEqual([0]);
  });

  it('should emit focus with the LocationDto', () => {
    component.points = [loc1, loc2];
    fixture.detectChanges();

    const emitted: LocationDto[] = [];
    component.focus.subscribe((l: LocationDto) => emitted.push(l));

    const buttons = fixture.nativeElement.querySelectorAll('button[title="Localizar no mapa"]');
    (buttons[1] as HTMLButtonElement).click();

    expect(emitted).toEqual([loc2]);
  });

  describe('getOptimizedPosition()', () => {
    it('should return null when bestRouteOrder is null', () => {
      component.bestRouteOrder = null;
      expect(component.getOptimizedPosition(0)).toBeNull();
    });

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
});
