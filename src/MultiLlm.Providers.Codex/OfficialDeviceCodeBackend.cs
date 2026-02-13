using System.Text.Json;
using System.Text.Json.Serialization;
using MultiLlm.Core.Auth;

namespace MultiLlm.Providers.Codex;

public sealed class OfficialDeviceCodeBackend(CodexProviderOptions options) : ICodexAuthBackend
{
    public const string BackendIdValue = "official-device-code";
    public const string DeviceTokenStoreKey = "codex.device_code.access_token";

    public string BackendId => BackendIdValue;

    public async ValueTask AuthenticateAsync(ITokenStore tokenStore, CancellationToken cancellationToken = default)
    {
        var existingToken = await tokenStore.GetAsync(DeviceTokenStoreKey, cancellationToken);
        if (existingToken is not null)
        {
            return;
        }

        var authFilePath = ResolveAuthFilePath(options.CodexHome);
        if (!File.Exists(authFilePath))
        {
            throw new InvalidOperationException(
                $"Codex auth file was not found: {authFilePath}. Run 'codex login --device-auth' (ChatGPT tokens) or configure API key mode.");
        }

        var authJson = await LoadAuthFileAsync(authFilePath, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(authJson.OpenAiApiKey))
        {
            await tokenStore.SetAsync(DeviceTokenStoreKey, new AuthToken(authJson.OpenAiApiKey), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(authJson.Tokens?.AccessToken))
        {
            await tokenStore.SetAsync(DeviceTokenStoreKey, new AuthToken(authJson.Tokens.AccessToken, null, authJson.Tokens.RefreshToken), cancellationToken).ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException(
            $"Codex auth file {authFilePath} does not contain OPENAI_API_KEY or tokens.access_token. Re-login with codex CLI.");
    }

    private static async Task<CodexAuthFile> LoadAuthFileAsync(string authFilePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(authFilePath);
        var auth = await JsonSerializer.DeserializeAsync<CodexAuthFile>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return auth ?? new CodexAuthFile();
    }

    private static string ResolveAuthFilePath(string? codexHome)
    {
        var home = codexHome;
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetEnvironmentVariable("CODEX_HOME");
        }

        if (string.IsNullOrWhiteSpace(home))
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            home = Path.Combine(userProfile, ".codex");
        }

        return Path.Combine(home, "auth.json");
    }

    private sealed class CodexAuthFile
    {
        [JsonPropertyName("OPENAI_API_KEY")]
        public string? OpenAiApiKey { get; init; }

        [JsonPropertyName("tokens")]
        public CodexTokens? Tokens { get; init; }
    }

    private sealed class CodexTokens
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }
    }
}
