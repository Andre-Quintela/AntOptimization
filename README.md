# 🐜 AntOptimization

Route optimization application that uses the **Ant Colony Optimization (ACO)** algorithm to solve the **Travelling Salesman Problem (TSP)** — finding the shortest route that visits all given points exactly once.

Built with **.NET 10** and **Angular**, it features both a fast optimization mode and a **real-time visual mode** that streams each algorithm iteration to the browser, letting you watch the colony converge to the best solution.

---

## ✨ Features

- 📍 **Interactive map** — click to add stops, search addresses, or use your current location
- 🚀 **Fast mode** — runs all 100 iterations and returns the optimal route instantly
- 🎬 **Visual mode** — streams every iteration via SSE, rendering all ant tours on the map in real time
- 📌 **Fixed start point** — optionally lock the departure location
- 📏 **Real-world distances** — uses the OSRM routing engine for driving distances and route geometry

---

## 🏗️ Architecture

The solution follows **Clean Architecture**, organized into five projects:

```
AntOptimization.Domain         # Core: algorithm, models, interfaces, DTOs
AntOptimization.Services       # Application services: orchestrates ACO and routing
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

## 🧠 The Algorithm

The ACO algorithm is implemented from scratch in `ACOEngine.cs`, with no external libraries.

**How it works:**

1. A **colony** of ants is placed on the graph (one per location).
2. Each ant builds a complete tour by selecting the next city probabilistically using **roulette wheel selection**:

   `P(i → j) = [ τ(i,j)^α · η(i,j)^β ] / Σ [ τ(i,k)^α · η(i,k)^β ]`

   where `τ` is the pheromone level and `η = 1/distance` is the heuristic.

3. After all ants finish their tours, **pheromones evaporate** across all edges.
4. Ants that found shorter routes **deposit more pheromone**, reinforcing better paths.
5. This repeats for `N` iterations — shorter paths accumulate more pheromone over time, guiding the colony toward the optimum.

**Default parameters:**

| Parameter         | Value | Description                         |
|-------------------|-------|-------------------------------------|
| `NumberOfAnts`    | 20    | Ants exploring per iteration        |
| `Alpha` (α)       | 1.0   | Pheromone influence weight          |
| `Beta` (β)        | 2.0   | Distance heuristic influence weight |
| `EvaporationRate` | 0.5   | Pheromone decay per iteration (50%) |
| `Iterations`      | 100   | Total number of optimization cycles |

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
│   ├── DTOs/               # OptimizationRequest, OptimizationResponse, IterationEvent...
│   ├── Interfaces/         # IRouteService, IAntColonyOptimizationService, IDistanceMatrixService
│   └── Models/             # Location
├── AntOptimization.Services/
│   ├── AntColonyOptimizationService.cs
│   └── RouteService.cs
├── AntOptimization.Infrastructure/
│   └── Services/
│       └── DistanceMatrixService.cs   # OSRM HTTP client
├── AntOptimization.IoC/
│   └── DependencyInjectionExtensions.cs
├── AntOptimization.Server/
│   ├── Controllers/
│   │   └── RouteController.cs
│   └── Program.cs
└── antoptimization.client/            # Angular app
    └── src/app/
        ├── components/map/            # Main map component
        ├── services/                  # RouteService, GeocodingService
        └── models/                    # TypeScript interfaces
```

---

## 📄 License

MIT