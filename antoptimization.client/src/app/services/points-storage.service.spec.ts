import { TestBed } from '@angular/core/testing';
import { PointsStorageService, SavedPoints } from './points-storage.service';
import { LocationDto } from '../models/route.models';

describe('PointsStorageService', () => {
  let service: PointsStorageService;

  const mockLocations: LocationDto[] = [
    { lat: -19.92080, lng: -43.93780 },
    { lat: -19.93500, lng: -43.92100 },
    { lat: -19.91000, lng: -43.95000 }
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(PointsStorageService);
    localStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
  });

  // ──────────────────────────────────────────
  // localStorage
  // ──────────────────────────────────────────

  describe('saveToLocalStorage', () => {
    it('should call localStorage.setItem with the correct key and serialized JSON', () => {
      spyOn(localStorage, 'setItem').and.callThrough();
      service.saveToLocalStorage(mockLocations, 0);

      expect(localStorage.setItem).toHaveBeenCalledWith(
        'antopt_saved_points',
        JSON.stringify({ locations: mockLocations, startIndex: 0 })
      );
    });

    it('should save with startIndex null when no start is set', () => {
      spyOn(localStorage, 'setItem').and.callThrough();
      service.saveToLocalStorage(mockLocations, null);

      expect(localStorage.setItem).toHaveBeenCalledWith(
        'antopt_saved_points',
        JSON.stringify({ locations: mockLocations, startIndex: null })
      );
    });
  });

  describe('loadFromLocalStorage', () => {
    it('should return null when no item is saved', () => {
      expect(service.loadFromLocalStorage()).toBeNull();
    });

    it('should return the saved object when an item is present', () => {
      const saved: SavedPoints = { locations: mockLocations, startIndex: 1 };
      localStorage.setItem('antopt_saved_points', JSON.stringify(saved));

      expect(service.loadFromLocalStorage()).toEqual(saved);
    });

    it('should return null when the stored value is invalid JSON', () => {
      localStorage.setItem('antopt_saved_points', 'not-valid-json');
      expect(service.loadFromLocalStorage()).toBeNull();
    });
  });

  describe('hasSavedPoints', () => {
    it('should return false when no item is saved', () => {
      expect(service.hasSavedPoints()).toBeFalse();
    });

    it('should return true when an item is saved', () => {
      localStorage.setItem('antopt_saved_points', JSON.stringify({ locations: [], startIndex: null }));
      expect(service.hasSavedPoints()).toBeTrue();
    });
  });

  describe('clearSavedPoints', () => {
    it('should call localStorage.removeItem with the correct key', () => {
      spyOn(localStorage, 'removeItem').and.callThrough();
      localStorage.setItem('antopt_saved_points', '{}');
      service.clearSavedPoints();

      expect(localStorage.removeItem).toHaveBeenCalledWith('antopt_saved_points');
      expect(localStorage.getItem('antopt_saved_points')).toBeNull();
    });
  });

  // ──────────────────────────────────────────
  // exportToCsv
  // ──────────────────────────────────────────

  describe('exportToCsv', () => {
    let anchorClickSpy: jasmine.Spy;
    let mockAnchor: Partial<HTMLAnchorElement>;

    beforeEach(() => {
      anchorClickSpy = jasmine.createSpy('click');
      mockAnchor = { click: anchorClickSpy, set href(_: string) {}, set download(_: string) {} };
      spyOn(document, 'createElement').and.returnValue(mockAnchor as HTMLAnchorElement);
      spyOn(URL, 'createObjectURL').and.returnValue('blob:mock-url');
      spyOn(URL, 'revokeObjectURL');
    });

    it('should call URL.createObjectURL with a Blob', () => {
      service.exportToCsv(mockLocations, 0);
      expect(URL.createObjectURL).toHaveBeenCalledWith(jasmine.any(Blob));
    });

    it('should set the download attribute to "pontos_rota.csv"', () => {
      const downloadSpy = jasmine.createSpy('downloadSetter');
      Object.defineProperty(mockAnchor, 'download', { set: downloadSpy, configurable: true });
      service.exportToCsv(mockLocations, 0);
      expect(downloadSpy).toHaveBeenCalledWith('pontos_rota.csv');
    });

    it('should call click() on the anchor element', () => {
      service.exportToCsv(mockLocations, 0);
      expect(anchorClickSpy).toHaveBeenCalled();
    });

    it('should call URL.revokeObjectURL after click', () => {
      service.exportToCsv(mockLocations, 0);
      expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:mock-url');
    });

    it('should generate a Blob with the correct CSV content (header + rows)', () => {
      let capturedBlob: Blob | undefined;
      (URL.createObjectURL as jasmine.Spy).and.callFake((b: Blob) => {
        capturedBlob = b;
        return 'blob:mock';
      });
      service.exportToCsv([mockLocations[0], mockLocations[1]], 0);

      return capturedBlob!.text().then(text => {
        const lines = text.split('\n');
        expect(lines[0]).toBe('lat,lng,is_start');
        expect(lines[1]).toBe(`${mockLocations[0].lat},${mockLocations[0].lng},true`);
        expect(lines[2]).toBe(`${mockLocations[1].lat},${mockLocations[1].lng},false`);
      });
    });
  });

  // ──────────────────────────────────────────
  // parseCsv
  // ──────────────────────────────────────────

  describe('parseCsv', () => {
    it('should parse a valid CSV correctly', () => {
      const csv = 'lat,lng,is_start\n-19.92080,-43.93780,true\n-19.93500,-43.92100,false';
      const result = service.parseCsv(csv);

      expect(result.locations.length).toBe(2);
      expect(result.locations[0]).toEqual({ lat: -19.92080, lng: -43.93780 });
      expect(result.locations[1]).toEqual({ lat: -19.93500, lng: -43.92100 });
      expect(result.startIndex).toBe(0);
    });

    it('should return startIndex null when no row has is_start=true', () => {
      const csv = 'lat,lng,is_start\n-19.92080,-43.93780,false\n-19.93500,-43.92100,false';
      const result = service.parseCsv(csv);
      expect(result.startIndex).toBeNull();
    });

    it('should set startIndex to the correct index when is_start=true is in the second row', () => {
      const csv = 'lat,lng,is_start\n-19.92080,-43.93780,false\n-19.93500,-43.92100,true';
      const result = service.parseCsv(csv);
      expect(result.startIndex).toBe(1);
    });

    it('should throw an Error if the header is invalid', () => {
      const csv = 'latitude,longitude,start\n-19.92080,-43.93780,true';
      expect(() => service.parseCsv(csv)).toThrowError(/cabeçalho/i);
    });

    it('should throw an Error if a row has non-numeric lat/lng', () => {
      const csv = 'lat,lng,is_start\nabc,def,false';
      expect(() => service.parseCsv(csv)).toThrowError(/coordenadas inválidas/i);
    });

    it('should throw an Error if a row is malformed (missing columns)', () => {
      const csv = 'lat,lng,is_start\n-19.92080,-43.93780';
      expect(() => service.parseCsv(csv)).toThrowError(/mal formatada/i);
    });

    it('should ignore blank lines at the end', () => {
      const csv = 'lat,lng,is_start\n-19.92080,-43.93780,false\n\n\n';
      const result = service.parseCsv(csv);
      expect(result.locations.length).toBe(1);
    });

    it('should parse CSV with Windows line endings (CRLF)', () => {
      const csv = 'lat,lng,is_start\r\n-19.92080,-43.93780,true\r\n-19.93500,-43.92100,false';
      const result = service.parseCsv(csv);
      expect(result.locations.length).toBe(2);
      expect(result.startIndex).toBe(0);
    });
  });
});
