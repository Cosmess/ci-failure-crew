using CiFailureCrew.Core.Orchestration;

namespace CiFailureCrew.Tests;

public class RetryPolicyTests
{
    [Fact]
    public async Task Times_out_a_slow_attempt_and_retries()
    {
        var calls = 0;
        var policy = new RetryPolicy(maxRetries: 1, timeout: TimeSpan.FromMilliseconds(50), backoff: _ => TimeSpan.Zero);
        Exception? retryError = null;

        var result = await policy.ExecuteAsync(
            async token =>
            {
                if (++calls == 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), token);
                }

                return "ok";
            },
            (_, ex) => retryError = ex);

        Assert.Equal("ok", result);
        Assert.IsType<TimeoutException>(retryError);
    }

    [Fact]
    public async Task Gives_up_after_the_last_retry()
    {
        var calls = 0;
        var policy = new RetryPolicy(maxRetries: 2, timeout: TimeSpan.FromSeconds(5), backoff: _ => TimeSpan.Zero);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            policy.ExecuteAsync<string>(_ => { calls++; throw new InvalidOperationException(); }));

        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task Does_not_retry_when_the_caller_cancels()
    {
        var calls = 0;
        using var cts = new CancellationTokenSource();
        var policy = new RetryPolicy(maxRetries: 3, timeout: TimeSpan.FromSeconds(5), backoff: _ => TimeSpan.Zero);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            policy.ExecuteAsync<string>(async token =>
            {
                calls++;
                await cts.CancelAsync();
                token.ThrowIfCancellationRequested();
                return "nunca";
            }, cancellationToken: cts.Token));

        Assert.Equal(1, calls);
    }
}
