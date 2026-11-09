import { Component, EventEmitter, Input, Output } from '@angular/core';
import {
  ACTIVE_JOB_STATUSES,
  DatasetExportLevel,
  DatasetInstance,
  DatasetJob,
  INSTANCE_STATUS_LABELS,
  JOB_STATUS_LABELS
} from '../../../models/dataset.models';

@Component({
  selector: 'app-dataset-job-progress',
  templateUrl: './dataset-job-progress.component.html'
})
export class DatasetJobProgressComponent {
  @Input({ required: true }) job!: DatasetJob;
  @Input() instances: DatasetInstance[] = [];
  @Input() busy = false;
  @Output() cancel = new EventEmitter<void>();
  @Output() resume = new EventEmitter<void>();
  @Output() remove = new EventEmitter<void>();
  @Output() download = new EventEmitter<DatasetExportLevel>();

  readonly statusLabels = JOB_STATUS_LABELS;
  readonly instanceStatusLabels = INSTANCE_STATUS_LABELS;

  get isActive(): boolean {
    return ACTIVE_JOB_STATUSES.includes(this.job.status);
  }

  get canResume(): boolean {
    return ['Interrupted', 'Cancelled', 'Failed', 'CompletedWithErrors'].includes(this.job.status);
  }

  get problemInstances(): DatasetInstance[] {
    return this.instances.filter(i => i.status === 'Failed' || i.status === 'ExceededLimit');
  }

  get completedInstances(): number {
    return this.instances.filter(i => i.status === 'Completed').length;
  }

  get hasData(): boolean {
    return this.completedInstances > 0;
  }
}
