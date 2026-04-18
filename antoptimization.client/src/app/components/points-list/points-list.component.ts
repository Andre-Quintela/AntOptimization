import { Component, Input, Output, EventEmitter } from '@angular/core';
import { LocationDto } from '../../models/route.models';

@Component({
  selector: 'app-points-list',
  templateUrl: './points-list.component.html',
  styleUrls: ['./points-list.component.css']
})
export class PointsListComponent {
  @Input() points: LocationDto[] = [];
  @Input() startIndex: number | null = null;
  @Input() bestRouteOrder: number[] | null = null;
  @Input() isOptimizing = false;
  @Output() setStart = new EventEmitter<number>();
  @Output() remove = new EventEmitter<number>();
  @Output() focus = new EventEmitter<LocationDto>();

  getOptimizedPosition(originalIndex: number): number | null {
    if (!this.bestRouteOrder || this.bestRouteOrder.length === 0) return null;
    const pos = this.bestRouteOrder.indexOf(originalIndex);
    return pos >= 0 ? pos + 1 : null;
  }
}
