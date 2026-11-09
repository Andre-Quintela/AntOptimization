using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AntOptimization.Infrastructure.Persistence;

/// <summary>Used only by the EF Core CLI (dotnet ef migrations ...).</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AntOptimizationDbContext>
{
    public AntOptimizationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AntOptimizationDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new AntOptimizationDbContext(options);
    }
}
