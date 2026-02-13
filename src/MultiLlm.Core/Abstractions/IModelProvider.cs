using MultiLlm.Core.Contracts;

namespace MultiLlm.Core.Abstractions;

public interface IModelProvider
{
    string ProviderId { get; }
    ProviderCapabilities Capabilities { get; }

    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatDelta> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken = default);
}
