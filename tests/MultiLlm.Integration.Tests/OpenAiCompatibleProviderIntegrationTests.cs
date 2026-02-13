using MultiLlm.Core.Contracts;
using MultiLlm.Providers.OpenAICompatible;

namespace MultiLlm.Integration.Tests;

public class OpenAiCompatibleProviderIntegrationTests
{
    [Fact]
    public async Task ChatAsync_SendsOpenAiPayload_AndReadsResponse()
    {
        using var server = new MockOpenAiCompatibleServer();
        server.Start();

        var provider = new OpenAiCompatibleProvider(new OpenAiCompatibleProviderOptions
        {
            BaseUrl = server.BaseUri.ToString(),
            Headers = new Dictionary<string, string> { ["X-Test-Key"] = "secret" },
            Timeout = TimeSpan.FromSeconds(10)
        });

        var request = new ChatRequest(
            Model: "gpt-4.1-mini",
            Messages: [new Message(MessageRole.User, [new TextPart("Привет")])],
            RequestId: "req-1",
            CorrelationId: "corr-1");

        var response = await provider.ChatAsync(request);

        Assert.Equal("assistant-response", ((TextPart)response.Message.Parts[0]).Text);
        Assert.NotNull(server.LastRequestJson);
        Assert.Equal("gpt-4.1-mini", server.LastRequestJson!.RootElement.GetProperty("model").GetString());
        Assert.Equal("Привет", server.LastRequestJson.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
        Assert.Equal("secret", server.LastRequestHeaders["X-Test-Key"]);
    }

    [Fact]
    public async Task ChatStreamAsync_ParsesSseChunks_AndYieldsFinalDelta()
    {
        using var server = new MockOpenAiCompatibleServer();
        server.Start();

        var provider = new OpenAiCompatibleProvider(new OpenAiCompatibleProviderOptions
        {
            BaseUrl = server.BaseUri.ToString(),
            Timeout = TimeSpan.FromSeconds(10)
        });

        var request = new ChatRequest(
            Model: "gpt-4.1-mini",
            Messages: [new Message(MessageRole.User, [new TextPart("stream")])]);

        var deltas = await provider.ChatStreamAsync(request).ToListAsync();

        Assert.Equal(3, deltas.Count);
        Assert.Equal("hel", deltas[0].Delta);
        Assert.Equal("lo", deltas[1].Delta);
        Assert.False(deltas[0].IsFinal);
        Assert.True(deltas[^1].IsFinal);
    }
}
