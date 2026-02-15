using System.Text;
using MultiLlm.Core.Contracts;
using OpenAI.Chat;

namespace MultiLlm.Providers.OpenAI;

internal static class OpenAiSdkMessageMapper
{
    public static List<ChatMessage> MapMessages(IReadOnlyList<Message> messages)
    {
        var mapped = new List<ChatMessage>(messages.Count);
        foreach (var message in messages)
        {
            mapped.Add(MapMessage(message));
        }

        return mapped;
    }

    public static UsageStats? MapUsage(ChatTokenUsage? usage)
    {
        if (usage is null)
        {
            return null;
        }

        return new UsageStats(usage.InputTokenCount, usage.OutputTokenCount, usage.TotalTokenCount);
    }

    public static string ToText(IReadOnlyList<ChatMessageContentPart> parts)
    {
        if (parts.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            sb.Append(part.Text);
        }

        return sb.ToString();
    }

    private static ChatMessage MapMessage(Message message)
    {
        return message.Role switch
        {
            MessageRole.System => new SystemChatMessage(ToText(MapParts(message.Parts))),
            // OpenAI SDK marks DeveloperChatMessage as experimental (OPENAI001).
            // Map developer instructions as system content to keep strong instruction priority
            // without introducing compile-time experimental diagnostics.
            MessageRole.Developer => new SystemChatMessage(ToText(MapParts(message.Parts))),
            MessageRole.User => new UserChatMessage(MapParts(message.Parts)),
            MessageRole.Assistant => new AssistantChatMessage(ToText(MapParts(message.Parts))),
            MessageRole.Tool => new UserChatMessage(MapParts(message.Parts)),
            _ => throw new ArgumentOutOfRangeException(nameof(message.Role), message.Role, "Unknown message role")
        };
    }

    private static ChatMessageContentPart[] MapParts(IReadOnlyList<MessagePart> parts)
    {
        var mapped = new List<ChatMessageContentPart>(parts.Count);
        foreach (var part in parts)
        {
            mapped.AddRange(MapPart(part));
        }

        return [.. mapped];
    }

    private static IEnumerable<ChatMessageContentPart> MapPart(MessagePart part)
    {
        switch (part)
        {
            case TextPart text:
                yield return ChatMessageContentPart.CreateTextPart(text.Text);
                break;
            case ImagePart image:
                var imageUri = new Uri($"data:{image.MimeType};base64,{Convert.ToBase64String(image.Content)}", UriKind.Absolute);
                yield return ChatMessageContentPart.CreateImagePart(imageUri);
                break;
            case FilePart file:
                using (var ms = new MemoryStream())
                {
                    if (file.Content.CanSeek)
                    {
                        file.Content.Position = 0;
                    }

                    file.Content.CopyTo(ms);
                    if (file.Content.CanSeek)
                    {
                        file.Content.Position = 0;
                    }

                    var dataUrl = $"data:{file.MimeType};base64,{Convert.ToBase64String(ms.ToArray())}";
                    yield return ChatMessageContentPart.CreateTextPart($"[file:{file.FileName}] {dataUrl}");
                }

                break;
            case ToolCallPart toolCall:
                yield return ChatMessageContentPart.CreateTextPart($"[tool_call:{toolCall.ToolName}:{toolCall.CallId}] {toolCall.ArgumentsJson}");
                break;
            case ToolResultPart toolResult:
                yield return ChatMessageContentPart.CreateTextPart($"[tool_result:{toolResult.CallId}:error={toolResult.IsError}] {toolResult.ResultJson}");
                break;
            default:
                throw new NotSupportedException($"Message part '{part.GetType().Name}' is not supported by OpenAI SDK mapper.");
        }
    }
}
