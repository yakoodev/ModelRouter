using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Contracts;
using MultiLlm.Providers.Ollama;
using MultiLlm.Providers.OpenAICompatible;

namespace MultiLlm.Integration.Tests;

public class CrossProviderChatRequestIntegrationTests
{
    public static TheoryData<string, Func<Uri, IModelProvider>> Providers => new()
    {
        {
            "openai-compatible",
            baseUri => new OpenAiCompatibleProvider(new OpenAiCompatibleProviderOptions
            {
                BaseUrl = baseUri.ToString(),
                Timeout = TimeSpan.FromSeconds(10)
            })
        },
        {
            "ollama-openai-compat",
            baseUri => new OllamaOpenAiCompatProvider(new OllamaOpenAiCompatProviderOptions
            {
                BaseUrl = baseUri.ToString(),
                Timeout = TimeSpan.FromSeconds(10)
            })
        }
    };

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task ChatAsync_CommonRequest_WorksAcrossProviders(string expectedProviderId, Func<Uri, IModelProvider> providerFactory)
    {
        using var server = new MockOpenAiCompatibleServer();
        server.Start();

        var provider = providerFactory(server.BaseUri);
        var request = BuildCommonRequest();

        var response = await provider.ChatAsync(request);

        Assert.Equal(expectedProviderId, response.ProviderId);
        Assert.Equal("assistant-response", ((TextPart)response.Message.Parts[0]).Text);
        Assert.Equal("cross-provider-model", server.LastRequestJson!.RootElement.GetProperty("model").GetString());
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task ChatStreamAsync_CommonRequest_SupportsStreamingAcrossProviders(string expectedProviderId, Func<Uri, IModelProvider> providerFactory)
    {
        using var server = new MockOpenAiCompatibleServer();
        server.Start();

        var provider = providerFactory(server.BaseUri);
        var request = BuildCommonRequest();

        var deltas = await provider.ChatStreamAsync(request).ToListAsync();

        Assert.Equal(expectedProviderId, deltas[0].ProviderId);
        Assert.Equal("hel", deltas[0].Delta);
        Assert.Equal("lo", deltas[1].Delta);
        Assert.True(deltas[^1].IsFinal);
    }

    private static ChatRequest BuildCommonRequest() => new(
        Model: "cross-provider-model",
        Messages:
        [
            new Message(MessageRole.System, [new TextPart("You are helpful")]),
            new Message(MessageRole.User, [new TextPart("Привет")])
        ],
        RequestId: "req-cross",
        CorrelationId: "corr-cross");
}
