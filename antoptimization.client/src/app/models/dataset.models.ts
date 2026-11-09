import { LocationDto } from './route.models';

export type DistanceSource = 'Osrm' | 'Haversine';

export type DatasetJobStatus =
  | 'Pending'
  | 'Running'
  | 'Completed'
  | 'CompletedWithErrors'
  | 'Failed'
  | 'Cancelled'
  | 'Interrupted';

export type DatasetInstanceStatus = 'Pending' | 'Completed' | 'Failed' | 'ExceededLimit';

export type DatasetExportLevel = 'Runs' | 'Instances' | 'Configs' | 'Zip';

export interface RegionDto {
  south: number;
  west: number;
  north: number;
  east: number;
}

export interface HyperparameterGrid {
  alpha: number[];
  beta: number[];
  numberOfAnts: number[];
  evaporationRate: number[];
  iterations: number[];
}

export interface CreateDatasetJobRequest {
  name?: string;
  region: RegionDto;
  /** Instance sizes to cover; one point set is drawn per size per replica. */
  pointCounts: number[];
  /** Independent point sets drawn for each size. */
  instancesPerPointCount: number;
  distanceSource: DistanceSource;
  fixedStart: boolean;
  repetitions: number;
  masterSeed?: number | null;
  grid: HyperparameterGrid;
}

export interface DatasetEstimate {
  isValid: boolean;
  errors: string[];
  warnings: string[];
  configurationCount: number;
  /** Normalized (deduplicated, ascending) sizes the job will generate. */
  pointCounts: number[];
  /** Sizes x instances per size. */
  totalInstances: number;
  totalRuns: number;
  exactMethod: string | null;
  exactOperationsPerInstance: number;
  exactMemoryBytes: number;
  estimatedAcoSeconds: number;
  estimatedExactSeconds: number;
  estimatedNetworkSeconds: number;
  estimatedTotalSeconds: number;
}

export interface DatasetJob {
  id: string;
  name: string | null;
  createdAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  status: DatasetJobStatus;
  errorMessage: string | null;
  region: RegionDto;
  pointCounts: number[];
  instancesPerPointCount: number;
  instanceCount: number;
  distanceSource: DistanceSource;
  fixedStart: boolean;
  repetitions: number;
  masterSeed: number;
  grid: HyperparameterGrid;
  configurationCount: number;
  totalRuns: number;
  completedRuns: number;
  processedInstances: number;
  failedInstances: number;
  progressPercent: number;
}

export interface InstanceFeatures {
  pointCount: number;
  regionWidthKm: number;
  regionHeightKm: number;
  regionAreaKm2: number;
  pointDensityPerKm2: number;
  centroidDistanceMeanKm: number;
  centroidDistanceStdKm: number;
  spatialNearestNeighborMeanKm: number;
  clarkEvansRatio: number;
  costMeanKm: number;
  costStdKm: number;
  costMinKm: number;
  costMaxKm: number;
  costMedianKm: number;
  costCoefficientOfVariation: number;
  costNearestNeighborMeanKm: number;
  costNearestNeighborStdKm: number;
  asymmetryIndex: number;
  detourIndex: number;
  nearestNeighborDistanceM: number;
  twoOptDistanceM: number;
  nearestNeighborGapPercent: number | null;
  twoOptGapPercent: number | null;
}

export interface DatasetInstance {
  id: string;
  index: number;
  pointCount: number;
  seed: number;
  status: DatasetInstanceStatus;
  errorMessage: string | null;
  generationAttempts: number;
  coordinates: LocationDto[];
  fixedStartIndex: number | null;
  features: InstanceFeatures | null;
  optimalDistanceM: number | null;
  optimalTour: number[];
  exactMethod: string | null;
  exactTimeMs: number | null;
  exactOperations: number | null;
  runCount: number;
  bestGapPercent: number | null;
  meanGapPercent: number | null;
}

export interface AcoRun {
  id: string;
  configKey: string;
  repetition: number;
  seed: number;
  status: 'Completed' | 'Failed';
  errorMessage: string | null;
  alpha: number;
  beta: number;
  numberOfAnts: number;
  evaporationRate: number;
  iterations: number;
  bestDistanceM: number;
  gapAbsM: number;
  gapPercent: number;
  reachedOptimum: boolean;
  bestIteration: number;
  firstOptimalIteration: number | null;
  toursConstructed: number;
  executionTimeMs: number;
  bestTour: number[];
}

export interface ConfigSummary {
  configKey: string;
  alpha: number;
  beta: number;
  numberOfAnts: number;
  evaporationRate: number;
  iterations: number;
  runCount: number;
  instanceCount: number;
  meanGapPercent: number;
  stdGapPercent: number;
  minGapPercent: number;
  maxGapPercent: number;
  optimalHitRate: number;
  meanExecutionTimeMs: number;
  meanBestIteration: number;
  instancesWon: number;
}

export const ACTIVE_JOB_STATUSES: DatasetJobStatus[] = ['Pending', 'Running'];

export const JOB_STATUS_LABELS: Record<DatasetJobStatus, string> = {
  Pending: 'Na fila',
  Running: 'Em execução',
  Completed: 'Concluído',
  CompletedWithErrors: 'Concluído com falhas',
  Failed: 'Falhou',
  Cancelled: 'Cancelado',
  Interrupted: 'Interrompido'
};

export const INSTANCE_STATUS_LABELS: Record<DatasetInstanceStatus, string> = {
  Pending: 'Pendente',
  Completed: 'Concluída',
  Failed: 'Falhou',
  ExceededLimit: 'Limite excedido'
};
