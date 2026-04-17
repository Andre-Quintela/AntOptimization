namespace AntOptimization.Domain.Algorithms;

public class GeneticAlgorithmEngine
{
    private readonly GeneticAlgorithmParameters _parameters;
    private readonly Random _random = new();

    public GeneticAlgorithmEngine(GeneticAlgorithmParameters parameters)
    {
        _parameters = parameters;
    }

    public (List<int> BestTour, double BestDistance) Solve(double[,] distanceMatrix, int? fixedStartCity = null)
    {
        int n = distanceMatrix.GetLength(0);

        // Initialize population with random permutations
        var population = new List<List<int>>(_parameters.PopulationSize);
        for (int i = 0; i < _parameters.PopulationSize; i++)
            population.Add(CreateRandomTour(n, fixedStartCity));

        List<int> bestTour = population[0];
        double bestDistance = TourDistance(bestTour, distanceMatrix);

        for (int gen = 0; gen < _parameters.Generations; gen++)
        {
            var newPopulation = new List<List<int>>(_parameters.PopulationSize);

            for (int i = 0; i < _parameters.PopulationSize; i++)
            {
                var parent1 = TournamentSelect(population, distanceMatrix);
                var parent2 = TournamentSelect(population, distanceMatrix);
                var child = OrderCrossover(parent1, parent2, fixedStartCity);

                if (_random.NextDouble() < _parameters.MutationRate)
                    SwapMutate(child, fixedStartCity);

                newPopulation.Add(child);

                double d = TourDistance(child, distanceMatrix);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    bestTour = new List<int>(child);
                }
            }

            population = newPopulation;
        }

        return (bestTour, bestDistance);
    }

    private List<int> CreateRandomTour(int n, int? fixedStartCity)
    {
        var cities = Enumerable.Range(0, n).ToList();

        if (fixedStartCity.HasValue)
        {
            cities.Remove(fixedStartCity.Value);
            Shuffle(cities);
            cities.Insert(0, fixedStartCity.Value);
        }
        else
        {
            Shuffle(cities);
        }

        return cities;
    }

    private void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private List<int> TournamentSelect(List<List<int>> population, double[,] distanceMatrix)
    {
        List<int>? best = null;
        double bestDist = double.MaxValue;

        for (int i = 0; i < _parameters.TournamentSize; i++)
        {
            var candidate = population[_random.Next(population.Count)];
            double d = TourDistance(candidate, distanceMatrix);
            if (d < bestDist)
            {
                bestDist = d;
                best = candidate;
            }
        }

        return best!;
    }

    private List<int> OrderCrossover(List<int> parent1, List<int> parent2, int? fixedStartCity)
    {
        int n = parent1.Count;
        int startIdx = fixedStartCity.HasValue ? 1 : 0;

        int cut1 = _random.Next(startIdx, n);
        int cut2 = _random.Next(startIdx, n);
        if (cut1 > cut2) (cut1, cut2) = (cut2, cut1);

        var child = new int[n];
        if (fixedStartCity.HasValue)
            child[0] = fixedStartCity.Value;

        var inChild = new HashSet<int>();
        if (fixedStartCity.HasValue)
            inChild.Add(fixedStartCity.Value);

        for (int i = cut1; i <= cut2; i++)
        {
            child[i] = parent1[i];
            inChild.Add(parent1[i]);
        }

        int pos = cut2 + 1 < n ? cut2 + 1 : startIdx;
        foreach (int city in parent2)
        {
            if (inChild.Contains(city)) continue;
            child[pos] = city;
            inChild.Add(city);
            pos++;
            if (pos >= n) pos = startIdx;
        }

        return new List<int>(child);
    }

    private void SwapMutate(List<int> tour, int? fixedStartCity)
    {
        int startIdx = fixedStartCity.HasValue ? 1 : 0;
        if (tour.Count - startIdx < 2) return;

        int i = _random.Next(startIdx, tour.Count);
        int j = _random.Next(startIdx, tour.Count);
        (tour[i], tour[j]) = (tour[j], tour[i]);
    }

    private static double TourDistance(List<int> tour, double[,] distances)
    {
        double total = 0;
        for (int i = 0; i < tour.Count - 1; i++)
            total += distances[tour[i], tour[i + 1]];
        return total;
    }
}
