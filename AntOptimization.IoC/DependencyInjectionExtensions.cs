using AntOptimization.Domain.Dataset;
using AntOptimization.Domain.Interfaces;
using AntOptimization.Infrastructure.Persistence;
using AntOptimization.Infrastructure.Services;
using AntOptimization.Services;
using AntOptimization.Services.Dataset;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AntOptimization.IoC;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddProjectDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IDistanceMatrixService, DistanceMatrixService>(client =>
        {
            client.BaseAddress = new Uri("https://router.project-osrm.org/");
            // The public OSRM demo server rejects requests without a User-Agent (HTTP 403).
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AntOptimization/1.0 (TCC route optimization)");
        });

        services.AddScoped<IAntColonyOptimizationService, AntColonyOptimizationService>();
        services.AddScoped<INearestNeighborService, NearestNeighborService>();
        services.AddScoped<ITwoOptService, TwoOptService>();
        services.AddScoped<IGeneticAlgorithmService, GeneticAlgorithmService>();
        services.AddScoped<IRouteService, RouteService>();
        services.AddScoped<ICompareRouteService, CompareRouteService>();

        services.AddDatasetGeneration(configuration);

        return services;
    }

    private static void AddDatasetGeneration(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(DatasetOptions.SectionName).Get<DatasetOptions>() ?? new DatasetOptions();
        services.AddSingleton(options);

        services.AddDbContext<AntOptimizationDbContext>(db =>
            db.UseSqlite(ResolveConnectionString(configuration)));

        services.AddSingleton<IDatasetJobQueue, DatasetJobQueue>();
        services.AddScoped<IDatasetRepository, DatasetRepository>();
        services.AddScoped<IDatasetCostMatrixProvider, DatasetCostMatrixProvider>();
        services.AddScoped<IDatasetJobRunner, DatasetJobRunner>();
        services.AddScoped<IDatasetGenerationService, DatasetGenerationService>();
        services.AddScoped<IDatasetExportService, DatasetExportService>();
    }

    /// <summary>
    /// Uses "ConnectionStrings:Dataset" when configured. Otherwise stores the SQLite file under
    /// %HOME%/data on Azure App Service (persistent storage) or App_Data locally.
    /// </summary>
    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("Dataset");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        bool onAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
        string directory = onAzure
            ? Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? AppContext.BaseDirectory, "data")
            : Path.Combine(AppContext.BaseDirectory, "App_Data");

        Directory.CreateDirectory(directory);
        return $"Data Source={Path.Combine(directory, "antoptimization.db")}";
    }

    /// <summary>Applies pending migrations and flags jobs interrupted by the previous shutdown.</summary>
    public static async Task InitializeDatasetStorageAsync(this IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AntOptimizationDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<IDatasetRepository>().MarkRunningJobsInterruptedAsync();
    }
}
