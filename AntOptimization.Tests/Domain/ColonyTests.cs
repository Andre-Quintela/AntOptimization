using AntOptimization.Domain.Algorithms;
using FluentAssertions;
using Xunit;

namespace AntOptimization.Tests.Domain;

public class ColonyTests
{
    [Fact]
    public void Constructor_CreatesCorrectNumberOfAnts()
    {
        var colony = new Colony(numberOfAnts: 5, numberOfCities: 3);

        colony.Ants.Should().HaveCount(5);
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(5, 4)]
    [InlineData(1, 6)]
    public void Constructor_InitializesPheromoneMatrixToOne(int numberOfAnts, int numberOfCities)
    {
        var colony = new Colony(numberOfAnts, numberOfCities);

        for (int i = 0; i < numberOfCities; i++)
            for (int j = 0; j < numberOfCities; j++)
                colony.PheromoneMatrix[i, j].Should().Be(1.0);
    }

    [Fact]
    public void Constructor_InitializesAntsWithCorrectCityCount()
    {
        var colony = new Colony(numberOfAnts: 3, numberOfCities: 4);

        foreach (var ant in colony.Ants)
        {
            ant.Visited.Should().HaveCount(4);
            ant.Tour.Should().BeEmpty();
        }
    }

    [Fact]
    public void PheromoneMatrix_HasCorrectDimensions()
    {
        var colony = new Colony(numberOfAnts: 2, numberOfCities: 5);

        colony.PheromoneMatrix.GetLength(0).Should().Be(5);
        colony.PheromoneMatrix.GetLength(1).Should().Be(5);
    }
}
