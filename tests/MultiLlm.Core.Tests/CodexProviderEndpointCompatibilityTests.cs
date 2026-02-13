using MultiLlm.Core.Auth;
using MultiLlm.Core.Contracts;
using MultiLlm.Providers.Codex;

namespace MultiLlm.Core.Tests;

public class CodexProviderEndpointCompatibilityTests
{
    [Fact]
    public async Task ChatAsync_ThrowsFriendlyError_ForDeviceCodeTokenOnApiOpenAiEndpoint()
    {
        var options = new CodexProviderOptions
        {
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-5-mini"
        };

        var tokenStore = new InMemoryTokenStore(
            [new KeyValuePair<string, AuthToken>(
                OfficialDeviceCodeBackend.DeviceTokenStoreKey,
                new AuthToken("access-token", null, "refresh-token"))]);

        var provider = new CodexProvider(options, [new NoOpOfficialBackend()], tokenStore);

        var request = new ChatRequest(
            Model: "gpt-5-mini",
            Messages: [new Message(MessageRole.User, [new TextPart("hello")])]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ChatAsync(request));
        Assert.Contains("API key", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class NoOpOfficialBackend : ICodexAuthBackend
    {
        public string BackendId => OfficialDeviceCodeBackend.BackendIdValue;

        public ValueTask AuthenticateAsync(ITokenStore tokenStore, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}
