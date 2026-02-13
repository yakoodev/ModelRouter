namespace MultiLlm.Core.Contracts;

public static class ToolPartMapper
{
    public static ToolCallPart ToToolCallPart(string toolName, string argumentsJson, string callId)
        => new(toolName, argumentsJson, callId);

    public static ToolResultPart ToToolResultPart(string callId, string resultJson, bool isError = false)
        => new(callId, resultJson, isError);
}
