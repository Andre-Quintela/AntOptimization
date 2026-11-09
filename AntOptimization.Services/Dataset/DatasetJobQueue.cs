using System.Collections.Concurrent;
using System.Threading.Channels;
using AntOptimization.Domain.Interfaces;

namespace AntOptimization.Services.Dataset;

/// <summary>Singleton in-process queue consumed by the background worker.</summary>
public class DatasetJobQueue : IDatasetJobQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _running = new();

    public ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(jobId, cancellationToken);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    public CancellationToken BeginRun(Guid jobId, CancellationToken stoppingToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _running[jobId] = cts;
        return cts.Token;
    }

    public void EndRun(Guid jobId)
    {
        if (_running.TryRemove(jobId, out var cts))
            cts.Dispose();
    }

    public bool RequestCancel(Guid jobId)
    {
        if (!_running.TryGetValue(jobId, out var cts))
            return false;
        cts.Cancel();
        return true;
    }

    public bool IsRunning(Guid jobId) => _running.ContainsKey(jobId);
}
