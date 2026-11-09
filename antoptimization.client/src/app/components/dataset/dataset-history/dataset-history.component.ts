import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ACTIVE_JOB_STATUSES, DatasetJob, JOB_STATUS_LABELS } from '../../../models/dataset.models';

@Component({
  selector: 'app-dataset-history',
  templateUrl: './dataset-history.component.html'
})
export class DatasetHistoryComponent {
  @Input() jobs: DatasetJob[] = [];
  @Input() selectedJobId: string | null = null;
  @Input() loading = false;
  @Output() select = new EventEmitter<DatasetJob>();
  @Output() refresh = new EventEmitter<void>();
  @Output() exportAll = new EventEmitter<void>();

  readonly statusLabels = JOB_STATUS_LABELS;

  isActive(job: DatasetJob): boolean {
    return ACTIVE_JOB_STATUSES.includes(job.status);
  }

  trackById(_: number, job: DatasetJob): string {
    return job.id;
  }

  /** "8 pts" for a single size, "6-12 pts" for a range. */
  pointCountLabel(job: DatasetJob): string {
    const counts = job.pointCounts ?? [];
    if (counts.length === 0) return '\u2014';
    const min = counts[0];
    const max = counts[counts.length - 1];
    return min === max ? `${min} pts` : `${min}\u2013${max} pts`;
  }
}
