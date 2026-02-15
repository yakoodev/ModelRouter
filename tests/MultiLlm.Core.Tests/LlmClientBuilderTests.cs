using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Contracts;
using MultiLlm.Providers.Codex;
using MultiLlm.Providers.OpenAICompatible;

namespace MultiLlm.Core.Tests;

public class LlmClientBuilderTests
{
    [Fact]
    public void Build_Throws_WhenNoProvidersConfigured()
    {
        var builder = new LlmClientBuilder();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Equal("At least one provider must be configured before Build().", exception.Message);
    }

    [Fact]
    public void Configure_CodexOptions_AddsCodexProvider()
    {
        var builder = new LlmClientBuilder()
            .Configure(new CodexProviderOptions
            {
                ProviderId = "codex",
                UseChatGptBackend = true
            });

        Assert.Contains(builder.Providers, provider => provider.ProviderId == "codex");
    }

    [Fact]
    public void Configure_OpenAiCompatibleOptions_AddsProvider()
    {
        var builder = new LlmClientBuilder()
            .Configure(new OpenAiCompatibleProviderOptions
            {
                ProviderId = "openai-compatible",
                BaseUrl = "https://api.openai.com/v1",
                Model = "gpt-5-mini"
            });

        Assert.Contains(builder.Providers, provider => provider.ProviderId == "openai-compatible");
    }

    [Fact]
    public async Task Build_CreatesWorkingClient_ForConfiguredProvider()
    {
        var builder = new LlmClientBuilder()
            .Configure(new FakeProvider("demo"));

        var client = builder.Build();
        var response = await client.ChatAsync(new ChatRequest(
            Model: "demo/test-model",
            Messages: [new Message(MessageRole.User, [new TextPart("hi")])]));

        Assert.Equal("demo", response.ProviderId);
        Assert.Equal("test-model", response.Model);
    }

    private sealed class FakeProvider(string providerId) : IModelProvider
    {
        public string ProviderId { get; } = providerId;

        public ProviderCapabilities Capabilities { get; } = new(true, true, true, true);

        public Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatResponse(
                ProviderId,
                request.Model,
                new Message(MessageRole.Assistant, [new TextPart("ok")]),
                request.RequestId,
                request.CorrelationId));
        }

        public async IAsyncEnumerable<ChatDelta> ChatStreamAsync(
            ChatRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatDelta(ProviderId, request.Model, "ok", true, request.RequestId, request.CorrelationId);
            await Task.CompletedTask;
        }
    }
}
