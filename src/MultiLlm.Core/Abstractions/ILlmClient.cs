using MultiLlm.Core.Contracts;

namespace MultiLlm.Core.Abstractions;

public interface ILlmClient
{
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatDelta> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken = default);
}
