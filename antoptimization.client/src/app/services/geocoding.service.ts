import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { GeocodingResult } from '../models/geocoding.models';

interface PhotonResponse {
  features: PhotonFeature[];
}

interface PhotonFeature {
  geometry: {
    coordinates: [number, number]; // [lon, lat]
  };
  properties: {
    name?: string;
    street?: string;
    housenumber?: string;
    district?: string;
    city?: string;
    state?: string;
    country?: string;
    osm_key?: string;
    osm_value?: string;
  };
}

const TYPE_LABELS: Record<string, string> = {
  mall: 'Shopping',
  supermarket: 'Supermercado',
  convenience: 'Conveniência',
  residential: 'Rua',
  house: 'Endereço',
  apartments: 'Edifício',
  commercial: 'Comercial',
  retail: 'Comércio',
  restaurant: 'Restaurante',
  fast_food: 'Fast Food',
  cafe: 'Café',
  bar: 'Bar',
  pharmacy: 'Farmácia',
  hospital: 'Hospital',
  clinic: 'Clínica',
  school: 'Escola',
  university: 'Universidade',
  bus_stop: 'Ponto de ônibus',
  station: 'Estação',
  park: 'Parque',
  hotel: 'Hotel',
  bank: 'Banco',
  fuel: 'Posto de combustível',
  parking: 'Estacionamento',
  place_of_worship: 'Templo religioso',
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
  private readonly baseUrl = 'https://photon.komoot.io/api/';

  constructor(private http: HttpClient) {}

  search(query: string, center?: { lat: number; lng: number }): Observable<GeocodingResult[]> {
    if (!query || query.trim().length < 3) {
      return of([]);
    }

    const params: Record<string, string> = {
      q: query.trim(),
      limit: '5'
    };

    if (center) {
      params['lat'] = String(center.lat);
      params['lon'] = String(center.lng);
    }

    return this.http.get<PhotonResponse>(this.baseUrl, { params }).pipe(
      map(response => {
        const results = response.features.map(f => this.parseFeature(f));
        return this.rerank(results, query);
      }),
      catchError(() => of([]))
    );
  }

  private rerank(results: GeocodingResult[], query: string): GeocodingResult[] {
    const normalized = this.normalize(query);
    const words = normalized.split(/\s+/).filter(w => w.length > 1);

    if (words.length <= 1) return results;

    return [...results].sort((a, b) => {
      return this.textMatchScore(b.name, words, normalized)
           - this.textMatchScore(a.name, words, normalized);
    });
  }

  private textMatchScore(name: string, queryWords: string[], fullQuery: string): number {
    const normalized = this.normalize(name);
    if (normalized.includes(fullQuery)) return 1;
    const matches = queryWords.filter(w => normalized.includes(w)).length;
    return matches / queryWords.length;
  }

  private normalize(text: string): string {
    return text.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '');
  }

  private parseFeature(f: PhotonFeature): GeocodingResult {
    const p = f.properties;
    const name = p.name || [p.street, p.housenumber].filter(Boolean).join(' ') || 'Sem nome';
    const typeKey = p.osm_value || p.osm_key || '';
    const type = TYPE_LABELS[typeKey] ?? this.capitalize(typeKey.replace(/_/g, ' '));

    const addressParts = [p.street, p.district, p.city, p.state].filter(Boolean);

    return {
      name,
      type,
      address: addressParts.join(', '),
      displayName: [name, ...addressParts].join(', '),
      lat: f.geometry.coordinates[1],
      lng: f.geometry.coordinates[0]
    };
  }

  private capitalize(text: string): string {
    if (!text) return '';
    return text.charAt(0).toUpperCase() + text.slice(1);
  }
}
