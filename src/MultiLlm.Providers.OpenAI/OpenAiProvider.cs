using System.ClientModel;
using System.Runtime.CompilerServices;
using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Contracts;
using OpenAI;
using OpenAI.Chat;

namespace MultiLlm.Providers.OpenAI;

public sealed class OpenAiProvider : IModelProvider
{
    private readonly OpenAiProviderOptions _options;
    private readonly OpenAIClient _openAiClient;

    public OpenAiProvider(OpenAiProviderOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("OpenAiProviderOptions.ApiKey must be provided.");
        }

        var clientOptions = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            clientOptions.Endpoint = new Uri(_options.BaseUrl, UriKind.Absolute);
        }

        var credential = new ApiKeyCredential(_options.ApiKey);
        _openAiClient = new OpenAIClient(credential, clientOptions);
    }

    internal OpenAiProvider(OpenAiProviderOptions options, OpenAIClient openAiClient)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _openAiClient = openAiClient ?? throw new ArgumentNullException(nameof(openAiClient));
    }

    public string ProviderId => _options.ProviderId;

    public ProviderCapabilities Capabilities => new(true, true, true, true);

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var model = ResolveModel(request.Model, _options.Model);
        var messages = OpenAiSdkMessageMapper.MapMessages(request.Messages);
        var chatClient = _openAiClient.GetChatClient(model);
        using var requestCts = CreateRequestCancellationSource(cancellationToken);
        var completion = await chatClient.CompleteChatAsync(messages, cancellationToken: requestCts.Token).ConfigureAwait(false);
        var text = OpenAiSdkMessageMapper.ToText(completion.Value.Content);

        return new ChatResponse(
            ProviderId: ProviderId,
            Model: model,
            Message: new Message(MessageRole.Assistant, [new TextPart(text)]),
            RequestId: request.RequestId,
            CorrelationId: request.CorrelationId,
            Usage: OpenAiSdkMessageMapper.MapUsage(completion.Value.Usage));
    }

    public async IAsyncEnumerable<ChatDelta> ChatStreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var model = ResolveModel(request.Model, _options.Model);
        var messages = OpenAiSdkMessageMapper.MapMessages(request.Messages);
        var chatClient = _openAiClient.GetChatClient(model);
        using var requestCts = CreateRequestCancellationSource(cancellationToken);

        await foreach (var update in chatClient.CompleteChatStreamingAsync(messages, cancellationToken: requestCts.Token).ConfigureAwait(false))
        {
            var deltaText = OpenAiSdkMessageMapper.ToText(update.ContentUpdate);
            if (string.IsNullOrEmpty(deltaText))
            {
                continue;
            }

            yield return new ChatDelta(ProviderId, model, deltaText, IsFinal: false, request.RequestId, request.CorrelationId);
        }

        yield return new ChatDelta(ProviderId, model, string.Empty, IsFinal: true, request.RequestId, request.CorrelationId);
    }

    private CancellationTokenSource CreateRequestCancellationSource(CancellationToken cancellationToken)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(_options.Timeout);
        return linkedCts;
    }

    private static string ResolveModel(string requestModel, string? fallbackModel)
    {
        if (!string.IsNullOrWhiteSpace(requestModel))
        {
            return requestModel;
        }

        if (!string.IsNullOrWhiteSpace(fallbackModel))
        {
            return fallbackModel;
        }

        throw new InvalidOperationException("Model must be provided either in ChatRequest.Model or OpenAiProviderOptions.Model.");
    }
}
