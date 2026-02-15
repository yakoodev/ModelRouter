using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Contracts;
using MultiLlm.Providers.OpenAICompatible;

namespace MultiLlm.Providers.Ollama;

public sealed class OllamaOpenAiCompatProvider : IModelProvider
{
    private readonly OpenAiCompatibleProvider _innerProvider;

    public OllamaOpenAiCompatProvider(OllamaOpenAiCompatProviderOptions options, HttpClient? httpClient = null)
        : this(ToProviderOptions(options), httpClient)
    {
    }

    public OllamaOpenAiCompatProvider(OllamaProviderOptions options, HttpClient? httpClient = null)
    {
        _innerProvider = new OpenAiCompatibleProvider(new OpenAiCompatibleProviderOptions
        {
            ProviderId = options.ProviderId,
            BaseUrl = options.BaseUrl,
            Model = options.Model,
            Timeout = options.Timeout,
            Headers = options.Headers
        }, httpClient);
    }

    public string ProviderId => _innerProvider.ProviderId;

    public ProviderCapabilities Capabilities => _innerProvider.Capabilities;

    public Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
        _innerProvider.ChatAsync(request, cancellationToken);

    public IAsyncEnumerable<ChatDelta> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
        _innerProvider.ChatStreamAsync(request, cancellationToken);

    private static OllamaProviderOptions ToProviderOptions(OllamaOpenAiCompatProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new OllamaProviderOptions
        {
            BaseUrl = options.BaseUrl,
            Model = options.Model,
            Timeout = options.Timeout,
            Headers = options.Headers
        };
    }
}
