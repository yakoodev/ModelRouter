using MultiLlm.Core.Auth;

namespace MultiLlm.Providers.Codex;

public interface ICodexAuthBackend
{
    string BackendId { get; }

    ValueTask AuthenticateAsync(ITokenStore tokenStore, CancellationToken cancellationToken = default);
}
