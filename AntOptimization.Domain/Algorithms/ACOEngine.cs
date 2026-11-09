namespace AntOptimization.Domain.Algorithms;

public class ACOEngine
{
    private readonly ACOParameters _parameters;
    private readonly Random _random;

    public ACOEngine(ACOParameters parameters, int? seed = null)
    {
        _parameters = parameters;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public (List<int> BestTour, double BestDistance) Solve(double[,] distanceMatrix, int? fixedStartCity = null)
    {
        var result = Run(distanceMatrix, fixedStartCity, onIteration: null);
        return (result.BestTour, result.BestDistance);
    }

    public (List<int> BestTour, double BestDistance, List<IterationSnapshot> History) SolveWithHistory(double[,] distanceMatrix, int? fixedStartCity = null)
    {
        var history = new List<IterationSnapshot>(_parameters.Iterations);

        var result = Run(distanceMatrix, fixedStartCity, (iteration, colony, bestTour, bestDistance) =>
        {
            history.Add(new IterationSnapshot(
                iteration,
                new List<int>(bestTour),
                bestDistance,
                colony.Ants.Select(a => new List<int>(a.Tour)).ToList()
            ));
        });

        return (result.BestTour, result.BestDistance, history);
    }

    public ACORunMetrics SolveWithMetrics(double[,] distanceMatrix, int? fixedStartCity = null)
    {
        var curve = new double[_parameters.Iterations];

        var result = Run(distanceMatrix, fixedStartCity, (iteration, _, _, bestDistance) =>
        {
            curve[iteration] = bestDistance;
        });

        return new ACORunMetrics(
            result.BestTour,
            result.BestDistance,
            result.BestIteration,
            (long)_parameters.NumberOfAnts * _parameters.Iterations,
            curve);
    }

    private (List<int> BestTour, double BestDistance, int BestIteration) Run(
        double[,] distanceMatrix,
        int? fixedStartCity,
        Action<int, Colony, List<int>, double>? onIteration)
    {
        int numberOfCities = distanceMatrix.GetLength(0);
        var colony = new Colony(_parameters.NumberOfAnts, numberOfCities);

        List<int> bestTour = [];
        double bestDistance = double.MaxValue;
        int bestIteration = -1;

        for (int iteration = 0; iteration < _parameters.Iterations; iteration++)
        {
            foreach (var ant in colony.Ants)
            {
                BuildTour(ant, colony.PheromoneMatrix, distanceMatrix, numberOfCities, fixedStartCity);

                if (ant.TourDistance < bestDistance)
                {
                    bestDistance = ant.TourDistance;
                    bestTour = new List<int>(ant.Tour);
                    bestIteration = iteration;
                }
            }

            onIteration?.Invoke(iteration, colony, bestTour, bestDistance);

            EvaporatePheromones(colony.PheromoneMatrix, numberOfCities);
            UpdatePheromones(colony);

            foreach (var ant in colony.Ants)
                ant.Reset();
        }

        return (bestTour, bestDistance, bestIteration);
    }

    private void BuildTour(Ant ant, double[,] pheromones, double[,] distances, int numberOfCities, int? fixedStartCity)
    {
        int startCity = fixedStartCity ?? _random.Next(numberOfCities);
        ant.Tour.Add(startCity);
        ant.Visited[startCity] = true;

        for (int step = 1; step < numberOfCities; step++)
        {
            int currentCity = ant.Tour[^1];
            int nextCity = SelectNextCity(currentCity, ant.Visited, pheromones, distances, numberOfCities);
            ant.Tour.Add(nextCity);
            ant.Visited[nextCity] = true;
        }

        ant.TourDistance = PathMath.PathDistance(ant.Tour, distances);
    }

    private int SelectNextCity(int currentCity, bool[] visited, double[,] pheromones, double[,] distances, int numberOfCities)
    {
        double[] probabilities = new double[numberOfCities];
        double total = 0;

        for (int j = 0; j < numberOfCities; j++)
        {
            if (visited[j]) continue;

            double pheromone = Math.Pow(pheromones[currentCity, j], _parameters.Alpha);
            double heuristic = distances[currentCity, j] > 0
                ? Math.Pow(1.0 / distances[currentCity, j], _parameters.Beta)
                : 0;

            probabilities[j] = pheromone * heuristic;
            total += probabilities[j];
        }

        if (total == 0)
        {
            for (int j = 0; j < numberOfCities; j++)
                if (!visited[j]) return j;
        }

        double randomValue = _random.NextDouble() * total;
        double cumulative = 0;

        for (int j = 0; j < numberOfCities; j++)
        {
            if (visited[j]) continue;
            cumulative += probabilities[j];
            if (cumulative >= randomValue) return j;
        }

        for (int j = numberOfCities - 1; j >= 0; j--)
            if (!visited[j]) return j;

        return 0;
    }

    private void EvaporatePheromones(double[,] pheromones, int numberOfCities)
    {
        for (int i = 0; i < numberOfCities; i++)
            for (int j = 0; j < numberOfCities; j++)
                pheromones[i, j] *= (1 - _parameters.EvaporationRate);
    }

    private static void UpdatePheromones(Colony colony)
    {
        foreach (var ant in colony.Ants)
        {
            if (ant.TourDistance <= 0) continue;

            double contribution = 1.0 / ant.TourDistance;
            for (int i = 0; i < ant.Tour.Count - 1; i++)
            {
                int from = ant.Tour[i];
                int to = ant.Tour[i + 1];
                colony.PheromoneMatrix[from, to] += contribution;
                colony.PheromoneMatrix[to, from] += contribution;
            }
        }
    }
}
