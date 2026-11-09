import {
  AfterViewInit,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  ViewChild
} from '@angular/core';
import { BarController, BarElement, CategoryScale, Chart, Legend, LinearScale, Tooltip } from 'chart.js';
import { AcoRun, ConfigSummary, DatasetInstance, INSTANCE_STATUS_LABELS } from '../../../models/dataset.models';

Chart.register(BarController, BarElement, CategoryScale, LinearScale, Tooltip, Legend);

type SortKey = keyof Pick<ConfigSummary,
  'meanGapPercent' | 'stdGapPercent' | 'optimalHitRate' | 'meanExecutionTimeMs' | 'meanBestIteration' | 'instancesWon'>;

const CHART_LIMIT = 15;
const RUNS_PREVIEW = 30;

@Component({
  selector: 'app-dataset-results',
  templateUrl: './dataset-results.component.html'
})
export class DatasetResultsComponent implements OnChanges, AfterViewInit, OnDestroy {
  @Input() configs: ConfigSummary[] = [];
  @Input() instances: DatasetInstance[] = [];
  @Input() selectedInstanceId: string | null = null;
  @Input() runs: AcoRun[] = [];
  @Output() selectInstance = new EventEmitter<DatasetInstance>();

  @ViewChild('gapChart') private gapChartRef?: ElementRef<HTMLCanvasElement>;
  private chart?: Chart;

  readonly instanceStatusLabels = INSTANCE_STATUS_LABELS;
  sortKey: SortKey = 'meanGapPercent';
  sortAscending = true;
  showAllRuns = false;

  readonly columns: { key: SortKey; label: string; title: string }[] = [
    { key: 'meanGapPercent', label: 'Gap médio', title: 'Média de (ACO − ótimo)/ótimo' },
    { key: 'stdGapPercent', label: 'Desvio', title: 'Desvio-padrão do gap (estabilidade)' },
    { key: 'optimalHitRate', label: '% ótimo', title: 'Fração de execuções que atingiram o ótimo' },
    { key: 'meanExecutionTimeMs', label: 'Tempo (ms)', title: 'Tempo médio por execução' },
    { key: 'meanBestIteration', label: 'Iter. melhor', title: 'Iteração média em que a melhor rota foi encontrada' },
    { key: 'instancesWon', label: 'Vitórias', title: 'Instâncias em que a configuração teve o menor gap médio' }
  ];

  get sortedConfigs(): ConfigSummary[] {
    const direction = this.sortAscending ? 1 : -1;
    return [...this.configs].sort((a, b) => (a[this.sortKey] - b[this.sortKey]) * direction);
  }

  get selectedInstance(): DatasetInstance | undefined {
    return this.instances.find(i => i.id === this.selectedInstanceId);
  }

  get visibleRuns(): AcoRun[] {
    const sorted = [...this.runs].sort((a, b) => a.gapPercent - b.gapPercent || a.executionTimeMs - b.executionTimeMs);
    return this.showAllRuns ? sorted : sorted.slice(0, RUNS_PREVIEW);
  }

  get hiddenRunCount(): number {
    return Math.max(0, this.runs.length - RUNS_PREVIEW);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['configs']) this.renderChart();
    if (changes['selectedInstanceId']) this.showAllRuns = false;
  }

  ngAfterViewInit(): void {
    this.renderChart();
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }

  sortBy(key: SortKey): void {
    if (this.sortKey === key) {
      this.sortAscending = !this.sortAscending;
    } else {
      this.sortKey = key;
      // Higher is better for hit rate and wins; lower is better otherwise.
      this.sortAscending = !(key === 'optimalHitRate' || key === 'instancesWon');
    }
  }

  gapClass(gap: number | null | undefined): string {
    if (gap === null || gap === undefined) return '';
    if (gap < 1e-9) return 'ds-gap-optimal';
    if (gap < 2) return 'ds-gap-good';
    if (gap < 10) return 'ds-gap-medium';
    return 'ds-gap-bad';
  }

  private renderChart(): void {
    const canvas = this.gapChartRef?.nativeElement;
    if (!canvas) return;
    this.chart?.destroy();
    this.chart = undefined;
    if (this.configs.length === 0) return;

    const top = [...this.configs].sort((a, b) => a.meanGapPercent - b.meanGapPercent).slice(0, CHART_LIMIT);
    this.chart = new Chart(canvas, {
      type: 'bar',
      data: {
        labels: top.map(c => c.configKey),
        datasets: [
          {
            label: 'Gap médio (%)',
            data: top.map(c => +c.meanGapPercent.toFixed(3)),
            backgroundColor: '#2563eb'
          },
          {
            label: 'Desvio (%)',
            data: top.map(c => +c.stdGapPercent.toFixed(3)),
            backgroundColor: '#93c5fd'
          }
        ]
      },
      options: {
        indexAxis: 'y',
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { position: 'bottom' } },
        scales: { x: { beginAtZero: true, title: { display: true, text: '% acima do ótimo' } } }
      }
    });
  }
}
