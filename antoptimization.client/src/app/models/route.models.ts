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
