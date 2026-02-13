using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Contracts;

namespace MultiLlm.Providers.OpenAICompatible;

public sealed class OpenAiCompatibleProvider : IModelProvider
{
    public string ProviderId => "openai-compatible";

    public ProviderCapabilities Capabilities => new(true, true, true, true);

    public Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("OpenAI-compatible implementation will support vLLM/SGLang endpoints.");

    public IAsyncEnumerable<ChatDelta> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Streaming implementation is not yet wired.");
}
