using MultiLlm.Core.Contracts;
using MultiLlm.Core.Events;
using MultiLlm.Core.Instructions;

namespace MultiLlm.Core.Abstractions;

public sealed class LlmClient(IEnumerable<IModelProvider> providers, IEnumerable<ILlmEventHook>? hooks = null) : ILlmClient
{
    private readonly IReadOnlyDictionary<string, IModelProvider> _providers = providers
        .ToDictionary(static provider => provider.ProviderId, StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyList<ILlmEventHook> _hooks = (hooks ?? []).ToArray();

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var routed = RouteRequest(request);
        await NotifyStartAsync(routed.Request, cancellationToken).ConfigureAwait(false);

        ChatResponse response;
        try
        {
            response = await routed.Provider.ChatAsync(routed.Request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await NotifyErrorAsync(routed.Request, exception, cancellationToken).ConfigureAwait(false);
            throw;
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
        await NotifyStartAsync(routed.Request, cancellationToken).ConfigureAwait(false);

        var stream = routed.Provider.ChatStreamAsync(routed.Request, cancellationToken).GetAsyncEnumerator(cancellationToken);

        try
        {
            while (true)
            {
                ChatDelta delta;
                try
                {
                    if (!await stream.MoveNextAsync().ConfigureAwait(false))
                    {
                        break;
                    }

                    delta = stream.Current;
                }
                catch (Exception exception)
                {
                    await NotifyErrorAsync(routed.Request, exception, cancellationToken).ConfigureAwait(false);
                    throw;
                }

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
            await stream.DisposeAsync().ConfigureAwait(false);
        }

        await NotifyEndAsync(routed.Request, cancellationToken).ConfigureAwait(false);
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
        foreach (var hook in _hooks)
        {
            await hook.OnErrorAsync(request.RequestId!, request.CorrelationId, exception, cancellationToken).ConfigureAwait(false);
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
