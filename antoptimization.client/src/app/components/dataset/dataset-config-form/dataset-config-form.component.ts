import { Component, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output } from '@angular/core';
import { Subject, of } from 'rxjs';
import { catchError, debounceTime, switchMap, takeUntil } from 'rxjs/operators';
import { DatasetService } from '../../../services/dataset.service';
import {
  CreateDatasetJobRequest,
  DatasetEstimate,
  DistanceSource,
  HyperparameterGrid,
  RegionDto
} from '../../../models/dataset.models';

type GridField = keyof HyperparameterGrid;

interface GridFieldDefinition {
  key: GridField;
  label: string;
  hint: string;
  integer: boolean;
}

@Component({
  selector: 'app-dataset-config-form',
  templateUrl: './dataset-config-form.component.html'
})
export class DatasetConfigFormComponent implements OnInit, OnChanges, OnDestroy {
  @Input() region: RegionDto | null = null;
  @Input() submitting = false;
  @Output() submitJob = new EventEmitter<CreateDatasetJobRequest>();

  readonly gridFields: GridFieldDefinition[] = [
    { key: 'alpha', label: 'Alpha (α)', hint: 'Peso do feromônio', integer: false },
    { key: 'beta', label: 'Beta (β)', hint: 'Peso da heurística (1/distância)', integer: false },
    { key: 'numberOfAnts', label: 'Formigas', hint: 'Quantidade de formigas', integer: true },
    { key: 'evaporationRate', label: 'Evaporação (ρ)', hint: 'Valores entre 0 e 1', integer: false },
    { key: 'iterations', label: 'Iterações', hint: 'Iterações por execução', integer: true }
  ];

  name = '';
  pointCountsText = '6, 8, 10, 12';
  pointCountsError: string | null = null;
  instancesPerPointCount = 5;
  distanceSource: DistanceSource = 'Osrm';
  fixedStart = true;
  repetitions = 3;
  masterSeed: number | null = null;
  gridText: Record<GridField, string> = {
    alpha: '0.5, 1, 2',
    beta: '1, 2, 5',
    numberOfAnts: '10, 20, 40',
    evaporationRate: '0.1, 0.3, 0.5, 0.7',
    iterations: '50, 100, 200'
  };
  gridErrors: Partial<Record<GridField, string>> = {};

  estimate: DatasetEstimate | null = null;
  estimating = false;
  estimateError: string | null = null;

  private readonly changes$ = new Subject<void>();
  private readonly destroy$ = new Subject<void>();

  constructor(private datasetService: DatasetService) {}

  ngOnInit(): void {
    this.changes$.pipe(
      debounceTime(400),
      switchMap(() => {
        const request = this.buildRequest();
        if (!request) {
          this.estimating = false;
          return of(null);
        }
        this.estimating = true;
        return this.datasetService.estimate(request).pipe(
          catchError(() => {
            this.estimateError = 'Não foi possível calcular a estimativa.';
            return of(null);
          })
        );
      }),
      takeUntil(this.destroy$)
    ).subscribe(estimate => {
      this.estimating = false;
      this.estimate = estimate;
      if (estimate) this.estimateError = null;
    });

    this.onChange();
  }

  ngOnChanges(): void {
    this.onChange();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onChange(): void {
    this.changes$.next();
  }

  get canSubmit(): boolean {
    return !!this.region && !this.submitting && !this.estimating &&
      !this.hasGridErrors && !this.pointCountsError && !!this.estimate?.isValid;
  }

  /** "2 tamanhos x 5 instâncias = 10 conjuntos de pontos" */
  get instanceBreakdown(): string | null {
    const estimate = this.estimate;
    if (!estimate?.isValid || estimate.pointCounts.length === 0) return null;
    const sizes = estimate.pointCounts.length;
    const sizeLabel = sizes === 1 ? '1 tamanho' : `${sizes} tamanhos`;
    const perSize = this.instancesPerPointCount;
    return `${sizeLabel} \u00d7 ${perSize} inst\u00e2ncia(s) = ${estimate.totalInstances} conjuntos de pontos ` +
      `\u00d7 ${estimate.configurationCount} configs \u00d7 ${this.repetitions} repeti\u00e7\u00f5es`;
  }

  get hasGridErrors(): boolean {
    return Object.values(this.gridErrors).some(e => !!e);
  }

  submit(): void {
    const request = this.buildRequest();
    if (request && this.canSubmit) {
      this.submitJob.emit(request);
    }
  }

  formatDuration(seconds: number): string {
    if (seconds < 1) return '< 1 s';
    if (seconds < 60) return `${Math.round(seconds)} s`;
    if (seconds < 3600) return `${Math.round(seconds / 60)} min`;
    return `${(seconds / 3600).toFixed(1)} h`;
  }

  formatNumber(value: number): string {
    if (!isFinite(value)) return '∞';
    return value >= 1e6 ? value.toExponential(2) : Math.round(value).toLocaleString('pt-BR');
  }

  /** Parses "1, 2.5; 3" into numbers, recording a per-field validation message. */
  parseList(field: GridFieldDefinition): number[] | null {
    const values = parseNumberList(this.gridText[field.key], field.integer);
    this.gridErrors[field.key] = values
      ? undefined
      : field.integer ? 'Use apenas inteiros separados por vírgula.' : 'Use números separados por vírgula (ponto decimal).';
    return values;
  }

  /** Deduplicated, ascending instance sizes - mirrors the backend normalization. */
  private parsePointCounts(): number[] | null {
    const values = parseNumberList(this.pointCountsText, true);
    if (!values) {
      this.pointCountsError = 'Informe quantidades inteiras de pontos separadas por vírgula.';
      return null;
    }
    if (values.some(v => v < 2)) {
      this.pointCountsError = 'Cada tamanho precisa de ao menos 2 pontos.';
      return null;
    }
    this.pointCountsError = null;
    return [...new Set(values)].sort((a, b) => a - b);
  }

  private buildRequest(): CreateDatasetJobRequest | null {
    const grid: Partial<HyperparameterGrid> = {};
    let valid = true;
    for (const field of this.gridFields) {
      const values = this.parseList(field);
      if (values) grid[field.key] = values; else valid = false;
    }
    const pointCounts = this.parsePointCounts();
    if (!valid || !pointCounts || !this.region) {
      this.estimate = null;
      return null;
    }

    return {
      name: this.name.trim() || undefined,
      region: this.region,
      pointCounts,
      instancesPerPointCount: Number(this.instancesPerPointCount),
      distanceSource: this.distanceSource,
      fixedStart: this.fixedStart,
      repetitions: Number(this.repetitions),
      masterSeed: this.masterSeed === null || (this.masterSeed as unknown) === '' ? null : Number(this.masterSeed),
      grid: grid as HyperparameterGrid
    };
  }
}

/** Parses "1, 2.5; 3" into numbers; null when empty or any token is invalid. */
function parseNumberList(text: string, integer: boolean): number[] | null {
  const tokens = text.split(/[,;\s]+/).filter(t => t.length > 0);
  if (tokens.length === 0) return null;
  const values = tokens.map(t => Number(t));
  if (values.some(v => isNaN(v) || (integer && !Number.isInteger(v)))) return null;
  return values;
}
