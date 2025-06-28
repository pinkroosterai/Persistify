using Microsoft.Extensions.Logging;
using PinkRoosterAi.Persistify.Abstractions;
using PinkRoosterAi.Persistify.Events;
using Polly;
using Polly.Retry;

namespace PinkRoosterAi.Persistify;

/// <summary>
/// Manages retry logic with exponential back-off and jitter for persistence operations.
/// Provides centralized error handling and retry policies.
/// </summary>
internal class RetryManager
{
    private readonly ILogger? _logger;

    public RetryManager(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Executes the given operation with retry logic using Polly, with exponential back-off and jitter.
    /// </summary>
    /// <param name="operation">The asynchronous operation to execute.</param>
    /// <param name="operationName">A name for logging and error events.</param>
    /// <param name="options">Persistence options containing retry configuration.</param>
    /// <param name="onError">Optional callback for persistence errors.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// Returns null if the operation succeeded, or the last exception if all attempts failed and
    /// ThrowOnPersistenceFailure is false.
    /// </returns>
    /// <exception cref="Exception">
    /// Throws if ThrowOnPersistenceFailure is true and all attempts fail.
    /// </exception>
    public async Task<Exception?> ExecuteWithRetryAsync(
        Func<Task> operation, 
        string operationName,
        IPersistenceOptions? options = null,
        EventHandler<PersistenceErrorEventArgs>? onError = null,
        CancellationToken cancellationToken = default)
    {
        int maxAttempts = options?.MaxRetryAttempts ?? 3;
        TimeSpan baseDelay = options?.RetryDelay ?? TimeSpan.FromMilliseconds(100);
        bool throwOnFailure = options?.ThrowOnPersistenceFailure ?? false;

        Exception? lastException = null;
        Random jitterer = new Random();

        AsyncRetryPolicy policy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                maxAttempts,
                attempt => CalculateRetryDelay(attempt, baseDelay, jitterer),
                (exception, timespan, attempt, context) =>
                {
                    lastException = exception;
                    _logger?.LogError(exception,
                        "Persistence operation {Operation} failed on attempt {Attempt}. Retrying in {Delay}ms.",
                        operationName, attempt, timespan.TotalMilliseconds);
                    
                    onError?.Invoke(this, 
                        new PersistenceErrorEventArgs(exception, operationName, attempt, attempt == maxAttempts));
                    
                    return Task.CompletedTask;
                }
            );

        try
        {
            await policy.ExecuteAsync(ct => operation(), cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            if (throwOnFailure)
                throw;

            return lastException ?? ex;
        }
    }

    private static TimeSpan CalculateRetryDelay(int attempt, TimeSpan baseDelay, Random jitterer)
    {
        // Exponential backoff with jitter
        double exp = baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        double capped = Math.Min(exp, 60_000); // cap at 1 minute
        double jitterMax = Math.Min(100, baseDelay.TotalMilliseconds / 2.0);
        double jitter = jitterer.NextDouble() * jitterMax;
        return TimeSpan.FromMilliseconds(capped + jitter);
    }
}