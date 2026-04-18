using AntOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntOptimization.Infrastructure.Persistence.Configurations;

public class CompareRunResultConfiguration : IEntityTypeConfiguration<CompareRunResult>
{
    public void Configure(EntityTypeBuilder<CompareRunResult> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Algorithm).HasMaxLength(100).IsRequired();
        builder.Property(x => x.BestRouteOrderJson).HasColumnType("TEXT").IsRequired();
        builder.Property(x => x.RouteCoordinatesJson).HasColumnType("TEXT").IsRequired();
        builder.HasIndex(x => x.CompareRunId);
    }
}
