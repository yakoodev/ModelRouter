namespace MultiLlm.Core.Ops;

public sealed record LlmClientResilienceOptions
{
    public static LlmClientResilienceOptions Default { get; } = new();

    public int MaxRetries { get; init; } = 2;

    public TimeSpan InitialBackoff { get; init; } = TimeSpan.FromMilliseconds(200);

    public TimeSpan MaxBackoff { get; init; } = TimeSpan.FromSeconds(2);

    public bool UseJitter { get; init; } = true;

    public TimeSpan? RequestTimeout { get; init; }

    public int MaxConcurrentRequests { get; init; } = int.MaxValue;

    public TimeSpan? MinDelayBetweenRequests { get; init; }

    public Func<Exception, bool> ShouldRetry { get; init; } = static exception =>
        exception is TimeoutException
            or HttpRequestException
            or IOException
            || (exception is TaskCanceledException taskCanceled && !taskCanceled.CancellationToken.IsCancellationRequested);
}
