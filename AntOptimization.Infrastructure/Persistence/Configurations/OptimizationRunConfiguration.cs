using AntOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntOptimization.Infrastructure.Persistence.Configurations;

public class OptimizationRunConfiguration : IEntityTypeConfiguration<OptimizationRun>
{
    public void Configure(EntityTypeBuilder<OptimizationRun> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.BestRouteOrderJson).HasColumnType("TEXT").IsRequired();
        builder.Property(x => x.RouteCoordinatesJson).HasColumnType("TEXT").IsRequired();
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
