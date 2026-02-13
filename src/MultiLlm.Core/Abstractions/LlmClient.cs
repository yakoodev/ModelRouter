using MultiLlm.Core.Contracts;
using MultiLlm.Core.Events;
using MultiLlm.Core.Instructions;
using MultiLlm.Core.Ops;

namespace MultiLlm.Core.Abstractions;

public sealed class LlmClient : ILlmClient
{
    private readonly IReadOnlyDictionary<string, IModelProvider> _providers;
    private readonly IReadOnlyList<ILlmEventHook> _hooks;
    private readonly LlmClientResilienceOptions _resilience;
    private readonly ISecretRedactor _secretRedactor;
    private readonly SemaphoreSlim _concurrencyGate;
    private readonly SemaphoreSlim _rateLock = new(1, 1);
    private DateTimeOffset _nextAllowedRequestAt = DateTimeOffset.MinValue;

    public LlmClient(
        IEnumerable<IModelProvider> providers,
        IEnumerable<ILlmEventHook>? hooks = null,
        LlmClientResilienceOptions? resilienceOptions = null,
        ISecretRedactor? secretRedactor = null)
    {
        _providers = providers.ToDictionary(static provider => provider.ProviderId, StringComparer.OrdinalIgnoreCase);
        _hooks = (hooks ?? []).ToArray();
        _resilience = resilienceOptions ?? LlmClientResilienceOptions.Default;
        _secretRedactor = secretRedactor ?? new SecretRedactor();

        var maxConcurrent = Math.Max(1, _resilience.MaxConcurrentRequests);
        _concurrencyGate = new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var routed = RouteRequest(request);
        await WaitForRateLimitAsync(cancellationToken).ConfigureAwait(false);
        await NotifyStartAsync(routed.Request, cancellationToken).ConfigureAwait(false);

        ChatResponse response;
        try
        {
            response = await ExecuteWithRetryAsync(
                    ct => routed.Provider.ChatAsync(routed.Request, ct),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await NotifyErrorAsync(routed.Request, exception, cancellationToken).ConfigureAwait(false);
            throw;
        }
        finally
        {
            _concurrencyGate.Release();
        }

        await NotifyEndAsync(routed.Request, cancellationToken).ConfigureAwait(false);

        return response with
        {
            ProviderId = routed.Provider.ProviderId,
            Model = routed.Request.Model,
            RequestId = routed.Request.RequestId,
            CorrelationId = routed.Request.CorrelationId
        };
    }

    public async IAsyncEnumerable<ChatDelta> ChatStreamAsync(
        ChatRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var routed = RouteRequest(request);
        await WaitForRateLimitAsync(cancellationToken).ConfigureAwait(false);
        await NotifyStartAsync(routed.Request, cancellationToken).ConfigureAwait(false);

        IAsyncEnumerator<ChatDelta>? stream = null;

        try
        {
            try
            {
                stream = await StartStreamWithRetryAsync(routed, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                await NotifyErrorAsync(routed.Request, exception, cancellationToken).ConfigureAwait(false);
                throw;
            }

            while (true)
            {
                bool moved;
                try
                {
                    moved = await stream.MoveNextAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    await NotifyErrorAsync(routed.Request, exception, cancellationToken).ConfigureAwait(false);
                    throw;
                }

                if (!moved)
                {
                    break;
                }

                var delta = stream.Current;
                yield return delta with
                {
                    ProviderId = routed.Provider.ProviderId,
                    Model = routed.Request.Model,
                    RequestId = routed.Request.RequestId,
                    CorrelationId = routed.Request.CorrelationId
                };
            }
        }
        finally
        {
            if (stream is not null)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }

            _concurrencyGate.Release();
        }

        await NotifyEndAsync(routed.Request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IAsyncEnumerator<ChatDelta>> StartStreamWithRetryAsync(RoutedRequest routed, CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (var attempt = 0; attempt <= _resilience.MaxRetries; attempt++)
        {
            IAsyncEnumerator<ChatDelta>? stream = null;
            using var linkedCts = CreateAttemptToken(cancellationToken);
            var attemptToken = linkedCts?.Token ?? cancellationToken;

            try
            {
                stream = routed.Provider.ChatStreamAsync(routed.Request, attemptToken).GetAsyncEnumerator(attemptToken);
                if (!await stream.MoveNextAsync().ConfigureAwait(false))
                {
                    return EmptyStream();
                }

                return Prepend(stream.Current, stream);
            }
            catch (Exception exception) when (attempt < _resilience.MaxRetries && CanRetry(exception))
            {
                lastError = exception;
                if (stream is not null)
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                }

                var delay = GetBackoff(attempt);
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        throw lastError ?? new InvalidOperationException("Unable to start stream after retries.");
    }

    private static IAsyncEnumerator<ChatDelta> EmptyStream() => AsyncEnumerable.Empty<ChatDelta>().GetAsyncEnumerator();

    private static IAsyncEnumerator<ChatDelta> Prepend(ChatDelta first, IAsyncEnumerator<ChatDelta> remainder)
        => PrependIterator(first, remainder).GetAsyncEnumerator();

    private static async IAsyncEnumerable<ChatDelta> PrependIterator(
        ChatDelta first,
        IAsyncEnumerator<ChatDelta> remainder,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return first;

        try
        {
            while (await remainder.MoveNextAsync().ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return remainder.Current;
            }
        }
        finally
        {
            await remainder.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (var attempt = 0; attempt <= _resilience.MaxRetries; attempt++)
        {
            using var linkedCts = CreateAttemptToken(cancellationToken);
            var attemptToken = linkedCts?.Token ?? cancellationToken;

            try
            {
                return await operation(attemptToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (attempt < _resilience.MaxRetries && CanRetry(exception))
            {
                lastError = exception;
                var delay = GetBackoff(attempt);
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        throw lastError ?? new InvalidOperationException("Resilience pipeline failed without exception.");
    }

    private bool CanRetry(Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            return false;
        }

        return _resilience.ShouldRetry(exception);
    }

    private TimeSpan GetBackoff(int attempt)
    {
        var baseDelay = TimeSpan.FromMilliseconds(_resilience.InitialBackoff.TotalMilliseconds * Math.Pow(2, attempt));
        var capped = baseDelay <= _resilience.MaxBackoff ? baseDelay : _resilience.MaxBackoff;

        if (!_resilience.UseJitter)
        {
            return capped;
        }

        var jitter = Random.Shared.NextDouble() * 0.25 + 0.875;
        return TimeSpan.FromMilliseconds(capped.TotalMilliseconds * jitter);
    }

    private CancellationTokenSource? CreateAttemptToken(CancellationToken cancellationToken)
    {
        if (_resilience.RequestTimeout is not { } requestTimeout)
        {
            return null;
        }

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(requestTimeout);
        return linkedCts;
    }

    private async Task WaitForRateLimitAsync(CancellationToken cancellationToken)
    {
        await _concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        if (_resilience.MinDelayBetweenRequests is not { } minDelay || minDelay <= TimeSpan.Zero)
        {
            return;
        }

        await _rateLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var now = DateTimeOffset.UtcNow;
            if (_nextAllowedRequestAt > now)
            {
                await Task.Delay(_nextAllowedRequestAt - now, cancellationToken).ConfigureAwait(false);
            }

            _nextAllowedRequestAt = DateTimeOffset.UtcNow + minDelay;
        }
        finally
        {
            _rateLock.Release();
        }
    }

    private async ValueTask NotifyStartAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        foreach (var hook in _hooks)
        {
            await hook.OnStartAsync(request.RequestId!, request.CorrelationId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask NotifyEndAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        foreach (var hook in _hooks)
        {
            await hook.OnEndAsync(request.RequestId!, request.CorrelationId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask NotifyErrorAsync(ChatRequest request, Exception exception, CancellationToken cancellationToken)
    {
        var sanitized = _secretRedactor.Redact(exception);

        foreach (var hook in _hooks)
        {
            await hook.OnErrorAsync(request.RequestId!, request.CorrelationId, sanitized, cancellationToken).ConfigureAwait(false);
        }
    }

    private RoutedRequest RouteRequest(ChatRequest request)
    {
        var separatorIndex = request.Model.IndexOf('/');
        if (separatorIndex <= 0 || separatorIndex == request.Model.Length - 1)
        {
            throw new ArgumentException("Model must be in 'providerId/model' format.", nameof(request));
        }

        var providerId = request.Model[..separatorIndex];
        var modelId = request.Model[(separatorIndex + 1)..];

        if (!_providers.TryGetValue(providerId, out var provider))
        {
            throw new UnknownProviderException(providerId);
        }

        var pipelineRequest = request with
        {
            Model = modelId,
            RequestId = request.RequestId ?? Guid.NewGuid().ToString("N"),
            CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString("N")
        };

        pipelineRequest = InstructionNormalizer.Normalize(pipelineRequest);

        return new RoutedRequest(provider, pipelineRequest);
    }

    private sealed record RoutedRequest(IModelProvider Provider, ChatRequest Request);
}
