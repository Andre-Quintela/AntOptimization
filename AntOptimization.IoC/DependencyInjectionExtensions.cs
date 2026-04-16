using AntOptimization.Domain.Interfaces;
using AntOptimization.Infrastructure.Services;
using AntOptimization.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AntOptimization.IoC;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddProjectDependencies(this IServiceCollection services)
    {
        services.AddHttpClient<IDistanceMatrixService, DistanceMatrixService>(client =>
        {
            client.BaseAddress = new Uri("https://router.project-osrm.org/");
        });

        services.AddScoped<IAntColonyOptimizationService, AntColonyOptimizationService>();
        services.AddScoped<INearestNeighborService, NearestNeighborService>();
        services.AddScoped<ITwoOptService, TwoOptService>();
        services.AddScoped<IGeneticAlgorithmService, GeneticAlgorithmService>();
        services.AddScoped<IRouteService, RouteService>();
        services.AddScoped<ICompareRouteService, CompareRouteService>();

        return services;
    }
}
