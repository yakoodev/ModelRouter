namespace MultiLlm.Core.Contracts;

public sealed record Message(
    MessageRole Role,
    IReadOnlyList<MessagePart> Parts);

public enum MessageRole
{
    System,
    Developer,
    User,
    Assistant,
    Tool
}
