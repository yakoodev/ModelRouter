using MultiLlm.Core.Contracts;

namespace MultiLlm.Core.Instructions;

public static class InstructionNormalizer
{
    public static ChatRequest Normalize(ChatRequest request)
    {
        var normalizedLayers = request.Instructions?.Normalize();
        if (normalizedLayers is null)
        {
            return request;
        }

        var instructionMessages = BuildInstructionMessages(normalizedLayers);
        if (instructionMessages.Count == 0)
        {
            return request with { Instructions = normalizedLayers };
        }

        var mergedMessages = instructionMessages
            .Concat(request.Messages)
            .ToArray();

        return request with
        {
            Instructions = normalizedLayers,
            Messages = mergedMessages
        };
    }

    private static IReadOnlyList<Message> BuildInstructionMessages(InstructionLayers layers)
    {
        var messages = new List<Message>(capacity: 4);

        Add(messages, MessageRole.Developer, "request", layers.Request);
        Add(messages, MessageRole.Developer, "session", layers.Session);
        Add(messages, MessageRole.Developer, "developer", layers.Developer);
        Add(messages, MessageRole.System, "system", layers.System);

        return messages;
    }

    private static void Add(ICollection<Message> messages, MessageRole role, string layerName, string? text)
    {
        if (text is null)
        {
            return;
        }

        messages.Add(new Message(role, [new TextPart($"[{layerName}]\n{text}")]));
    }
}
