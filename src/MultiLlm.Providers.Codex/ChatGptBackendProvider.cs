using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Contracts;

namespace MultiLlm.Providers.Codex;

internal sealed class ChatGptBackendProvider : IModelProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly CodexProviderOptions _options;
    private readonly string _accessToken;

    public ChatGptBackendProvider(CodexProviderOptions options, string accessToken, HttpClient? httpClient = null)
    {
        _options = options;
        _accessToken = accessToken;
        _httpClient = httpClient ?? CreateDefaultHttpClient(options);
        _httpClient.Timeout = options.Timeout;
    }

    public string ProviderId => _options.ProviderId;

    public ProviderCapabilities Capabilities => new(true, true, true, true);

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(request, stream: false);
        using var httpRequest = CreateHttpRequest(payload);
        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessOrThrowAsync(response, cancellationToken).ConfigureAwait(false);

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var text = ExtractResponseText(document.RootElement);

        return new ChatResponse(
            ProviderId,
            request.Model,
            new Message(MessageRole.Assistant, [new TextPart(text)]),
            request.RequestId,
            request.CorrelationId);
    }

    public async IAsyncEnumerable<ChatDelta> ChatStreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(request, stream: true);
        using var httpRequest = CreateHttpRequest(payload);
        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessOrThrowAsync(response, cancellationToken).ConfigureAwait(false);

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
                yield return new ChatDelta(ProviderId, request.Model, string.Empty, IsFinal: true, request.RequestId, request.CorrelationId);
                yield break;
            }

            using var chunk = JsonDocument.Parse(payloadLine);
            if (IsStreamCompleted(chunk.RootElement))
            {
                yield return new ChatDelta(ProviderId, request.Model, string.Empty, IsFinal: true, request.RequestId, request.CorrelationId);
                yield break;
            }

            var delta = ExtractStreamDelta(chunk.RootElement);
            if (string.IsNullOrWhiteSpace(delta))
            {
                continue;
            }

            yield return new ChatDelta(ProviderId, request.Model, delta, IsFinal: false, request.RequestId, request.CorrelationId);
        }

        yield return new ChatDelta(ProviderId, request.Model, string.Empty, IsFinal: true, request.RequestId, request.CorrelationId);
    }

    private object BuildPayload(ChatRequest request, bool stream)
    {
        var input = request.Messages
            .Select(static message => new
            {
                type = "message",
                role = MapRole(message.Role),
                content = message.Parts
                    .OfType<TextPart>()
                    .Select(part => new { type = ResolveContentType(message.Role), text = part.Text })
                    .ToArray()
            })
            .Where(static item => item.content.Length > 0)
            .ToArray();

        return new
        {
            model = request.Model,
            input,
            stream
        };
    }

    private HttpRequestMessage CreateHttpRequest(object payload)
    {
        var endpoint = BuildResponsesUri();
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = CreateJsonContent(payload)
        };

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return request;
    }

    private Uri BuildResponsesUri() => new(new Uri(_options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute), "responses");

    private static HttpContent CreateJsonContent(object payload)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        var content = new ByteArrayContent(jsonBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var message = $"ChatGPT backend request failed: {(int)response.StatusCode} ({response.StatusCode}). Body: {body}";
        throw new HttpRequestException(message, null, response.StatusCode);
    }

    private static HttpClient CreateDefaultHttpClient(CodexProviderOptions options)
    {
        var baseUri = new Uri(options.BaseUrl, UriKind.Absolute);
        if (baseUri.IsLoopback)
        {
            return new HttpClient(new HttpClientHandler { UseProxy = false });
        }

        return new HttpClient();
    }

    private static string ResolveContentType(MessageRole role)
    {
        return role switch
        {
            MessageRole.Assistant => "output_text",
            _ => "input_text"
        };
    }

    private static string MapRole(MessageRole role) => role switch
    {
        MessageRole.User => "user",
        MessageRole.Assistant => "assistant",
        MessageRole.System => "system",
        MessageRole.Developer => "system",
        _ => "user"
    };

    private static string ExtractResponseText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind is JsonValueKind.String)
        {
            return outputText.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("output", out var output) && output.ValueKind is JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var outputItem in output.EnumerateArray())
            {
                if (!outputItem.TryGetProperty("content", out var content) || content.ValueKind is not JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var contentItem in content.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var text) && text.ValueKind is JsonValueKind.String)
                    {
                        sb.Append(text.GetString());
                    }
                }
            }

            return sb.ToString();
        }

        return string.Empty;
    }

    private static string ExtractStreamDelta(JsonElement root)
    {
        if (root.TryGetProperty("delta", out var delta) && delta.ValueKind is JsonValueKind.String)
        {
            return delta.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("type", out var typeElement) && typeElement.ValueKind is JsonValueKind.String)
        {
            var eventType = typeElement.GetString();
            if (string.Equals(eventType, "response.output_text.delta", StringComparison.Ordinal)
                && root.TryGetProperty("delta", out var typedDelta)
                && typedDelta.ValueKind is JsonValueKind.String)
            {
                return typedDelta.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static bool IsStreamCompleted(JsonElement root)
    {
        if (!root.TryGetProperty("type", out var typeElement) || typeElement.ValueKind is not JsonValueKind.String)
        {
            return false;
        }

        var eventType = typeElement.GetString();
        return string.Equals(eventType, "response.completed", StringComparison.Ordinal)
            || string.Equals(eventType, "response.failed", StringComparison.Ordinal);
    }
}
