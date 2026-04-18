import { Component, Input, Output, EventEmitter, OnInit, OnDestroy } from '@angular/core';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, takeUntil } from 'rxjs/operators';
import { GeocodingService } from '../../services/geocoding.service';
import { GeocodingResult } from '../../models/geocoding.models';

@Component({
  selector: 'app-search-box',
  templateUrl: './search-box.component.html',
  styleUrls: ['./search-box.component.css']
})
export class SearchBoxComponent implements OnInit, OnDestroy {
  @Input() placeholder = 'Pesquisar local...';
  @Input() center?: { lat: number; lng: number };
  @Input() disabled = false;
  @Output() locationSelected = new EventEmitter<GeocodingResult>();

  searchQuery = '';
  searchResults: GeocodingResult[] = [];
  isSearching = false;
  showResults = false;

  private searchSubject$ = new Subject<string>();
  private destroy$ = new Subject<void>();

  constructor(private geocodingService: GeocodingService) {}

  ngOnInit(): void {
    this.searchSubject$.pipe(
      debounceTime(400),
      distinctUntilChanged(),
      switchMap(query => {
        this.isSearching = query.trim().length >= 3;
        return this.geocodingService.search(query, this.center);
      }),
      takeUntil(this.destroy$)
    ).subscribe(results => {
      this.searchResults = results;
      this.showResults = results.length > 0;
      this.isSearching = false;
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onInput(): void {
    this.searchSubject$.next(this.searchQuery);
  }

  selectResult(result: GeocodingResult): void {
    this.locationSelected.emit(result);
    this.searchQuery = result.displayName;
    this.showResults = false;
    this.searchResults = [];
  }

  clear(): void {
    this.searchQuery = '';
    this.searchResults = [];
    this.showResults = false;
  }

  hideResults(): void {
    setTimeout(() => { this.showResults = false; }, 200);
  }
}
