import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { of } from 'rxjs';
import { SearchBoxComponent } from './search-box.component';
import { GeocodingService } from '../../services/geocoding.service';
import { GeocodingResult } from '../../models/geocoding.models';

const mockResult: GeocodingResult = {
  name: 'Praça da Liberdade',
  type: 'Parque',
  address: 'Belo Horizonte, MG',
  displayName: 'Praça da Liberdade, Belo Horizonte, MG',
  lat: -19.9321,
  lng: -43.9386
};

describe('SearchBoxComponent', () => {
  let component: SearchBoxComponent;
  let fixture: ComponentFixture<SearchBoxComponent>;
  let geocodingServiceSpy: jasmine.SpyObj<GeocodingService>;

  beforeEach(async () => {
    geocodingServiceSpy = jasmine.createSpyObj('GeocodingService', ['search']);
    geocodingServiceSpy.search.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      declarations: [SearchBoxComponent],
      imports: [FormsModule],
      providers: [{ provide: GeocodingService, useValue: geocodingServiceSpy }]
    }).compileComponents();

    fixture = TestBed.createComponent(SearchBoxComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should call GeocodingService after debounce for queries >= 3 chars', fakeAsync(() => {
    geocodingServiceSpy.search.and.returnValue(of([mockResult]));

    component.searchQuery = 'Praça';
    component.onInput();
    tick(400);

    expect(geocodingServiceSpy.search).toHaveBeenCalledWith('Praça', undefined);
    expect(component.searchResults).toEqual([mockResult]);
    expect(component.showResults).toBeTrue();
  }));

  it('should NOT trigger search before debounce delay elapses', fakeAsync(() => {
    component.searchQuery = 'test';
    component.onInput();
    tick(100);
    expect(geocodingServiceSpy.search).not.toHaveBeenCalled();
    tick(300); // complete debounce
  }));

  it('should pass center input to geocoding service', fakeAsync(() => {
    geocodingServiceSpy.search.and.returnValue(of([]));
    component.center = { lat: -19.9, lng: -43.9 };
    component.searchQuery = 'Lib';
    component.onInput();
    tick(400);
    expect(geocodingServiceSpy.search).toHaveBeenCalledWith('Lib', { lat: -19.9, lng: -43.9 });
  }));

  it('should emit locationSelected and update query on selectResult()', () => {
    const emitted: GeocodingResult[] = [];
    component.locationSelected.subscribe(r => emitted.push(r));

    component.selectResult(mockResult);

    expect(emitted).toEqual([mockResult]);
    expect(component.searchQuery).toBe(mockResult.displayName);
    expect(component.showResults).toBeFalse();
  });

  it('should clear state on clear()', () => {
    component.searchQuery = 'test';
    component.searchResults = [mockResult];
    component.showResults = true;

    component.clear();

    expect(component.searchQuery).toBe('');
    expect(component.searchResults).toEqual([]);
    expect(component.showResults).toBeFalse();
  });

  it('should hide results after hideResults() delay', fakeAsync(() => {
    component.showResults = true;
    component.hideResults();
    expect(component.showResults).toBeTrue();
    tick(200);
    expect(component.showResults).toBeFalse();
  }));
});
