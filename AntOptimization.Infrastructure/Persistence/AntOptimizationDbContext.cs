using AntOptimization.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AntOptimization.Infrastructure.Persistence;

public class AntOptimizationDbContext(DbContextOptions<AntOptimizationDbContext> options) : DbContext(options)
{
    public DbSet<DatasetJob> DatasetJobs => Set<DatasetJob>();
    public DbSet<DatasetInstance> DatasetInstances => Set<DatasetInstance>();
    public DbSet<AcoRun> AcoRuns => Set<AcoRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DatasetJob>(job =>
        {
            job.ToTable("DatasetJobs");
            job.HasKey(j => j.Id);
            job.Property(j => j.Name).HasMaxLength(200);
            job.Property(j => j.Status).HasConversion<string>().HasMaxLength(32);
            job.Property(j => j.DistanceSource).HasConversion<string>().HasMaxLength(16);
            job.Ignore(j => j.IsActive);
            job.HasIndex(j => j.CreatedAt);
            job.HasMany(j => j.Instances)
                .WithOne(i => i.Job)
                .HasForeignKey(i => i.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DatasetInstance>(instance =>
        {
            instance.ToTable("DatasetInstances");
            instance.HasKey(i => i.Id);
            instance.Property(i => i.Status).HasConversion<string>().HasMaxLength(32);
            instance.Property(i => i.ExactMethod).HasConversion<string>().HasMaxLength(16);
            instance.HasIndex(i => new { i.JobId, i.Index }).IsUnique();
            instance.OwnsOne(i => i.Features, features => features.ToTable("DatasetInstances"));
            instance.Navigation(i => i.Features).IsRequired(false);
            instance.HasMany(i => i.Runs)
                .WithOne(r => r.Instance)
                .HasForeignKey(r => r.InstanceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AcoRun>(run =>
        {
            run.ToTable("AcoRuns");
            run.HasKey(r => r.Id);
            run.Property(r => r.ConfigKey).HasMaxLength(64).IsRequired();
            run.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
            run.HasIndex(r => r.InstanceId);
            run.HasIndex(r => r.ConfigKey);
        });
    }
}
