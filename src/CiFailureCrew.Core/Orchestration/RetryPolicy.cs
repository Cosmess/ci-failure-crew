using System.Globalization;

namespace CiFailureCrew.Core.Orchestration;

/// <summary>Runs an operation with a per-attempt timeout and exponential backoff between retries.</summary>
public sealed class RetryPolicy(int maxRetries, TimeSpan timeout, Func<int, TimeSpan>? backoff = null)
{
    private readonly Func<int, TimeSpan> _backoff = backoff ?? (attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Action<int, Exception>? onRetry = null,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attemptCts.CancelAfter(timeout);

            try
            {
                return await operation(attemptCts.Token);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                var error = ex is OperationCanceledException
                    ? new TimeoutException($"Timed out after {timeout.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture)}s.", ex)
                    : ex;

                if (attempt > maxRetries)
                {
                    throw error == ex ? ex : error;
                }

                onRetry?.Invoke(attempt, error);
                await Task.Delay(_backoff(attempt), cancellationToken);
            }
        }
    }
}
