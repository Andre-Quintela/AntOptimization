namespace AntOptimization.Domain.Algorithms;

public class GeneticAlgorithmParameters
{
    public int PopulationSize { get; set; } = 100;
    public int Generations { get; set; } = 200;
    public double MutationRate { get; set; } = 0.02;
    public int TournamentSize { get; set; } = 5;
}
