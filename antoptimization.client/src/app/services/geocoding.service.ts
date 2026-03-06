import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { GeocodingResult } from '../models/geocoding.models';

interface NominatimAddress {
  road?: string;
  neighbourhood?: string;
  suburb?: string;
  city?: string;
  town?: string;
  village?: string;
  state?: string;
  country?: string;
  [key: string]: string | undefined;
}

interface NominatimResult {
  display_name: string;
  name: string;
  type: string;
  lat: string;
  lon: string;
  address: NominatimAddress;
}

const TYPE_LABELS: Record<string, string> = {
  residential: 'Rua',
  house: 'Endereço',
  apartments: 'Edifício',
  commercial: 'Comercial',
  retail: 'Comércio',
  restaurant: 'Restaurante',
  cafe: 'Café',
  hospital: 'Hospital',
  school: 'Escola',
  university: 'Universidade',
  bus_stop: 'Ponto de ônibus',
  station: 'Estação',
  park: 'Parque',
  city: 'Cidade',
  town: 'Cidade',
  village: 'Vila',
  neighbourhood: 'Bairro',
  suburb: 'Bairro',
  administrative: 'Região',
  state: 'Estado',
  country: 'País',
};

@Injectable({
  providedIn: 'root'
})
export class GeocodingService {
  private readonly baseUrl = 'https://nominatim.openstreetmap.org/search';

  constructor(private http: HttpClient) {}

  search(query: string, viewbox?: { west: number; south: number; east: number; north: number }): Observable<GeocodingResult[]> {
    if (!query || query.trim().length < 3) {
      return of([]);
    }

    const params: Record<string, string> = {
      q: query.trim(),
      format: 'json',
      addressdetails: '1',
      limit: '5'
    };

    if (viewbox) {
      params['viewbox'] = `${viewbox.west},${viewbox.north},${viewbox.east},${viewbox.south}`;
    }

    return this.http.get<NominatimResult[]>(this.baseUrl, { params }).pipe(
      map(results => results.map(r => this.parseResult(r))),
      catchError(() => of([]))
    );
  }

  private parseResult(r: NominatimResult): GeocodingResult {
    const addr = r.address;
    const name = r.name || r.display_name.split(',')[0].trim();
    const type = TYPE_LABELS[r.type] ?? this.capitalize(r.type.replace(/_/g, ' '));
    const city = addr.city || addr.town || addr.village || '';
    const parts = [addr.road, addr.neighbourhood || addr.suburb, city, addr.state]
      .filter(Boolean);

    return {
      name,
      type,
      address: parts.join(', '),
      displayName: r.display_name,
      lat: parseFloat(r.lat),
      lng: parseFloat(r.lon)
    };
  }

  private capitalize(text: string): string {
    return text.charAt(0).toUpperCase() + text.slice(1);
  }
}
