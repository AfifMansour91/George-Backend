using System.Threading.Channels;
using George.DB;
using Microsoft.Extensions.Logging;

namespace George.Services;

/// <summary>
/// In-memory, non-blocking queue for <see cref="IntegrationLog"/> rows. Producers (order/sync code) enqueue
/// and return immediately; a single background writer drains + batch-inserts them. Bounded with DropWrite so
/// a flood of logs can never grow memory unbounded or block the operation being logged (logs are best-effort).
/// </summary>
public interface IIntegrationLogQueue
{
    /// <summary>Enqueue a row without blocking. Returns false if the row was dropped (queue saturated).</summary>
    bool TryEnqueue(IntegrationLog row);

    ChannelReader<IntegrationLog> Reader { get; }
}

public sealed class IntegrationLogQueue : IIntegrationLogQueue
{
    // ~10k rows of headroom; under a sustained flood the oldest-unwritten are dropped rather than blocking.
    private const int Capacity = 10_000;

    private readonly Channel<IntegrationLog> _channel = Channel.CreateBounded<IntegrationLog>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });

    private readonly ILogger<IntegrationLogQueue> _logger;
    private long _enqueueFailures;

    public IntegrationLogQueue(ILogger<IntegrationLogQueue> logger)
    {
        _logger = logger;
    }

    public ChannelReader<IntegrationLog> Reader => _channel.Reader;

    public bool TryEnqueue(IntegrationLog row)
    {
        if (row == null) return false;
        if (_channel.Writer.TryWrite(row)) return true;

        var n = System.Threading.Interlocked.Increment(ref _enqueueFailures);
        if (n % 100 == 1)
            _logger.LogWarning("IntegrationLog queue saturated; dropped {Count} log rows so far.", n);
        return false;
    }
}
