using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Contracts;

namespace MultiLlm.Providers.Codex;

public sealed class CodexProvider(CodexProviderOptions options, IEnumerable<ICodexAuthBackend> backends) : IModelProvider
{
    public string ProviderId => "codex-dev-only";

    public ProviderCapabilities Capabilities => new(true, true, true, true);

    public Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Codex dev-only provider is scaffolded and awaits auth + chat implementation.");

    public IAsyncEnumerable<ChatDelta> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Codex stream implementation is not wired yet.");
}
