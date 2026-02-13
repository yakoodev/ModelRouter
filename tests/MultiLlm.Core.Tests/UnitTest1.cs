using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Contracts;
using MultiLlm.Core.Events;
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

    [Fact]
    public void Normalize_TrimValues_AndDropWhitespaceOnlyLayers()
    {
        var layers = new InstructionLayers(
            System: "  system  ",
            Developer: "   ",
            Session: "\n session \n",
            Request: "  request\t");

        var normalized = layers.Normalize();

        Assert.Equal("system", normalized.System);
        Assert.Null(normalized.Developer);
        Assert.Equal("session", normalized.Session);
        Assert.Equal("request", normalized.Request);
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

    [Fact]
    public async Task ChatAsync_NormalizesInstructionsInCore_AndProducesDeterministicMessages()
    {
        var provider = new FakeProvider("openai");
        var client = new LlmClient([provider]);

        var request = new ChatRequest(
            Model: "openai/gpt-4.1",
            Messages: [new Message(MessageRole.User, [new TextPart("user")])],
            Instructions: new InstructionLayers(
                System: " system ",
                Developer: " developer ",
                Session: " session ",
                Request: " request "));

        await client.ChatAsync(request);

        Assert.NotNull(provider.LastRequest);
        var lastRequest = provider.LastRequest!;

        Assert.Equal("request", lastRequest.Instructions!.Request);
        Assert.Equal("session", lastRequest.Instructions.Session);
        Assert.Equal("developer", lastRequest.Instructions.Developer);
        Assert.Equal("system", lastRequest.Instructions.System);

        Assert.Collection(
            lastRequest.Messages,
            message => AssertMessage(message, MessageRole.Developer, "[request]\nrequest"),
            message => AssertMessage(message, MessageRole.Developer, "[session]\nsession"),
            message => AssertMessage(message, MessageRole.Developer, "[developer]\ndeveloper"),
            message => AssertMessage(message, MessageRole.System, "[system]\nsystem"),
            message => AssertMessage(message, MessageRole.User, "user"));
    }


    [Fact]
    public async Task ChatAsync_CallsHooksInOrder_OnSuccess()
    {
        var provider = new FakeProvider("openai");
        var hook = new RecordingHook();
        var client = new LlmClient([provider], [hook]);

        var response = await client.ChatAsync(new ChatRequest(
            Model: "openai/gpt-4.1",
            Messages: [new Message(MessageRole.User, [new TextPart("hi")])]));

        Assert.Collection(
            hook.Events,
            ev =>
            {
                Assert.Equal("start", ev.Name);
                Assert.Equal(response.RequestId, ev.RequestId);
                Assert.Equal(response.CorrelationId, ev.CorrelationId);
                Assert.Null(ev.Error);
            },
            ev =>
            {
                Assert.Equal("end", ev.Name);
                Assert.Equal(response.RequestId, ev.RequestId);
                Assert.Equal(response.CorrelationId, ev.CorrelationId);
                Assert.Null(ev.Error);
            });
    }

    [Fact]
    public async Task ChatAsync_CallsErrorHookWithOriginalException_OnFailure()
    {
        var expectedException = new InvalidOperationException("boom");
        var provider = new FakeProvider("openai") { ChatException = expectedException };
        var hook = new RecordingHook();
        var client = new LlmClient([provider], [hook]);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => client.ChatAsync(new ChatRequest(
            Model: "openai/gpt-4.1",
            Messages: [new Message(MessageRole.User, [new TextPart("hi")])])));

        Assert.Same(expectedException, thrown);

        Assert.Collection(
            hook.Events,
            ev =>
            {
                Assert.Equal("start", ev.Name);
                Assert.Null(ev.Error);
            },
            ev =>
            {
                Assert.Equal("error", ev.Name);
                Assert.Same(expectedException, ev.Error);
            });
    }

    [Fact]
    public async Task ChatStreamAsync_CallsHooksInOrder_OnSuccess()
    {
        var provider = new FakeProvider("openai");
        var hook = new RecordingHook();
        var client = new LlmClient([provider], [hook]);

        var deltas = await client.ChatStreamAsync(new ChatRequest(
            Model: "openai/gpt-4.1",
            Messages: [new Message(MessageRole.User, [new TextPart("hi")])])).ToListAsync();

        Assert.Single(deltas);

        Assert.Collection(
            hook.Events,
            ev =>
            {
                Assert.Equal("start", ev.Name);
                Assert.Null(ev.Error);
            },
            ev =>
            {
                Assert.Equal("end", ev.Name);
                Assert.Null(ev.Error);
            });
    }

    [Fact]
    public async Task ChatStreamAsync_CallsErrorHookWithOriginalException_OnFailure()
    {
        var expectedException = new InvalidOperationException("stream-boom");
        var provider = new FakeProvider("openai") { StreamException = expectedException };
        var hook = new RecordingHook();
        var client = new LlmClient([provider], [hook]);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in client.ChatStreamAsync(new ChatRequest(
                               Model: "openai/gpt-4.1",
                               Messages: [new Message(MessageRole.User, [new TextPart("hi")])])))
            {
            }
        });

        Assert.Same(expectedException, thrown);

        Assert.Collection(
            hook.Events,
            ev =>
            {
                Assert.Equal("start", ev.Name);
                Assert.Null(ev.Error);
            },
            ev =>
            {
                Assert.Equal("error", ev.Name);
                Assert.Same(expectedException, ev.Error);
            });
    }

    private static void AssertMessage(Message message, MessageRole expectedRole, string expectedText)
    {
        Assert.Equal(expectedRole, message.Role);
        var textPart = Assert.IsType<TextPart>(Assert.Single(message.Parts));
        Assert.Equal(expectedText, textPart.Text);
    }

    private sealed class FakeProvider(string providerId) : IModelProvider
    {
        public Exception? ChatException { get; init; }

        public Exception? StreamException { get; init; }
        public string ProviderId { get; } = providerId;

        public ProviderCapabilities Capabilities { get; } = new(true, true, true, true);

        public ChatRequest? LastRequest { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            LastToken = cancellationToken;

            if (ChatException is not null)
            {
                throw ChatException;
            }

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

            if (StreamException is not null)
            {
                throw StreamException;
            }

            yield return new ChatDelta(ProviderId, request.Model, "ok", true, request.RequestId, request.CorrelationId);
            await Task.CompletedTask;
        }
    }

    private sealed class RecordingHook : ILlmEventHook
    {
        public List<HookEvent> Events { get; } = [];

        public ValueTask OnStartAsync(string requestId, string? correlationId, CancellationToken cancellationToken = default)
        {
            Events.Add(new HookEvent("start", requestId, correlationId, null));
            return ValueTask.CompletedTask;
        }

        public ValueTask OnEndAsync(string requestId, string? correlationId, CancellationToken cancellationToken = default)
        {
            Events.Add(new HookEvent("end", requestId, correlationId, null));
            return ValueTask.CompletedTask;
        }

        public ValueTask OnErrorAsync(string requestId, string? correlationId, Exception exception, CancellationToken cancellationToken = default)
        {
            Events.Add(new HookEvent("error", requestId, correlationId, exception));
            return ValueTask.CompletedTask;
        }
    }

    private sealed record HookEvent(string Name, string RequestId, string? CorrelationId, Exception? Error);
}
