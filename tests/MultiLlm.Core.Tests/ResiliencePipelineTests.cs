using System.Diagnostics;
using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Contracts;
using MultiLlm.Core.Events;
using MultiLlm.Core.Ops;

namespace MultiLlm.Core.Tests;

public class ResiliencePipelineTests
{
    [Fact]
    public async Task ChatAsync_RetriesTransientFailure_AndEventuallySucceeds()
    {
        var provider = new FaultInjectionProvider("openai")
        {
            ChatFailuresBeforeSuccess = 2,
            ChatFailureFactory = static _ => new HttpRequestException("temporary")
        };

        var client = new LlmClient(
            [provider],
            resilienceOptions: new LlmClientResilienceOptions
            {
                MaxRetries = 3,
                InitialBackoff = TimeSpan.Zero,
                MaxBackoff = TimeSpan.Zero,
                UseJitter = false
            });

        var response = await client.ChatAsync(BuildRequest());

        Assert.Equal("ok", ((TextPart)response.Message.Parts.Single()).Text);
        Assert.Equal(3, provider.ChatAttempts);
    }

    [Fact]
    public async Task ChatAsync_TimeoutsWhenProviderIsTooSlow()
    {
        var provider = new FaultInjectionProvider("openai")
        {
            ChatDelay = TimeSpan.FromMilliseconds(250)
        };

        var client = new LlmClient(
            [provider],
            resilienceOptions: new LlmClientResilienceOptions
            {
                MaxRetries = 0,
                RequestTimeout = TimeSpan.FromMilliseconds(50)
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ChatAsync(BuildRequest()));
    }

    [Fact]
    public async Task ChatAsync_AppliesRateLimitDelayBetweenRequests()
    {
        var provider = new FaultInjectionProvider("openai");
        var client = new LlmClient(
            [provider],
            resilienceOptions: new LlmClientResilienceOptions
            {
                MaxRetries = 0,
                MinDelayBetweenRequests = TimeSpan.FromMilliseconds(120)
            });

        var stopwatch = Stopwatch.StartNew();
        await client.ChatAsync(BuildRequest());
        await client.ChatAsync(BuildRequest());
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(115), $"Elapsed: {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task ChatAsync_RedactsSecretsBeforeSendingToHooks()
    {
        var provider = new FaultInjectionProvider("openai")
        {
            ChatFailuresBeforeSuccess = 1,
            ChatFailureFactory = static _ => new InvalidOperationException("authorization: bearer my-secret-token")
        };

        var hook = new RecordingHook();
        var client = new LlmClient(
            [provider],
            [hook],
            new LlmClientResilienceOptions { MaxRetries = 0 });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.ChatAsync(BuildRequest()));
        Assert.Contains("my-secret-token", exception.Message);

        var errorEvent = Assert.Single(hook.Events, static e => e.Name == "error");
        Assert.DoesNotContain("my-secret-token", errorEvent.Error!.Message);
        Assert.Contains("[REDACTED]", errorEvent.Error.Message);
    }

    [Fact]
    public async Task ChatStreamAsync_RetriesTransientFailureBeforeFirstDelta()
    {
        var provider = new FaultInjectionProvider("openai")
        {
            StreamFailuresBeforeSuccess = 1,
            StreamFailureFactory = static _ => new HttpRequestException("stream temporary")
        };

        var client = new LlmClient(
            [provider],
            resilienceOptions: new LlmClientResilienceOptions
            {
                MaxRetries = 2,
                InitialBackoff = TimeSpan.Zero,
                MaxBackoff = TimeSpan.Zero,
                UseJitter = false
            });

        var deltas = await client.ChatStreamAsync(BuildRequest()).ToListAsync();

        Assert.Single(deltas);
        Assert.Equal(2, provider.StreamAttempts);
    }

    private static ChatRequest BuildRequest() =>
        new(
            Model: "openai/gpt-4.1",
            Messages: [new Message(MessageRole.User, [new TextPart("hi")])]);

    private sealed class RecordingHook : ILlmEventHook
    {
        public List<HookEvent> Events { get; } = [];

        public ValueTask OnStartAsync(string requestId, string? correlationId, CancellationToken cancellationToken = default)
        {
            Events.Add(new HookEvent("start", null));
            return ValueTask.CompletedTask;
        }

        public ValueTask OnEndAsync(string requestId, string? correlationId, CancellationToken cancellationToken = default)
        {
            Events.Add(new HookEvent("end", null));
            return ValueTask.CompletedTask;
        }

        public ValueTask OnErrorAsync(string requestId, string? correlationId, Exception exception, CancellationToken cancellationToken = default)
        {
            Events.Add(new HookEvent("error", exception));
            return ValueTask.CompletedTask;
        }
    }

    private sealed record HookEvent(string Name, Exception? Error);

    private sealed class FaultInjectionProvider(string providerId) : IModelProvider
    {
        public string ProviderId { get; } = providerId;

        public ProviderCapabilities Capabilities { get; } = new(true, true, true, true);

        public int ChatFailuresBeforeSuccess { get; init; }

        public int StreamFailuresBeforeSuccess { get; init; }

        public TimeSpan ChatDelay { get; init; }

        public Func<int, Exception>? ChatFailureFactory { get; init; }

        public Func<int, Exception>? StreamFailureFactory { get; init; }

        public int ChatAttempts { get; private set; }

        public int StreamAttempts { get; private set; }

        public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
        {
            ChatAttempts++;

            if (ChatDelay > TimeSpan.Zero)
            {
                await Task.Delay(ChatDelay, cancellationToken);
            }

            if (ChatAttempts <= ChatFailuresBeforeSuccess)
            {
                throw ChatFailureFactory?.Invoke(ChatAttempts) ?? new HttpRequestException("fault");
            }

            return new ChatResponse(
                ProviderId,
                request.Model,
                new Message(MessageRole.Assistant, [new TextPart("ok")]),
                request.RequestId,
                request.CorrelationId);
        }

        public async IAsyncEnumerable<ChatDelta> ChatStreamAsync(ChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            StreamAttempts++;

            if (StreamAttempts <= StreamFailuresBeforeSuccess)
            {
                throw StreamFailureFactory?.Invoke(StreamAttempts) ?? new HttpRequestException("stream-fault");
            }

            yield return new ChatDelta(ProviderId, request.Model, "ok", true, request.RequestId, request.CorrelationId);
            await Task.CompletedTask;
        }
    }
}
