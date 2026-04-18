namespace AntOptimization.Domain.Entities;

public class CompareRun
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int LocationCount { get; set; }
    public int? StartLocationIndex { get; set; }
    public ICollection<CompareRunResult> Results { get; set; } = [];
}
