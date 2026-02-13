using MultiLlm.Core.Auth;

namespace MultiLlm.Providers.Codex;

public sealed class OfficialDeviceCodeBackend : ICodexAuthBackend
{
    public string BackendId => "official-device-code";

    public ValueTask AuthenticateAsync(ITokenStore tokenStore, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Official device-code flow will be implemented against public Codex auth docs.");
}
