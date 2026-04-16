import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { RouteService } from './route.service';
import { OptimizationRequest, OptimizationResponse, IterationEvent, VisualOptimizationResult } from '../models/route.models';

describe('RouteService', () => {
  let service: RouteService;
  let httpMock: HttpTestingController;

  const mockRequest: OptimizationRequest = {
    locations: [
      { lat: -19.9, lng: -43.9 },
      { lat: -19.8, lng: -43.8 }
    ],
    startLocationIndex: 0
  };

  const mockResponse: OptimizationResponse = {
    bestRouteOrder: [0, 1],
    totalDistance: 15.5,
    routeCoordinates: [
      { lat: -19.9, lng: -43.9 },
      { lat: -19.8, lng: -43.8 }
    ]
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [RouteService]
    });

    service = TestBed.inject(RouteService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('optimizeRoute()', () => {
    it('should be created', () => {
      expect(service).toBeTruthy();
    });

    it('should POST to /api/routes/optimize with the correct payload', () => {
      service.optimizeRoute(mockRequest).subscribe();

      const req = httpMock.expectOne('/api/routes/optimize');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(mockRequest);
      req.flush(mockResponse);
    });

    it('should return the optimization response', () => {
      let result: OptimizationResponse | undefined;
      service.optimizeRoute(mockRequest).subscribe(r => (result = r));

      const req = httpMock.expectOne('/api/routes/optimize');
      req.flush(mockResponse);

      expect(result).toEqual(mockResponse);
    });

    it('should propagate HTTP errors', () => {
      let error: Error | undefined;
      service.optimizeRoute(mockRequest).subscribe({
        error: (err) => (error = err)
      });

      const req = httpMock.expectOne('/api/routes/optimize');
      req.flush('Server Error', { status: 500, statusText: 'Internal Server Error' });

      expect(error).toBeTruthy();
    });
  });

  describe('optimizeRouteVisual()', () => {
    let originalFetch: typeof fetch;

    beforeEach(() => {
      originalFetch = window.fetch;
    });

    afterEach(() => {
      window.fetch = originalFetch;
    });

    it('should return an Observable', () => {
      const obs = service.optimizeRouteVisual(mockRequest);
      expect(obs).toBeTruthy();
      expect(typeof obs.subscribe).toBe('function');
    });

    it('should call fetch with POST to /api/routes/optimize-visual', (done) => {
      const iterationEvent: IterationEvent = {
        type: 'iteration',
        iteration: 0,
        totalIterations: 10,
        bestTourSoFar: [0, 1],
        bestDistanceSoFar: 15.5,
        antTours: [[0, 1]]
      };

      const encoder = new TextEncoder();
      const sseData = `data: ${JSON.stringify(iterationEvent)}\n\n`;
      const chunk = encoder.encode(sseData);

      let readCount = 0;
      const mockReader = {
        read: jasmine.createSpy('read').and.callFake(() => {
          readCount++;
          if (readCount === 1) return Promise.resolve({ done: false, value: chunk });
          return Promise.resolve({ done: true, value: undefined });
        })
      };

      const mockBody = { getReader: () => mockReader };
      const mockFetchResponse = { ok: true, body: mockBody };

      window.fetch = jasmine.createSpy('fetch').and.returnValue(
        Promise.resolve(mockFetchResponse as unknown as Response)
      );

      const emitted: IterationEvent[] = [];
      service.optimizeRouteVisual(mockRequest).subscribe({
        next: (event) => emitted.push(event as IterationEvent),
        complete: () => {
          expect(window.fetch).toHaveBeenCalledWith(
            '/api/routes/optimize-visual',
            jasmine.objectContaining({ method: 'POST' })
          );
          expect(emitted.length).toBe(1);
          expect(emitted[0]).toEqual(iterationEvent);
          done();
        }
      });
    });

    it('should emit error when fetch response is not ok', (done) => {
      window.fetch = jasmine.createSpy('fetch').and.returnValue(
        Promise.resolve({ ok: false, status: 500 } as Response)
      );

      service.optimizeRouteVisual(mockRequest).subscribe({
        error: (err: Error) => {
          expect(err.message).toContain('500');
          done();
        }
      });
    });

    it('should emit result event correctly', (done) => {
      const resultEvent: VisualOptimizationResult = {
        type: 'result',
        bestRouteOrder: [0, 1],
        totalDistance: 15.5,
        routeCoordinates: [{ lat: -19.9, lng: -43.9 }, { lat: -19.8, lng: -43.8 }]
      };

      const encoder = new TextEncoder();
      const sseData = `data: ${JSON.stringify(resultEvent)}\n\n`;
      const chunk = encoder.encode(sseData);

      let readCount = 0;
      const mockReader = {
        read: jasmine.createSpy('read').and.callFake(() => {
          readCount++;
          if (readCount === 1) return Promise.resolve({ done: false, value: chunk });
          return Promise.resolve({ done: true, value: undefined });
        })
      };

      window.fetch = jasmine.createSpy('fetch').and.returnValue(
        Promise.resolve({ ok: true, body: { getReader: () => mockReader } } as unknown as Response)
      );

      const emitted: VisualOptimizationResult[] = [];
      service.optimizeRouteVisual(mockRequest).subscribe({
        next: (event) => emitted.push(event as VisualOptimizationResult),
        complete: () => {
          expect(emitted[0]).toEqual(resultEvent);
          done();
        }
      });
    });
  });
});
