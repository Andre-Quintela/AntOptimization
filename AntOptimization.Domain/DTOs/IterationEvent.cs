namespace AntOptimization.Domain.DTOs;

public class IterationEvent
{
    public string Type => "iteration";
    public int Iteration { get; set; }
    public int TotalIterations { get; set; }
    public List<int> BestTourSoFar { get; set; } = [];
    public double BestDistanceSoFar { get; set; }
    public List<List<int>> AntTours { get; set; } = [];
}
