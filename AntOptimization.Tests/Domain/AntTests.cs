using AntOptimization.Domain.Algorithms;
using FluentAssertions;
using Xunit;

namespace AntOptimization.Tests.Domain;

public class AntTests
{
    [Fact]
    public void Initialize_SetsCorrectNumberOfCities()
    {
        var ant = new Ant();

        ant.Initialize(5);

        ant.Tour.Should().BeEmpty();
        ant.Visited.Should().HaveCount(5);
        ant.TourDistance.Should().Be(0);
    }

    [Fact]
    public void Initialize_AllVisitedAreFalse()
    {
        var ant = new Ant();

        ant.Initialize(4);

        ant.Visited.Should().NotContain(true);
    }

    [Fact]
    public void Reset_ClearsTourAndDistance()
    {
        var ant = new Ant();
        ant.Initialize(3);
        ant.Tour.Add(0);
        ant.Tour.Add(1);
        ant.Visited[0] = true;
        ant.TourDistance = 42.5;

        ant.Reset();

        ant.Tour.Should().BeEmpty();
        ant.TourDistance.Should().Be(0);
        ant.Visited.Should().NotContain(true);
    }
}
