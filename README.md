# 🐜 AntOptimization

Route optimization application that solves the **Travelling Salesman Problem (TSP)** using four different algorithms — **Ant Colony Optimization (ACO)**, **Nearest Neighbor**, **2-Opt**, and **Genetic Algorithm** — with side-by-side performance comparison and a visual quality metric.

Built with **.NET 10** and **Angular**, it features a fast optimization mode, a **real-time visual mode** that streams each ACO iteration to the browser, and a **comparison dashboard** that runs all four algorithms and ranks them by solution quality.

---

## ✨ Features

- 📍 **Interactive map** — click to add stops, search addresses, or use your current location
- 🚀 **Fast mode** — runs all 100 iterations and returns the optimal route instantly
- 🎬 **Visual mode** — streams every iteration via SSE, rendering all ant tours on the map in real time
- 📌 **Fixed start point** — optionally lock the departure location
- 📏 **Real-world distances** — uses the OSRM routing engine for driving distances and route geometry
- 📊 **Algorithm comparison dashboard** — runs all 4 algorithms on the same input and displays results side by side with bar charts
- 🏆 **Solution quality metric** — ranks each algorithm by its *optimality gap* relative to the best solution found

---

## 🏗️ Architecture

The solution follows **Clean Architecture**, organized into five projects:

```
AntOptimization.Domain         # Core: algorithms, models, interfaces, DTOs
AntOptimization.Services       # Application services: ACO, NN, 2-Opt, GA, comparison
AntOptimization.Infrastructure # External I/O: OSRM API client
AntOptimization.IoC            # Dependency injection wiring
AntOptimization.Server         # ASP.NET Core API + Angular host
antoptimization.client/        # Angular frontend
```

### Dependency flow

```
Server → IoC → Services → Domain ← Infrastructure
```

No outer layer knows about any layer below it; all dependencies point inward toward `Domain`.

---

## 🔬 Optimality Gap — Solution Quality Metric

The comparison dashboard measures how close each algorithm's solution is to the **best solution found** in that run, using the standard *relative gap* formula from combinatorial optimization research:

```
gap(i) = ( distance(i) − min_distance ) / min_distance × 100
```

| Gap value | Interpretation | Visual indicator |
|-----------|---------------|-----------------|
| `0.00%` | Best solution found — labelled **"Ótimo"** | Full green bar |
| `> 0%` and `≤ 10%` | Close to optimal | Partial yellow bar |
| `> 10%` | Noticeably worse | Short red bar |

The gap is computed server-side in `CompareRouteService` after all four algorithms finish, so it is always relative to the best result in the **current run**. No external benchmark is needed.

---

## 🧠 Algorithms

### Ant Colony Optimization (ACO)

Implemented from scratch in `ACOEngine.cs`. A colony of ants builds tours probabilistically using pheromone trails and distance heuristics, reinforcing shorter paths over 100 iterations.

`P(i → j) = [ τ(i,j)^α · η(i,j)^β ] / Σ [ τ(i,k)^α · η(i,k)^β ]`

**Default parameters:**

| Parameter         | Value | Description                         |
|-------------------|-------|-------------------------------------|
| `NumberOfAnts`    | 20    | Ants exploring per iteration        |
| `Alpha` (α)       | 1.0   | Pheromone influence weight          |
| `Beta` (β)        | 2.0   | Distance heuristic influence weight |
| `EvaporationRate` | 0.5   | Pheromone decay per iteration (50%) |
| `Iterations`      | 100   | Total number of optimization cycles |

### Nearest Neighbor (NN)

Greedy constructive heuristic. Starting from the departure point, always moves to the nearest unvisited location. Fast but can produce suboptimal solutions.

### 2-Opt

Local search improvement heuristic. Iteratively reverses segments of the current tour whenever the swap reduces total distance. Continues until no improving swap exists.

### Genetic Algorithm (GA)

Evolutionary metaheuristic. Maintains a population of candidate tours, evolving them through selection, ordered crossover (OX), and mutation over multiple generations.

---

## 🛠️ Tech Stack

### Backend

| Technology         | Usage                                      |
|--------------------|--------------------------------------------|
| .NET 10 / C# 14    | Runtime and language                       |
| ASP.NET Core       | REST API and SSE streaming                 |
| `IAsyncEnumerable` | Real-time iteration streaming with SSE     |
| OSRM API           | Driving distance matrix and route geometry |
| OpenAPI            | API documentation                          |

### Frontend

| Technology                 | Usage                                       |
|----------------------------|---------------------------------------------|
| Angular (NgModules)        | UI framework                                |
| Leaflet.js                 | Interactive map rendering                   |
| Chart.js                   | Bar charts in the comparison dashboard      |
| RxJS                       | Reactive streams and geocoding search       |
| Fetch API + ReadableStream | Manual SSE consumption (supports POST body) |
| Photon API (OpenStreetMap) | Address geocoding                           |

### Infrastructure & CI/CD

| Technology     | Usage          |
|----------------|----------------|
| GitHub Actions | CI/CD pipeline |
| Azure          | Cloud hosting  |

---

## 🔌 API Endpoints

### `POST /api/routes/compare`

Runs all four algorithms on the same input and returns ranked results with execution times and optimality gaps.

**Request body:** same shape as `/optimize`.

**Response:**
```json
{
  "results": [
    {
      "algorithm": "ACO",
      "bestRouteOrder": [0, 2, 1, 3],
      "totalDistance": 12.34,
      "executionTimeMs": 310,
      "relativeGapPercent": 0.0,
      "routeCoordinates": [...]
    },
    {
      "algorithm": "Nearest Neighbor",
      "bestRouteOrder": [0, 1, 2, 3],
      "totalDistance": 13.05,
      "executionTimeMs": 12,
      "relativeGapPercent": 5.75,
      "routeCoordinates": [...]
    }
  ]
}
```

`relativeGapPercent` is `0.0` for the best algorithm and positive for the others (see [Optimality Gap](#-optimality-gap--solution-quality-metric)).

---

### `POST /api/routes/optimize`

Runs the ACO algorithm and returns the optimized route.

**Request body:**
```json
{
  "locations": [
    { "lat": -23.5505, "lng": -46.6333 },
    { "lat": -23.5615, "lng": -46.6560 }
  ],
  "startLocationIndex": 0
}
```

**Response:**
```json
{
  "bestRouteOrder": [0, 2, 1, 3],
  "totalDistance": 12.34,
  "routeCoordinates": [{ "lat": -23.5505, "lng": -46.6333 }]
}
```

---

### `POST /api/routes/optimize-visual`

Streams each iteration as a **Server-Sent Event**. Returns two event types:

**`IterationEvent`** — emitted once per iteration:
```json
{
  "iteration": 42,
  "totalIterations": 100,
  "bestTourSoFar": [0, 3, 1, 2],
  "bestDistanceSoFar": 14.7,
  "antTours": [[0, 1, 3, 2]]
}
```

**`VisualOptimizationResult`** — emitted once after all iterations complete:
```json
{
  "bestRouteOrder": [0, 3, 1, 2],
  "totalDistance": 12.34,
  "routeCoordinates": [{ "lat": -23.5505, "lng": -46.6333 }]
}
```

> The stream is cancelled gracefully if the client disconnects, thanks to `[EnumeratorCancellation] CancellationToken` propagated through the `IAsyncEnumerable` pipeline.

---

## 🚀 Running Locally

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)

### Steps

```bash
# Clone the repository
git clone https://github.com/Andre-Quintela/AntOptimization.git
cd AntOptimization

# Restore backend dependencies
dotnet restore

# Install frontend dependencies
cd antoptimization.client
npm install
cd ..

# Run (serves both API and Angular together)
dotnet run --project AntOptimization.Server
```

The app will be available at `https://localhost:5001`.

> The Angular proxy forwards `/api` requests to the .NET backend automatically during development.

---

## 📁 Project Structure

```
AntOptimization/
├── AntOptimization.Domain/
│   ├── Algorithms/         # ACOEngine, ACOParameters, Colony, Ant, IterationSnapshot
│   ├── DTOs/               # OptimizationRequest/Response, IterationEvent, AlgorithmResult...
│   ├── Interfaces/         # IRouteService, IAntColonyOptimizationService, INearestNeighborService,
│   │                       # ITwoOptService, IGeneticAlgorithmService, IDistanceMatrixService
│   └── Models/             # Location
├── AntOptimization.Services/
│   ├── AntColonyOptimizationService.cs
│   ├── NearestNeighborService.cs
│   ├── TwoOptService.cs
│   ├── GeneticAlgorithmService.cs
│   ├── CompareRouteService.cs         # Runs all 4 algorithms + computes optimality gaps
│   └── RouteService.cs
├── AntOptimization.Infrastructure/
│   └── Services/
│       └── DistanceMatrixService.cs   # OSRM HTTP client
├── AntOptimization.IoC/
│   └── DependencyInjectionExtensions.cs
├── AntOptimization.Server/
│   ├── Controllers/
│   │   └── RouteController.cs         # /optimize, /optimize-visual, /compare
│   └── Program.cs
├── AntOptimization.Tests/
│   └── Services/
│       └── CompareRouteServiceTests.cs
└── antoptimization.client/            # Angular app
    └── src/app/
        ├── components/
        │   ├── map/                   # Main map + ACO optimization
        │   └── dashboard/             # Algorithm comparison dashboard
        ├── services/                  # RouteService, GeocodingService
        └── models/                    # TypeScript interfaces
```

---

## 📄 License

MIT