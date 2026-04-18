using AntOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AntOptimization.Infrastructure.Persistence.Configurations;

public class CompareRunConfiguration : IEntityTypeConfiguration<CompareRun>
{
    public void Configure(EntityTypeBuilder<CompareRun> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.HasMany(x => x.Results)
               .WithOne(x => x.CompareRun)
               .HasForeignKey(x => x.CompareRunId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
