namespace AntOptimization.Domain.Algorithms;

public class ACOParameters
{
    public int NumberOfAnts { get; set; } = 20;
    public double EvaporationRate { get; set; } = 0.5;
    public double Alpha { get; set; } = 1.0;
    public double Beta { get; set; } = 2.0;
    public int Iterations { get; set; } = 100;
}
