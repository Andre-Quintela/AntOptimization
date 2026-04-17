import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { GeocodingService } from './geocoding.service';
import { GeocodingResult } from '../models/geocoding.models';

const BASE_URL = 'https://photon.komoot.io/api/';

const mockFeature = {
  geometry: { coordinates: [-43.9378, -19.9208] },
  properties: {
    name: 'Praça da Liberdade',
    street: 'Rua da Bahia',
    city: 'Belo Horizonte',
    state: 'Minas Gerais',
    country: 'Brasil',
    osm_key: 'leisure',
    osm_value: 'park'
  }
};

describe('GeocodingService', () => {
  let service: GeocodingService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [GeocodingService]
    });

    service = TestBed.inject(GeocodingService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('search()', () => {
    it('should return empty array for empty query', (done) => {
      service.search('').subscribe(results => {
        expect(results).toEqual([]);
        done();
      });
      httpMock.expectNone(BASE_URL);
    });

    it('should return empty array for query shorter than 3 chars', (done) => {
      service.search('ab').subscribe(results => {
        expect(results).toEqual([]);
        done();
      });
      httpMock.expectNone(BASE_URL);
    });

    it('should make a GET request to the Photon API with the query', () => {
      service.search('Belo Horizonte').subscribe();

      const req = httpMock.expectOne(r => r.url === BASE_URL);
      expect(req.request.method).toBe('GET');
      expect(req.request.params.get('q')).toBe('Belo Horizonte');
      expect(req.request.params.get('limit')).toBe('5');
      req.flush({ features: [] });
    });

    it('should include lat/lon params when center is provided', () => {
      service.search('parque', { lat: -19.9208, lng: -43.9378 }).subscribe();

      const req = httpMock.expectOne(r => r.url === BASE_URL);
      expect(req.request.params.get('lat')).toBe('-19.9208');
      expect(req.request.params.get('lon')).toBe('-43.9378');
      req.flush({ features: [] });
    });

    it('should NOT include lat/lon params when center is not provided', () => {
      service.search('parque').subscribe();

      const req = httpMock.expectOne(r => r.url === BASE_URL);
      expect(req.request.params.has('lat')).toBeFalse();
      expect(req.request.params.has('lon')).toBeFalse();
      req.flush({ features: [] });
    });

    it('should parse a feature into a GeocodingResult correctly', (done) => {
      service.search('Praça').subscribe((results: GeocodingResult[]) => {
        expect(results.length).toBe(1);
        const r = results[0];
        expect(r.name).toBe('Praça da Liberdade');
        expect(r.type).toBe('Parque');
        expect(r.lat).toBeCloseTo(-19.9208);
        expect(r.lng).toBeCloseTo(-43.9378);
        expect(r.address).toContain('Belo Horizonte');
        done();
      });

      const req = httpMock.expectOne(r => r.url === BASE_URL);
      req.flush({ features: [mockFeature] });
    });

    it('should return empty array on HTTP error', (done) => {
      service.search('erro').subscribe(results => {
        expect(results).toEqual([]);
        done();
      });

      const req = httpMock.expectOne(r => r.url === BASE_URL);
      req.flush('Error', { status: 500, statusText: 'Server Error' });
    });

    it('should return empty array for a feature with no name using street + housenumber', (done) => {
      const featureNoName = {
        geometry: { coordinates: [-43.9, -19.9] },
        properties: {
          street: 'Av. Amazonas',
          housenumber: '123',
          city: 'Belo Horizonte',
          osm_key: 'building',
          osm_value: 'house'
        }
      };

      service.search('Amazonas').subscribe((results: GeocodingResult[]) => {
        expect(results[0].name).toBe('Av. Amazonas 123');
        done();
      });

      const req = httpMock.expectOne(r => r.url === BASE_URL);
      req.flush({ features: [featureNoName] });
    });

    it('should rerank results placing better text matches first', (done) => {
      const featureA = {
        geometry: { coordinates: [-43.9, -19.9] },
        properties: { name: 'Centro Comercial', city: 'BH', osm_value: 'commercial' }
      };
      const featureB = {
        geometry: { coordinates: [-43.8, -19.8] },
        properties: { name: 'Centro Cultural', city: 'BH', osm_value: 'cultural' }
      };

      // Query has two words — reranking should prefer the one with both words matching
      service.search('Centro Cultural').subscribe((results: GeocodingResult[]) => {
        // "Centro Cultural" scores higher for featureB
        expect(results[0].name).toBe('Centro Cultural');
        done();
      });

      const req = httpMock.expectOne(r => r.url === BASE_URL);
      // API returns A first, B second — rerank should swap them
      req.flush({ features: [featureA, featureB] });
    });
  });
});
