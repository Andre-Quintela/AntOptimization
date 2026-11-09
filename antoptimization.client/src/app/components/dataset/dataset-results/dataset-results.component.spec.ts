import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { DatasetResultsComponent } from './dataset-results.component';
import { ConfigSummary } from '../../../models/dataset.models';

const config = (key: string, meanGap: number, hitRate: number): ConfigSummary => ({
  configKey: key, alpha: 1, beta: 2, numberOfAnts: 10, evaporationRate: 0.5, iterations: 50,
  runCount: 3, instanceCount: 1, meanGapPercent: meanGap, stdGapPercent: 0, minGapPercent: meanGap,
  maxGapPercent: meanGap, optimalHitRate: hitRate, meanExecutionTimeMs: 1, meanBestIteration: 1, instancesWon: 0
});

describe('DatasetResultsComponent', () => {
  let fixture: ComponentFixture<DatasetResultsComponent>;
  let component: DatasetResultsComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [DatasetResultsComponent],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();

    fixture = TestBed.createComponent(DatasetResultsComponent);
    component = fixture.componentInstance;
    component.configs = [config('b', 5, 0.2), config('a', 1, 0.9), config('c', 10, 0)];
  });

  it('sorts by mean gap ascending by default', () => {
    expect(component.sortedConfigs.map(c => c.configKey)).toEqual(['a', 'b', 'c']);
  });

  it('sorts "higher is better" columns descending and toggles direction', () => {
    component.sortBy('optimalHitRate');
    expect(component.sortedConfigs.map(c => c.configKey)).toEqual(['a', 'b', 'c']);

    component.sortBy('optimalHitRate');
    expect(component.sortedConfigs.map(c => c.configKey)).toEqual(['c', 'b', 'a']);
  });

  it('sorts "lower is better" columns ascending when switching columns', () => {
    component.sortBy('optimalHitRate');
    component.sortBy('meanExecutionTimeMs');
    expect(component.sortAscending).toBeTrue();
  });

  it('previews the best runs and can show all of them', () => {
    component.runs = Array.from({ length: 40 }, (_, i) => ({
      id: `${i}`, configKey: 'a', repetition: i, seed: i, status: 'Completed' as const, errorMessage: null,
      alpha: 1, beta: 2, numberOfAnts: 10, evaporationRate: 0.5, iterations: 50, bestDistanceM: 100 + i,
      gapAbsM: i, gapPercent: 40 - i, reachedOptimum: false, bestIteration: 1, firstOptimalIteration: null,
      toursConstructed: 500, executionTimeMs: 1, bestTour: []
    }));

    expect(component.visibleRuns.length).toBe(30);
    expect(component.visibleRuns[0].gapPercent).toBe(1);
    expect(component.hiddenRunCount).toBe(10);

    component.showAllRuns = true;
    expect(component.visibleRuns.length).toBe(40);

    component.ngOnChanges({ selectedInstanceId: {} as never });
    expect(component.showAllRuns).toBeFalse();
  });

  it('finds the selected instance', () => {
    component.instances = [{ id: 'x' } as never];
    component.selectedInstanceId = 'x';
    expect(component.selectedInstance?.id).toBe('x');
  });

  it('renders and destroys the chart with the view', () => {
    fixture.detectChanges();
    component.ngOnChanges({ configs: {} as never });
    component.configs = [];
    component.ngOnChanges({ configs: {} as never });
    expect(() => component.ngOnDestroy()).not.toThrow();
  });

  it('classifies gaps for color coding', () => {
    expect(component.gapClass(0)).toBe('ds-gap-optimal');
    expect(component.gapClass(1)).toBe('ds-gap-good');
    expect(component.gapClass(5)).toBe('ds-gap-medium');
    expect(component.gapClass(25)).toBe('ds-gap-bad');
    expect(component.gapClass(null)).toBe('');
  });
});
