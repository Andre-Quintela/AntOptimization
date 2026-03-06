namespace AntOptimization.Domain.Algorithms;

public class ACOEngine
{
    private readonly ACOParameters _parameters;
    private readonly Random _random = new();

    public ACOEngine(ACOParameters parameters)
    {
        _parameters = parameters;
    }

    public (List<int> BestTour, double BestDistance) Solve(double[,] distanceMatrix, int? fixedStartCity = null)
    {
        int numberOfCities = distanceMatrix.GetLength(0);
        var colony = new Colony(_parameters.NumberOfAnts, numberOfCities);

        List<int> bestTour = [];
        double bestDistance = double.MaxValue;

        for (int iteration = 0; iteration < _parameters.Iterations; iteration++)
        {
            foreach (var ant in colony.Ants)
            {
                BuildTour(ant, colony.PheromoneMatrix, distanceMatrix, numberOfCities, fixedStartCity);

                if (ant.TourDistance < bestDistance)
                {
                    bestDistance = ant.TourDistance;
                    bestTour = new List<int>(ant.Tour);
                }
            }

            EvaporatePheromones(colony.PheromoneMatrix, numberOfCities);
            UpdatePheromones(colony);

            foreach (var ant in colony.Ants)
                ant.Reset();
        }

        return (bestTour, bestDistance);
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

        ant.TourDistance = CalculateTourDistance(ant.Tour, distances);
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

    private static double CalculateTourDistance(List<int> tour, double[,] distances)
    {
        double total = 0;
        for (int i = 0; i < tour.Count - 1; i++)
            total += distances[tour[i], tour[i + 1]];
        return total;
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
