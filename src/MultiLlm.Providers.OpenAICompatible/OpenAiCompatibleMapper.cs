using System.Text.Json;
using MultiLlm.Core.Contracts;

namespace MultiLlm.Providers.OpenAICompatible;

internal static class OpenAiCompatibleMapper
{
    private const int MaxImageBytes = 20 * 1024 * 1024;
    private const int MaxFileBytes = 32 * 1024 * 1024;

    private static readonly HashSet<string> SupportedImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp",
        "image/gif"
    };

    public static object BuildChatPayload(ChatRequest request, bool stream) => new
    {
        model = request.Model,
        messages = request.Messages.Select(MapMessage).ToArray(),
        stream
    };

    public static ChatResponse ParseChatResponse(JsonElement root, string providerId, string model, string? requestId, string? correlationId)
    {
        var message = root.GetProperty("choices")[0].GetProperty("message");
        var text = ParseContent(message.GetProperty("content"));
        var usage = ParseUsage(root);

        return new ChatResponse(
            ProviderId: providerId,
            Model: model,
            Message: new Message(MessageRole.Assistant, [new TextPart(text)]),
            RequestId: requestId,
            CorrelationId: correlationId,
            Usage: usage);
    }

    public static string ParseStreamDelta(JsonElement chunk)
    {
        if (!chunk.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var delta = choices[0].GetProperty("delta");
        if (!delta.TryGetProperty("content", out var content))
        {
            return string.Empty;
        }

        return ParseContent(content);
    }

    private static object MapMessage(Message message)
    {
        var role = message.Role switch
        {
            MessageRole.System => "system",
            MessageRole.Developer => "developer",
            MessageRole.User => "user",
            MessageRole.Assistant => "assistant",
            MessageRole.Tool => "tool",
            _ => throw new ArgumentOutOfRangeException(nameof(message.Role), message.Role, "Unknown message role")
        };

        var contentParts = message.Parts.Select(MapPart).ToArray();

        object content = contentParts.Length == 1
            && contentParts[0].TryGetValue("type", out var type)
            && Equals(type, "text")
            && contentParts[0].TryGetValue("text", out var text)
            ? text
            : contentParts;

        return new
        {
            role,
            content
        };
    }

    private static Dictionary<string, object> MapPart(MessagePart part) => part switch
    {
        TextPart text => new Dictionary<string, object>
        {
            ["type"] = "text",
            ["text"] = text.Text
        },
        ImagePart image => MapImagePart(image),
        ToolCallPart toolCall => new Dictionary<string, object>
        {
            ["type"] = "tool_call",
            ["tool_name"] = toolCall.ToolName,
            ["arguments"] = toolCall.ArgumentsJson,
            ["call_id"] = toolCall.CallId
        },
        ToolResultPart toolResult => new Dictionary<string, object>
        {
            ["type"] = "tool_result",
            ["call_id"] = toolResult.CallId,
            ["result"] = toolResult.ResultJson,
            ["is_error"] = toolResult.IsError
        },
        FilePart file => MapFilePart(file),
        _ => throw new NotSupportedException($"Message part '{part.GetType().Name}' is not supported by OpenAI-compatible payload mapper.")
    };

    private static Dictionary<string, object> MapImagePart(ImagePart image)
    {
        if (!SupportedImageMimeTypes.Contains(image.MimeType))
        {
            throw new NotSupportedException($"Image mime type '{image.MimeType}' is not supported. Supported: {string.Join(", ", SupportedImageMimeTypes)}.");
        }

        if (image.Content.Length > MaxImageBytes)
        {
            throw new ArgumentException($"Image size exceeds maximum of {MaxImageBytes} bytes.", nameof(image));
        }

        return new Dictionary<string, object>
        {
            ["type"] = "image_url",
            ["image_url"] = new Dictionary<string, object>
            {
                ["url"] = $"data:{image.MimeType};base64,{Convert.ToBase64String(image.Content)}"
            }
        };
    }

    private static Dictionary<string, object> MapFilePart(FilePart file)
    {
        if (string.IsNullOrWhiteSpace(file.FileName))
        {
            throw new ArgumentException("File name is required for FilePart.", nameof(file));
        }

        var fileBytes = ReadStreamWithLimit(file.Content, MaxFileBytes);

        return new Dictionary<string, object>
        {
            ["type"] = "file",
            ["file"] = new Dictionary<string, object>
            {
                ["file_name"] = file.FileName,
                ["mime_type"] = file.MimeType,
                ["file_data"] = $"data:{file.MimeType};base64,{Convert.ToBase64String(fileBytes)}"
            }
        };
    }

    private static byte[] ReadStreamWithLimit(Stream stream, int maxBytes)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var ms = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0)
            {
                break;
            }

            ms.Write(buffer, 0, read);
            if (ms.Length > maxBytes)
            {
                throw new ArgumentException($"File size exceeds maximum of {maxBytes} bytes.", nameof(stream));
            }
        }

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return ms.ToArray();
    }

    private static UsageStats? ParseUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usageElement))
        {
            return null;
        }

        var input = usageElement.TryGetProperty("prompt_tokens", out var prompt) ? prompt.GetInt32() : 0;
        var output = usageElement.TryGetProperty("completion_tokens", out var completion) ? completion.GetInt32() : 0;
        var total = usageElement.TryGetProperty("total_tokens", out var totalTokens) ? totalTokens.GetInt32() : input + output;

        return new UsageStats(input, output, total);
    }

    private static string ParseContent(JsonElement content)
    {
        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString() ?? string.Empty,
            JsonValueKind.Array => string.Concat(content.EnumerateArray().Select(ParseArrayContentPart)),
            JsonValueKind.Null => string.Empty,
            _ => string.Empty
        };
    }

    private static string ParseArrayContentPart(JsonElement part)
    {
        if (part.ValueKind == JsonValueKind.String)
        {
            return part.GetString() ?? string.Empty;
        }

        if (part.ValueKind == JsonValueKind.Object
            && part.TryGetProperty("type", out var type)
            && type.GetString() == "text"
            && part.TryGetProperty("text", out var text))
        {
            return text.GetString() ?? string.Empty;
        }

        return string.Empty;
    }
}
