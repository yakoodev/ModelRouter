using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Contracts;

namespace MultiLlm.Providers.OpenAI;

public sealed class OpenAiProvider : IModelProvider
{
    public string ProviderId => "openai";

    public ProviderCapabilities Capabilities => new(true, true, true, true);

    public Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("OpenAI implementation will use openai-dotnet client.");

    public IAsyncEnumerable<ChatDelta> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("OpenAI streaming implementation is not yet wired.");
}
