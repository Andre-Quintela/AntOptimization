import { Injectable } from '@angular/core';
import { LocationDto } from '../models/route.models';

@Injectable({ providedIn: 'root' })
export class LocationStateService {
  private _locations: LocationDto[] = [];
  private _startIndex: number | null = null;

  get locations(): LocationDto[] { return this._locations; }
  get startIndex(): number | null { return this._startIndex; }

  sync(locations: LocationDto[], startIndex: number | null): void {
    this._locations = [...locations];
    this._startIndex = startIndex;
  }
}
