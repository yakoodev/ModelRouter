using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Contracts;

namespace MultiLlm.Providers.Ollama;

public sealed class OllamaOpenAiCompatProvider : IModelProvider
{
    public string ProviderId => "ollama-openai-compat";

    public ProviderCapabilities Capabilities => new(true, true, true, true);

    public Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Ollama OpenAI-compatible adapter is not implemented yet.");

    public IAsyncEnumerable<ChatDelta> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Ollama OpenAI-compatible streaming is not implemented yet.");
}
