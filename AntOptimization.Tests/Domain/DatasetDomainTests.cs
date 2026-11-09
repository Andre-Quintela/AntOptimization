using AntOptimization.Domain.Dataset;
using AntOptimization.Domain.Models;
using FluentAssertions;
using Xunit;

namespace AntOptimization.Tests.Domain;

public class DatasetDomainTests
{
    private static readonly GeoBounds BeloHorizonte = new(-19.95, -43.97, -19.90, -43.92);

    [Fact]
    public void RandomPointGenerator_IsDeterministicAndInsideRegion()
    {
        var a = RandomPointGenerator.Generate(BeloHorizonte, 12, seed: 99);
        var b = RandomPointGenerator.Generate(BeloHorizonte, 12, seed: 99);
        var c = RandomPointGenerator.Generate(BeloHorizonte, 12, seed: 100);

        a.Should().HaveCount(12);
        a.Select(p => (p.Lat, p.Lng)).Should().Equal(b.Select(p => (p.Lat, p.Lng)));
        a.Select(p => (p.Lat, p.Lng)).Should().NotEqual(c.Select(p => (p.Lat, p.Lng)));
        a.Should().OnlyContain(p => BeloHorizonte.Contains(p.Lat, p.Lng));
        a.Select(p => (p.Lat, p.Lng)).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void RandomPointGenerator_InvalidRegion_Throws()
    {
        var act = () => RandomPointGenerator.Generate(new GeoBounds(10, 10, 5, 5), 3, 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Haversine_KnownDistance()
    {
        // One degree of latitude ≈ 111.19 km.
        HaversineDistance.Meters(0, 0, 1, 0).Should().BeApproximately(111_195, 50);

        var matrix = HaversineDistance.Matrix([new Location { Lat = 0, Lng = 0 }, new Location { Lat = 0, Lng = 1 }]);
        matrix[0, 1].Should().Be(matrix[1, 0]);
        matrix[0, 0].Should().Be(0);
    }

    [Fact]
    public void HyperparameterGrid_ExpandsCartesianProductWithStableKeys()
    {
        var grid = new HyperparameterGrid
        {
            Alpha = [1, 2],
            Beta = [2, 2, 5], // duplicate removed
            NumberOfAnts = [10],
            EvaporationRate = [0.5],
            Iterations = [50, 100]
        };

        var configurations = grid.Expand().ToList();

        grid.ConfigurationCount.Should().Be(8);
        configurations.Should().HaveCount(8);
        configurations.Select(c => c.Key).Should().OnlyHaveUniqueItems();
        configurations[0].Key.Should().Be("a1_b2_m10_r0.5_i50");
    }

    [Fact]
    public void HyperparameterGrid_Validate_RejectsOutOfRangeValues()
    {
        var grid = new HyperparameterGrid { EvaporationRate = [0, 1.2], NumberOfAnts = [], Alpha = [-1] };

        grid.Validate().Should().HaveCount(3);
        new HyperparameterGrid().Validate().Should().BeEmpty();
    }

    [Fact]
    public void SeedDerivation_IsDeterministicAndDistinct()
    {
        SeedDerivation.ForInstance(1, 0).Should().Be(SeedDerivation.ForInstance(1, 0));
        SeedDerivation.ForInstance(1, 0).Should().NotBe(SeedDerivation.ForInstance(1, 1));
        SeedDerivation.ForInstance(1, 0, attempt: 1).Should().NotBe(SeedDerivation.ForInstance(1, 0));
        SeedDerivation.ForRun(5, "a1", 0).Should().NotBe(SeedDerivation.ForRun(5, "a1", 1));
        SeedDerivation.ForRun(5, "a1", 0).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void FeatureExtractor_ComputesExpectedValues()
    {
        var points = new List<Location>
        {
            new() { Lat = -19.95, Lng = -43.97 },
            new() { Lat = -19.95, Lng = -43.92 },
            new() { Lat = -19.90, Lng = -43.92 },
            new() { Lat = -19.90, Lng = -43.97 }
        };
        var matrix = HaversineDistance.Matrix(points);

        var features = InstanceFeatureExtractor.Extract(points, matrix, BeloHorizonte, fixedStartCity: 0);

        features.PointCount.Should().Be(4);
        features.RegionAreaKm2.Should().BeApproximately(BeloHorizonte.AreaKm2, 1e-9);
        features.AsymmetryIndex.Should().Be(0);
        features.DetourIndex.Should().BeApproximately(1, 1e-9);
        features.CostMinKm.Should().BeLessThanOrEqualTo(features.CostMedianKm);
        features.CostMedianKm.Should().BeLessThanOrEqualTo(features.CostMaxKm);
        features.TwoOptDistanceM.Should().BeLessThanOrEqualTo(features.NearestNeighborDistanceM + 1e-9);

        InstanceFeatureExtractor.ApplyOptimum(features, features.TwoOptDistanceM);
        features.TwoOptGapPercent.Should().Be(0);
    }
}
