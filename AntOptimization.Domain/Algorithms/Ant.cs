namespace AntOptimization.Domain.Algorithms;

public class Ant
{
    public List<int> Tour { get; set; } = [];
    public double TourDistance { get; set; }
    public bool[] Visited { get; set; } = [];

    public void Initialize(int numberOfCities)
    {
        Tour = new List<int>(numberOfCities);
        Visited = new bool[numberOfCities];
        TourDistance = 0;
    }

    public void Reset()
    {
        Tour.Clear();
        Array.Fill(Visited, false);
        TourDistance = 0;
    }
}
