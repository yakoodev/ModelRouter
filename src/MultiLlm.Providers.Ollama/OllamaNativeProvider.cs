using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Contracts;

namespace MultiLlm.Providers.Ollama;

public sealed class OllamaNativeProvider : IModelProvider
{
    public string ProviderId => "ollama-native";

    public ProviderCapabilities Capabilities => new(true, true, true, true);

    public Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Ollama native API adapter is not implemented yet.");

    public IAsyncEnumerable<ChatDelta> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Ollama native streaming API adapter is not implemented yet.");
}
