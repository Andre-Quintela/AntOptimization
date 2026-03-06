export interface LocationDto {
  lat: number;
  lng: number;
}

export interface OptimizationRequest {
  locations: LocationDto[];
  startLocationIndex?: number;
}

export interface OptimizationResponse {
  bestRouteOrder: number[];
  totalDistance: number;
  routeCoordinates: LocationDto[];
}

export interface IterationEvent {
  type: 'iteration';
  iteration: number;
  totalIterations: number;
  bestTourSoFar: number[];
  bestDistanceSoFar: number;
  antTours: number[][];
}

export interface VisualOptimizationResult {
  type: 'result';
  bestRouteOrder: number[];
  totalDistance: number;
  routeCoordinates: LocationDto[];
}

export type VisualEvent = IterationEvent | VisualOptimizationResult;
