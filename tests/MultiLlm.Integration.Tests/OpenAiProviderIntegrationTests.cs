using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Contracts;
using MultiLlm.Providers.OpenAI;

namespace MultiLlm.Integration.Tests;

public class OpenAiProviderIntegrationTests
{
    [Fact]
    public async Task ChatAsync_WorksThroughLlmClient_WithOpenAiPrefixedModel()
    {
        using var server = new MockOpenAiSdkServer();
        server.Start();

        var provider = new OpenAiProvider(new OpenAiProviderOptions
        {
            ApiKey = "test-key",
            BaseUrl = server.BaseUri.ToString(),
            Timeout = TimeSpan.FromSeconds(10)
        });

        var client = new LlmClient([provider]);

        var request = new ChatRequest(
            Model: "openai/gpt-4.1-mini",
            Messages: [new Message(MessageRole.User, [new TextPart("hello")])],
            RequestId: "req-openai-1",
            CorrelationId: "corr-openai-1");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var response = await client.ChatAsync(request, cts.Token);

        Assert.Equal("openai", response.ProviderId);
        Assert.Equal("gpt-4.1-mini", response.Model);
        Assert.Equal("sdk-response", ((TextPart)response.Message.Parts[0]).Text);
        Assert.Equal("req-openai-1", response.RequestId);
        Assert.Equal("corr-openai-1", response.CorrelationId);
        Assert.NotNull(server.LastRequestJson);
    }

    [Fact]
    public async Task ChatStreamAsync_EmitsIncrementalDeltas_AndFinalMarker()
    {
        using var server = new MockOpenAiSdkServer();
        server.Start();

        var provider = new OpenAiProvider(new OpenAiProviderOptions
        {
            ApiKey = "test-key",
            BaseUrl = server.BaseUri.ToString(),
            Timeout = TimeSpan.FromSeconds(10)
        });

        var request = new ChatRequest(
            Model: "gpt-4.1-mini",
            Messages: [new Message(MessageRole.User, [new TextPart("stream")])],
            RequestId: "req-openai-2",
            CorrelationId: "corr-openai-2");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var deltas = await provider.ChatStreamAsync(request, cts.Token).ToListAsync();

        Assert.True(deltas.Count >= 3);
        Assert.Equal("hel", deltas[0].Delta);
        Assert.Equal("lo", deltas[1].Delta);
        Assert.False(deltas[0].IsFinal);
        Assert.True(deltas[^1].IsFinal);
        Assert.Equal("req-openai-2", deltas[^1].RequestId);
        Assert.Equal("corr-openai-2", deltas[^1].CorrelationId);
    }
}
