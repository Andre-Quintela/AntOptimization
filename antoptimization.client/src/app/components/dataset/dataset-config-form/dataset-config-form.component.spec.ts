import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { of } from 'rxjs';
import { DatasetConfigFormComponent } from './dataset-config-form.component';
import { DatasetService } from '../../../services/dataset.service';
import { DatasetEstimate } from '../../../models/dataset.models';

const validEstimate: DatasetEstimate = {
  isValid: true, errors: [], warnings: [], configurationCount: 108, pointCounts: [6, 8, 10, 12], totalInstances: 20, totalRuns: 6480,
  exactMethod: 'BruteForce', exactOperationsPerInstance: 5040, exactMemoryBytes: 96,
  estimatedAcoSeconds: 10, estimatedExactSeconds: 0, estimatedNetworkSeconds: 15, estimatedTotalSeconds: 25
};

describe('DatasetConfigFormComponent', () => {
  let fixture: ComponentFixture<DatasetConfigFormComponent>;
  let component: DatasetConfigFormComponent;
  let datasetService: jasmine.SpyObj<DatasetService>;

  beforeEach(async () => {
    datasetService = jasmine.createSpyObj('DatasetService', ['estimate']);
    datasetService.estimate.and.returnValue(of(validEstimate));

    await TestBed.configureTestingModule({
      imports: [FormsModule],
      declarations: [DatasetConfigFormComponent],
      providers: [{ provide: DatasetService, useValue: datasetService }]
    }).compileComponents();

    fixture = TestBed.createComponent(DatasetConfigFormComponent);
    component = fixture.componentInstance;
  });

  it('does not estimate nor allow submission without a region', fakeAsync(() => {
    fixture.detectChanges();
    tick(500);

    expect(datasetService.estimate).not.toHaveBeenCalled();
    expect(component.canSubmit).toBeFalse();
  }));

  it('requests a debounced estimate with the parsed grid', fakeAsync(() => {
    component.region = { south: -20, west: -44, north: -19.9, east: -43.9 };
    fixture.detectChanges();
    tick(500);

    expect(datasetService.estimate).toHaveBeenCalledTimes(1);
    const request = datasetService.estimate.calls.mostRecent().args[0];
    expect(request.pointCounts).toEqual([6, 8, 10, 12]);
    expect(request.instancesPerPointCount).toBe(5);
    expect(request.grid.alpha).toEqual([0.5, 1, 2]);
    expect(request.grid.evaporationRate).toEqual([0.1, 0.3, 0.5, 0.7]);
    expect(request.masterSeed).toBeNull();
    expect(component.estimate).toEqual(validEstimate);
    expect(component.canSubmit).toBeTrue();
  }));

  it('flags invalid hyperparameter lists and blocks submission', fakeAsync(() => {
    component.region = { south: -20, west: -44, north: -19.9, east: -43.9 };
    component.gridText.numberOfAnts = '10, abc';
    component.gridText.iterations = '50.5';
    fixture.detectChanges();
    tick(500);

    expect(component.gridErrors.numberOfAnts).toBeTruthy();
    expect(component.gridErrors.iterations).toBeTruthy();
    expect(component.canSubmit).toBeFalse();
    expect(datasetService.estimate).not.toHaveBeenCalled();
  }));

  it('emits the request on submit', fakeAsync(() => {
    component.region = { south: -20, west: -44, north: -19.9, east: -43.9 };
    component.distanceSource = 'Haversine';
    component.masterSeed = 42;
    fixture.detectChanges();
    tick(500);
    const emitted = jasmine.createSpy('submitJob');
    component.submitJob.subscribe(emitted);

    component.submit();

    expect(emitted).toHaveBeenCalledWith(jasmine.objectContaining({
      distanceSource: 'Haversine', masterSeed: 42, pointCounts: [6, 8, 10, 12], instancesPerPointCount: 5
    }));
  }));

  it('normalizes the instance sizes it submits', fakeAsync(() => {
    component.region = { south: -20, west: -44, north: -19.9, east: -43.9 };
    component.pointCountsText = '10, 6, 6, 8';
    fixture.detectChanges();
    tick(500);

    expect(component.pointCountsError).toBeNull();
    expect(datasetService.estimate.calls.mostRecent().args[0].pointCounts).toEqual([6, 8, 10]);
  }));

  it('flags invalid instance sizes and blocks submission', fakeAsync(() => {
    component.region = { south: -20, west: -44, north: -19.9, east: -43.9 };
    component.pointCountsText = '6, a';
    fixture.detectChanges();
    tick(500);

    expect(component.pointCountsError).toBeTruthy();
    expect(component.canSubmit).toBeFalse();
    expect(datasetService.estimate).not.toHaveBeenCalled();

    component.pointCountsText = '1, 6';
    component.onChange();
    tick(500);

    expect(component.pointCountsError).toContain('2 pontos');
    expect(component.canSubmit).toBeFalse();
  }));

  it('describes the instance breakdown', fakeAsync(() => {
    component.region = { south: -20, west: -44, north: -19.9, east: -43.9 };
    fixture.detectChanges();
    tick(500);

    expect(component.instanceBreakdown)
      .toBe('4 tamanhos \u00d7 5 inst\u00e2ncia(s) = 20 conjuntos de pontos \u00d7 108 configs \u00d7 3 repeti\u00e7\u00f5es');
  }));

  it('formats durations for display', () => {
    expect(component.formatDuration(0.2)).toBe('< 1 s');
    expect(component.formatDuration(42)).toBe('42 s');
    expect(component.formatDuration(600)).toBe('10 min');
    expect(component.formatDuration(7200)).toBe('2.0 h');
  });
});
