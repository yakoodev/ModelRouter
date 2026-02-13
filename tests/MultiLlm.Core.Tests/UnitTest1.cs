using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Contracts;
using MultiLlm.Core.Instructions;

namespace MultiLlm.Core.Tests;

public class InstructionLayersTests
{
    [Fact]
    public void OrderedByPriority_ReturnsRequestFirst()
    {
        var layers = new InstructionLayers(
            System: "system",
            Developer: "developer",
            Session: "session",
            Request: "request");

        var ordered = layers.OrderedByPriority().ToArray();

        Assert.Equal(["request", "session", "developer", "system"], ordered);
    }
}

public class LlmClientTests
{
    [Fact]
    public async Task ChatAsync_RoutesByProviderPrefix_AndInjectsPipelineIds()
    {
        var openAiProvider = new FakeProvider("openai");
        var ollamaProvider = new FakeProvider("ollama");
        var client = new LlmClient([openAiProvider, ollamaProvider]);

        var request = new ChatRequest(
            Model: "openai/gpt-4.1",
            Messages: [new Message(MessageRole.User, [new TextPart("hi")])]);

        var response = await client.ChatAsync(request);

        Assert.Equal("openai", response.ProviderId);
        Assert.Equal("gpt-4.1", response.Model);
        Assert.NotNull(response.RequestId);
        Assert.NotNull(response.CorrelationId);

        Assert.NotNull(openAiProvider.LastRequest);
        Assert.Equal("gpt-4.1", openAiProvider.LastRequest!.Model);
        Assert.Equal(response.RequestId, openAiProvider.LastRequest.RequestId);
        Assert.Equal(response.CorrelationId, openAiProvider.LastRequest.CorrelationId);

        Assert.Null(ollamaProvider.LastRequest);
    }

    [Fact]
    public async Task ChatAsync_ThrowsUnknownProvider_WhenProviderIsMissing()
    {
        var client = new LlmClient([new FakeProvider("openai")]);
        var request = new ChatRequest(
            Model: "missing/gpt-4.1",
            Messages: [new Message(MessageRole.User, [new TextPart("hi")])]);

        var exception = await Assert.ThrowsAsync<UnknownProviderException>(() => client.ChatAsync(request));

        Assert.Equal("missing", exception.ProviderId);
    }

    [Fact]
    public async Task ChatAsync_PropagatesCancellationToken()
    {
        var provider = new FakeProvider("openai");
        var client = new LlmClient([provider]);
        using var cts = new CancellationTokenSource();

        await client.ChatAsync(
            new ChatRequest(Model: "openai/gpt-4.1", Messages: [new Message(MessageRole.User, [new TextPart("hi")])]),
            cts.Token);

        Assert.Equal(cts.Token, provider.LastToken);
    }

    private sealed class FakeProvider(string providerId) : IModelProvider
    {
        public string ProviderId { get; } = providerId;

        public ProviderCapabilities Capabilities { get; } = new(true, true, true, true);

        public ChatRequest? LastRequest { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            LastToken = cancellationToken;

            return Task.FromResult(new ChatResponse(
                ProviderId,
                request.Model,
                new Message(MessageRole.Assistant, [new TextPart("ok")]),
                request.RequestId,
                request.CorrelationId));
        }

        public async IAsyncEnumerable<ChatDelta> ChatStreamAsync(ChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            LastToken = cancellationToken;

            yield return new ChatDelta(ProviderId, request.Model, "ok", true, request.RequestId, request.CorrelationId);
            await Task.CompletedTask;
        }
    }
}
