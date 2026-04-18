using AntOptimization.Domain.Interfaces;
using AntOptimization.Infrastructure.Persistence;
using AntOptimization.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AntOptimization.IoC;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not found in configuration.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IOptimizationRunRepository, OptimizationRunRepository>();
        services.AddScoped<ICompareRunRepository, CompareRunRepository>();

        return services;
    }
}
