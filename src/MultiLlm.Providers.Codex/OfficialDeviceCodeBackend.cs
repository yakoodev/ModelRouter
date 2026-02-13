using MultiLlm.Core.Auth;

namespace MultiLlm.Providers.Codex;

public sealed class OfficialDeviceCodeBackend : ICodexAuthBackend
{
    public const string BackendIdValue = "official-device-code";
    public const string DeviceTokenStoreKey = "codex.device_code.access_token";

    public string BackendId => BackendIdValue;

    public async ValueTask AuthenticateAsync(ITokenStore tokenStore, CancellationToken cancellationToken = default)
    {
        var token = await tokenStore.GetAsync(DeviceTokenStoreKey, cancellationToken);
        if (token is null)
        {
            throw new InvalidOperationException(
                "Codex device-code token is missing. Complete sign-in flow and persist token before chat requests.");
        }
    }
}
