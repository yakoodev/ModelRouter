using MultiLlm.Core.Auth;

namespace MultiLlm.Providers.Codex;

public sealed class ExperimentalAuthBackend : ICodexAuthBackend
{
    public const string BackendIdValue = "experimental";

    public string BackendId => BackendIdValue;

    public ValueTask AuthenticateAsync(ITokenStore tokenStore, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Experimental adapters are extension points and must be provided by an external plugin.");
}
