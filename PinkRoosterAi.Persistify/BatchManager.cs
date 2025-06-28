using Microsoft.Extensions.Logging;
using PinkRoosterAi.Persistify.Abstractions;
using Timer = System.Timers.Timer;

namespace PinkRoosterAi.Persistify;

/// <summary>
/// Manages batch operations and timing for persistent dictionaries.
/// Handles automatic flushing based on batch size and interval thresholds.
/// </summary>
internal class BatchManager : IDisposable
{
    private readonly object _batchLock = new object();
    private readonly ILogger? _logger;
    private readonly Func<Task> _flushCallback;
    private Timer? _batchTimer;
    private int _pendingCount;
    private bool _disposed;

    public BatchManager(IPersistenceOptions? options, Func<Task> flushCallback, ILogger? logger = null)
    {
        _flushCallback = flushCallback ?? throw new ArgumentNullException(nameof(flushCallback));
        _logger = logger;
        
        if (options?.BatchInterval > TimeSpan.Zero)
        {
            _batchTimer = new Timer(options.BatchInterval.TotalMilliseconds)
            {
                AutoReset = false
            };
            _batchTimer.Elapsed += async (_, __) => await HandleTimerElapsed().ConfigureAwait(false);
        }
    }

    public IPersistenceOptions? Options { get; }

    public void TrackMutation(IPersistenceOptions? options)
    {
        if (options == null)
            return;

        lock (_batchLock)
        {
            var wasEmpty = _pendingCount == 0;
            _pendingCount++;
            
            if (_pendingCount >= options.BatchSize)
            {
                _batchTimer?.Stop();
                var pendingCount = _pendingCount;
                _pendingCount = 0;
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _flushCallback().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Exception during background flush in BatchManager. PendingCount was: {PendingCount}", pendingCount);
                    }
                });
            }
            else if (wasEmpty)
            {
                _batchTimer?.Stop();
                _batchTimer?.Start();
            }
        }
    }

    public void ClearPendingCount()
    {
        lock (_batchLock)
        {
            _pendingCount = 0;
        }
    }

    public int GetPendingCount()
    {
        lock (_batchLock)
        {
            return _pendingCount;
        }
    }

    private async Task HandleTimerElapsed()
    {
        try
        {
            ClearPendingCount();
            await _flushCallback().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception during timer-triggered flush in BatchManager.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _batchTimer?.Stop();
        _batchTimer?.Dispose();
        _batchTimer = null;
        _disposed = true;
    }
}