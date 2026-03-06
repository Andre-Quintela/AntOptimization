namespace AntOptimization.Domain.Algorithms;

public class Colony
{
    public List<Ant> Ants { get; }
    public double[,] PheromoneMatrix { get; set; }
    public int NumberOfCities { get; }

    public Colony(int numberOfAnts, int numberOfCities)
    {
        NumberOfCities = numberOfCities;
        PheromoneMatrix = new double[numberOfCities, numberOfCities];
        Ants = new List<Ant>(numberOfAnts);

        for (int i = 0; i < numberOfAnts; i++)
        {
            var ant = new Ant();
            ant.Initialize(numberOfCities);
            Ants.Add(ant);
        }

        InitializePheromones();
    }

    private void InitializePheromones()
    {
        double initialPheromone = 1.0;
        for (int i = 0; i < NumberOfCities; i++)
            for (int j = 0; j < NumberOfCities; j++)
                PheromoneMatrix[i, j] = initialPheromone;
    }
}
