# AntOptimization — CLAUDE.md

## Context

TCC (undergraduate thesis) project: a web application for solving the **Travelling Salesman Problem (TSP)** using metaheuristic algorithms, with focus on **Ant Colony Optimization (ACO)**. Users place locations on an interactive map, run optimization algorithms, and compare their results. Deployed to **Azure Web Apps** as "AntRoute".

---

## Architecture

**Clean Architecture** with 5 backend projects + 1 Angular frontend:

```
AntOptimization.Domain       → Pure domain: models, DTOs, interfaces, algorithm engines
AntOptimization.Services     → Application services: orchestrate domain engines
AntOptimization.Infrastructure → External integrations: OSRM routing client
AntOptimization.IoC          → Dependency injection wiring
AntOptimization.Server       → ASP.NET Core API + SPA host
AntOptimization.Tests        → xUnit test suite
AntOptimization.Benchmark    → Console runner for the algorithm-comparison experiment (TCC section 3.7)
antoptimization.client/      → Angular 18 frontend (SPA)
```

**Dependency rule:** Domain has zero external dependencies. Services depend only on Domain. Infrastructure depends only on Domain. IoC wires everything together.

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend runtime | .NET 10, C# 14 |
| API | ASP.NET Core Web API |
| Real-time | Server-Sent Events (SSE) via `IAsyncEnumerable` |
| External routing | OSRM (`https://router.project-osrm.org/`) — no API key needed |
| Frontend | Angular 18 (NgModules, not standalone) |
| Maps | Leaflet.js 1.9.4 + OpenStreetMap tiles |
| Charts | Chart.js 4.5.1 |
| Geocoding | Photon API (`https://photon.komoot.io/api/`) — no API key needed |
| Backend tests | xUnit + Moq + FluentAssertions |
| Frontend tests | Jasmine + Karma (headless Chrome in CI) |
| CI/CD | GitHub Actions → Azure Web Apps |

---

## Algorithms

Four optimization algorithms are implemented and compared:

| Algorithm | Class | Type | Quality |
|-----------|-------|------|---------|
| **ACO** | `ACOEngine` | Metaheuristic | Primary algorithm |
| **Nearest Neighbor** | `NearestNeighborEngine` | Greedy constructive | Baseline |
| **2-Opt** | `TwoOptEngine` | Local search | Refinement |
| **Genetic Algorithm** | `GeneticAlgorithmEngine` | Evolutionary | Alternative |

`ACOEngine` and `GeneticAlgorithmEngine` take an optional seed (`new GeneticAlgorithmEngine(parameters, seed)`); without it the run is not reproducible, which is what the interactive app uses. Experiments always pass a seed.

### ACO Default Parameters (`ACOParameters`)
- `NumberOfAnts`: 20
- `Iterations`: 100
- `Alpha` (pheromone weight): 1.0
- `Beta` (heuristic weight): 2.0
- `EvaporationRate`: 0.5

### GA Default Parameters (`GeneticAlgorithmParameters`)
- `PopulationSize`: 100
- `Generations`: 200
- `MutationRate`: 0.02
- `TournamentSize`: 5

### Pheromone Update Rule
```
P(i→j) = [τ(i,j)^α · η(i,j)^β] / Σ[τ(i,k)^α · η(i,k)^β]
Evaporate: τ *= (1 - EvaporationRate)
Deposit:   τ += 1 / tourDistance
```

---

## Business Rules

1. **Minimum 2 locations** required for any optimization request.
2. **Optional fixed start city**: if `StartLocationIndex` is provided, the tour must begin at that point. Must be a valid index (0 ≤ index < locations.count).
3. **Distances** are returned from OSRM in **meters** and converted to **kilometers** (÷ 1000) before display.
4. **OSRM coordinate order** is `lng,lat` (not `lat,lng`) — critical when building API URLs.
5. **Optimality gap** formula:
   ```
   RelativeGapPercent = ((distance - minDistance) / minDistance) × 100
   ```
   The algorithm with the shortest route always shows 0.00%. Others show how much worse they are as a percentage.
6. **ACO best distance is monotonically non-decreasing** — it never gets worse across iterations.
7. **Visual mode** adds a 50ms artificial delay between iteration events for smooth streaming UX.
8. **Comparison runs all 4 algorithms** on the same distance matrix sequentially and measures wall-clock execution time per algorithm.
9. **All engines optimize an open path** (no return to the start city). Exact solvers (`BruteForceEngine`, `HeldKarpEngine`) use the same objective and support asymmetric (OSRM) matrices.
10. **OSRM requires a `User-Agent`** — the public server answers 403 without one (set on the `HttpClient` in IoC).
11. **2-opt moves must use the exact delta on asymmetric matrices.** Reversing `tour[i+1..j]` flips every inner edge, and `d[y,x] != d[x,y]` with OSRM, so `TwoOptEngine.TwoOptDelta` adds the reversed-inner-edge difference to the two boundary edges and `Solve` recomputes the final distance from the tour. The symmetric boundary-only formula corrupts the running distance, can report a distance below the exact optimum, and lets a move and its inverse loop forever.
12. **No baseline may beat the exact optimum.** `NearestNeighbor` and `2-opt` solve the same open path on the same matrix, so `DatasetJobRunner.ValidateBaselines` fails the instance when either is below `OptimalDistanceM` (tolerance `OptimumTolerance × max(1, optimum)`) instead of letting it into the dataset.

---

## Dataset Generation Module (ML of ACO hyperparameters)

Generates experimental data to later train a model that recommends ACO hyperparameters per TSP instance. UI at `/dataset` (`DatasetPageComponent`).

**A job covers several instance sizes.** `PointCounts` (e.g. `[6, 8, 10, 12]`) × `InstancesPerPointCount` gives the total `InstanceCount`; each instance is an independent point draw in the same region, carries its own `PointCount` and its own exact optimum, and runs the whole grid × repetitions. Instances are laid out size by size with a contiguous `Index` (`(JobId, Index)` is unique).

**Pipeline per job** (`DatasetJobRunner`, executed one job at a time by `DatasetGenerationWorker` from `DatasetJobQueue`), per instance:
region (rectangle) → `instance.PointCount` random points (`RandomPointGenerator`, seeded) → cost matrix (OSRM or Haversine, rounded to mm; regenerated up to `MaxGenerationAttempts` if unreachable) → `InstanceFeatureExtractor` → exact optimum (`ExactSolver`: brute force ≤ `BruteForceMaxPoints`, Held-Karp ≤ `MaxExactPoints`, with timeout) → `ValidateBaselines` invariant check → every grid config × repetitions via `ACOEngine(params, seed).SolveWithMetrics` → instance + runs saved atomically.

- **Seeds** are derived deterministically (`SeedDerivation`, FNV-1a) from the job's master seed → fully reproducible.
- **Persistence**: SQLite + EF Core (`AntOptimizationDbContext`, migrations in `Infrastructure/Persistence/Migrations`, applied at startup). DB path: `ConnectionStrings:Dataset`, else `%HOME%/data` on Azure, else `App_Data` next to the binaries. Tables: `DatasetJobs` → `DatasetInstances` (features as owned columns) → `AcoRuns`.
- **Distances stored in meters**, features in km. Gap = `(aco − optimum) / optimum × 100`.
- **Limits** in `appsettings.json` → `DatasetGeneration` (`DatasetOptions`). Startup marks `Running` jobs as `Interrupted`; resume re-processes `Pending`/`Failed` instances only.
- **Export** (`DatasetExportService`): `runs.csv` (main training table), `instances.csv`, `configs_summary.csv`, or ZIP with `metadata.json`. `point_count` is an instance key column in every CSV (and is excluded from the reflected `InstanceFeatures` columns so no CSV header repeats it); `metadata.json` records `pointCounts` + `instancesPerPointCount`. Split train/test grouped by `instance_id`.
- **Generated dataset**: `datasets/vitoria-es/` (gitignored, 265 MB) — 330 instances, n = 5..15, 320,760 runs over Grande Vitória/ES with OSRM distances; see its `README.md`. Reproducible from the master seeds in `metadata.json`.
- **Article figure**: `docs/tools/fig_sens.py` builds the β × ρ gap heat map for the TCC from `runs.csv`.
- New migration: `dotnet ef migrations add <Name> --project AntOptimization.Infrastructure --startup-project AntOptimization.Infrastructure --output-dir Persistence/Migrations`

Endpoints (base `/api/datasets`): `POST /estimate`, `POST /jobs`, `GET /jobs`, `GET /jobs/{id}`, `GET /jobs/{id}/instances`, `GET /jobs/{id}/instances/{iid}/runs`, `GET /jobs/{id}/configs`, `POST /jobs/{id}/cancel|resume`, `DELETE /jobs/{id}`, `GET /jobs/{id}/export?level=Runs|Instances|Configs|Zip`, `GET /export?level=...`. Enums are serialized as strings.

Frontend: child components in `components/dataset/` have no own CSS; shared styles live in `pages/dataset-page/dataset-module.css` (global, scoped to `app-dataset-page`, registered in `angular.json`).

---

## Comparison Experiment (`AntOptimization.Benchmark`)

Console runner behind Table 1 of the article: the same instances solved by all four engines over one cost matrix, with wall-clock timing.

```bash
dotnet run -c Release --project AntOptimization.Benchmark -- --out datasets/comparacao
# --sizes 10,15,30,50  --instances 10  --reps 10  --seed 20251109  --haversine  --osrm-delay 1000
```

Points, ACO seeds and GA seeds all derive from the master seed (`SeedDerivation`), so a re-run reproduces the numbers exactly. Every engine is warmed up once before timing (otherwise the first measured size pays for JIT). Gap reference is the exact optimum when n ≤ `MaxExactPoints`, else the best distance found on the instance. Outputs to `datasets/comparacao/` (gitignored): `runs.csv`, `instances.csv`, `summary.csv`, `convergence.csv` (feeds `docs/tools/fig_conv.py`) and `metadata.json`.

---

## API Endpoints

Base: `/api/routes`

| Method | Route | Purpose | Input | Output |
|--------|-------|---------|-------|--------|
| POST | `/optimize` | ACO optimization | `OptimizationRequest` | `OptimizationResponse` |
| POST | `/compare` | All 4 algorithms | `CompareOptimizationRequest` | `CompareOptimizationResponse` |
| POST | `/optimize-visual` | ACO with SSE stream | `OptimizationRequest` | `text/event-stream` |

### SSE Event Format (`/optimize-visual`)
```
data: {"type":"iteration","iteration":1,"totalIterations":100,"bestTourSoFar":[...],"bestDistanceSoFar":12.34,"antTours":[[...],...]}

data: {"type":"result","bestRouteOrder":[0,2,1],"totalDistance":12.34,"routeCoordinates":[...]}
```

---

## Key Files

### Backend
- `AntOptimization.Domain/Algorithms/ACOEngine.cs` — Core ACO solver
- `AntOptimization.Domain/Algorithms/Colony.cs` — Pheromone matrix management
- `AntOptimization.Domain/Algorithms/Ant.cs` — Individual ant agent
- `AntOptimization.Domain/Algorithms/IterationSnapshot.cs` — History record (C# record type)
- `AntOptimization.Services/RouteService.cs` — Main orchestrator (ACO flow + streaming)
- `AntOptimization.Services/CompareRouteService.cs` — Comparison orchestrator (gap calculation)
- `AntOptimization.Infrastructure/Services/DistanceMatrixService.cs` — OSRM HTTP client
- `AntOptimization.IoC/DependencyInjectionExtensions.cs` — All DI registrations
- `AntOptimization.Server/Controllers/RouteController.cs` — API endpoints
- `AntOptimization.Server/Program.cs` — ASP.NET Core setup (CORS, SPA, OpenAPI)

### Frontend
- `antoptimization.client/src/app/components/map/map.component.ts` — Main map UI
- `antoptimization.client/src/app/components/dashboard/dashboard.component.ts` — Algorithm comparison UI
- `antoptimization.client/src/app/services/route.service.ts` — API client (including SSE via Fetch)
- `antoptimization.client/src/app/services/geocoding.service.ts` — Photon geocoding
- `antoptimization.client/src/app/services/points-storage.service.ts` — LocalStorage + CSV
- `antoptimization.client/src/app/services/location-state.service.ts` — Shared state (Map → Dashboard)
- `antoptimization.client/src/app/models/route.models.ts` — TypeScript interfaces
- `antoptimization.client/src/app/app-routing.module.ts` — Routes: `/` → Map, `/dashboard` → Dashboard

---

## Frontend Patterns

- **SSE streaming** uses Fetch API with `ReadableStream`, not Angular HttpClient (SSE requires manual stream reading)
- **Geocoding search** uses `debounceTime(400) + distinctUntilChanged() + switchMap` pattern
- **Observable cleanup** uses `takeUntil(this.destroy$)` with `ngOnDestroy`
- **Leaflet** is used directly (not wrapped) — initialize in `ngAfterViewInit` via `ViewChild`
- **Algorithm color map**:
  - ACO: `#2563eb` (blue)
  - Nearest Neighbor: `#16a34a` (green)
  - 2-Opt: `#d97706` (amber)
  - Genetic Algorithm: `#9333ea` (purple)
- **State management**: `LocationStateService` singleton (no NgRx) — MapComponent writes, DashboardComponent reads
- **LocalStorage key**: `'antopt_saved_points'`
- **CSV format**: header `lat,lng,is_start` + one row per point

---

## DI Registration (all Scoped)

Registered in `AntOptimization.IoC/DependencyInjectionExtensions.cs`:
- `IDistanceMatrixService` → `DistanceMatrixService` (HttpClient, base: `https://router.project-osrm.org/`)
- `IRouteService` → `RouteService`
- `IAntColonyOptimizationService` → `AntColonyOptimizationService`
- `INearestNeighborService` → `NearestNeighborService`
- `ITwoOptService` → `TwoOptService`
- `IGeneticAlgorithmService` → `GeneticAlgorithmService`
- `ICompareRouteService` → `CompareRouteService`

---

## Testing

### Backend (`AntOptimization.Tests/`)
- Tests organized in `Domain/`, `Services/`, `Controllers/` folders mirroring source
- Mocks injected via constructor (Moq)
- FluentAssertions for readable assertions
- Run: `dotnet test AntOptimization.Tests/AntOptimization.Tests.csproj`

### Frontend (`antoptimization.client/`)
- Jasmine + Karma; headless Chrome in CI
- `HttpTestingController` for HTTP mocks
- Fetch API mocked with Jasmine spies for SSE tests
- Run: `npm test -- --watch=false --browsers=ChromeHeadless`

---

## CI/CD

| Workflow | Trigger | Steps |
|----------|---------|-------|
| `ci.yml` | Push to non-master / PR to master | Backend tests + Frontend tests |
| `master_antroute.yml` | Push to master / manual | Tests → Build → Publish → Deploy to Azure Web App "AntRoute" |

Azure deployment uses OIDC federated authentication (no long-lived secrets).

---

## Running Locally

```bash
# Backend (from repo root)
dotnet run --project AntOptimization.Server

# Frontend (from antoptimization.client/)
npm install
npm start
# Dev server: http://localhost:52389
# API proxy: http://localhost:5000 (or configured port)
```

CORS is open (`AllowAngular` policy: any origin/method/header) for local development.
