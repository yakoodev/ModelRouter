using System.Text.Json;
using MultiLlm.Core.Auth;
using MultiLlm.Providers.Codex;

namespace MultiLlm.Core.Tests;

public class OfficialDeviceCodeBackendTests
{
    [Fact]
    public async Task AuthenticateAsync_UsesOpenAiApiKey_WhenItExists()
    {
        var codexHome = Directory.CreateTempSubdirectory("codex-home-");
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(codexHome.FullName, "auth.json"),
                JsonSerializer.Serialize(new { OPENAI_API_KEY = "sk-test" }));

            var options = new CodexProviderOptions { CodexHome = codexHome.FullName };
            var backend = new OfficialDeviceCodeBackend(options);
            var store = new InMemoryTokenStore();

            await backend.AuthenticateAsync(store);

            var token = await store.GetAsync(OfficialDeviceCodeBackend.DeviceTokenStoreKey);
            Assert.NotNull(token);
            Assert.Equal("sk-test", token!.AccessToken);
        }
        finally
        {
            codexHome.Delete(true);
        }
    }

    [Fact]
    public async Task AuthenticateAsync_FallsBackToDeviceAccessToken_WhenApiKeyMissing()
    {
        var codexHome = Directory.CreateTempSubdirectory("codex-home-");
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(codexHome.FullName, "auth.json"),
                JsonSerializer.Serialize(new
                {
                    auth_mode = "chatgpt",
                    tokens = new { access_token = "device-access", refresh_token = "device-refresh" }
                }));

            var options = new CodexProviderOptions { CodexHome = codexHome.FullName };
            var backend = new OfficialDeviceCodeBackend(options);
            var store = new InMemoryTokenStore();

            await backend.AuthenticateAsync(store);

            var token = await store.GetAsync(OfficialDeviceCodeBackend.DeviceTokenStoreKey);
            Assert.NotNull(token);
            Assert.Equal("device-access", token!.AccessToken);
            Assert.Equal("device-refresh", token.RefreshToken);
        }
        finally
        {
            codexHome.Delete(true);
        }
    }
}
