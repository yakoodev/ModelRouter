using MultiLlm.Core.Auth;

namespace MultiLlm.Providers.Codex;

public sealed class ExperimentalAuthBackend : ICodexAuthBackend
{
    public string BackendId => "experimental";

    public ValueTask AuthenticateAsync(ITokenStore tokenStore, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Experimental adapters are extension points and disabled by default.");
}
