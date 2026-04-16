using AntOptimization.Domain.Algorithms;
using AntOptimization.Domain.Interfaces;

namespace AntOptimization.Services;

public class GeneticAlgorithmService : IGeneticAlgorithmService
{
    public (List<int> BestTour, double BestDistance) Optimize(double[,] distanceMatrix, int? startCity = null)
    {
        var engine = new GeneticAlgorithmEngine(DefaultParameters);
        return engine.Solve(distanceMatrix, startCity);
    }

    private static GeneticAlgorithmParameters DefaultParameters => new()
    {
        PopulationSize = 100,
        Generations = 200,
        MutationRate = 0.02,
        TournamentSize = 5
    };
}
