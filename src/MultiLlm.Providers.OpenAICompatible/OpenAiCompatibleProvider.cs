using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Contracts;

namespace MultiLlm.Providers.OpenAICompatible;

public sealed class OpenAiCompatibleProvider : IModelProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly OpenAiCompatibleProviderOptions _options;

    public OpenAiCompatibleProvider(OpenAiCompatibleProviderOptions options, HttpClient? httpClient = null)
    {
        _options = options;
        _httpClient = httpClient ?? CreateDefaultHttpClient(options);
        _httpClient.Timeout = options.Timeout;
    }

    public string ProviderId => _options.ProviderId;

    public ProviderCapabilities Capabilities => new(true, true, true, true);

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var endpoint = BuildCompletionsUri();
        var payloadRequest = request with { Model = ResolveModel(request) };
        var payload = OpenAiCompatibleMapper.BuildChatPayload(payloadRequest, stream: false);

        using var httpRequest = CreateHttpRequestMessage(endpoint, payload);
        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken).ConfigureAwait(false);

        return OpenAiCompatibleMapper.ParseChatResponse(document.RootElement, ProviderId, payloadRequest.Model, request.RequestId, request.CorrelationId);
    }

    public async IAsyncEnumerable<ChatDelta> ChatStreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var endpoint = BuildCompletionsUri();
        var payloadRequest = request with { Model = ResolveModel(request) };
        var payload = OpenAiCompatibleMapper.BuildChatPayload(payloadRequest, stream: true);

        using var httpRequest = CreateHttpRequestMessage(endpoint, payload);
        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var payloadLine = line[5..].Trim();
            if (string.Equals(payloadLine, "[DONE]", StringComparison.Ordinal))
            {
                yield return new ChatDelta(ProviderId, payloadRequest.Model, string.Empty, IsFinal: true, request.RequestId, request.CorrelationId);
                yield break;
            }

            using var chunk = JsonDocument.Parse(payloadLine);
            var deltaText = OpenAiCompatibleMapper.ParseStreamDelta(chunk.RootElement);
            if (string.IsNullOrEmpty(deltaText))
            {
                continue;
            }

            yield return new ChatDelta(ProviderId, payloadRequest.Model, deltaText, IsFinal: false, request.RequestId, request.CorrelationId);
        }

        yield return new ChatDelta(ProviderId, payloadRequest.Model, string.Empty, IsFinal: true, request.RequestId, request.CorrelationId);
    }


    private static HttpClient CreateDefaultHttpClient(OpenAiCompatibleProviderOptions options)
    {
        var baseUri = new Uri(options.BaseUrl, UriKind.Absolute);
        if (baseUri.IsLoopback)
        {
            return new HttpClient(new HttpClientHandler { UseProxy = false });
        }

        return new HttpClient();
    }

    private HttpRequestMessage CreateHttpRequestMessage(Uri endpoint, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        foreach (var (name, value) in _options.Headers)
        {
            request.Headers.Remove(name);
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return request;
    }

    private Uri BuildCompletionsUri() => new(new Uri(_options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute), "chat/completions");

    private string ResolveModel(ChatRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Model))
        {
            return request.Model;
        }

        if (!string.IsNullOrWhiteSpace(_options.Model))
        {
            return _options.Model;
        }

        throw new InvalidOperationException("Model must be provided either in ChatRequest.Model or OpenAiCompatibleProviderOptions.Model.");
    }
}
