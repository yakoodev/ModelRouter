namespace MultiLlm.Core.Contracts;

public abstract record MessagePart;

public sealed record TextPart(string Text) : MessagePart;

public sealed record ImagePart(string MimeType, byte[] Content, string? FileName = null) : MessagePart;

public sealed record FilePart(string MimeType, string FileName, Stream Content) : MessagePart;

public sealed record ToolCallPart(string ToolName, string ArgumentsJson, string CallId) : MessagePart;

public sealed record ToolResultPart(string CallId, string ResultJson, bool IsError = false) : MessagePart;
