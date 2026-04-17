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
