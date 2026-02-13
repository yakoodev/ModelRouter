using MultiLlm.Core.Contracts;
using MultiLlm.Core.Instructions;

namespace MultiLlm.Core.Abstractions;

public sealed class LlmClient(IEnumerable<IModelProvider> providers) : ILlmClient
{
    private readonly IReadOnlyDictionary<string, IModelProvider> _providers = providers
        .ToDictionary(static provider => provider.ProviderId, StringComparer.OrdinalIgnoreCase);

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var routed = RouteRequest(request);
        var response = await routed.Provider.ChatAsync(routed.Request, cancellationToken).ConfigureAwait(false);

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

        await foreach (var delta in routed.Provider.ChatStreamAsync(routed.Request, cancellationToken).WithCancellation(cancellationToken))
        {
            yield return delta with
            {
                ProviderId = routed.Provider.ProviderId,
                Model = routed.Request.Model,
                RequestId = routed.Request.RequestId,
                CorrelationId = routed.Request.CorrelationId
            };
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
