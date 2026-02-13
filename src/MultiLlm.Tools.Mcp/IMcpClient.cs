namespace MultiLlm.Tools.Mcp;

public interface IMcpClient
{
    Task<IReadOnlyList<McpToolDescriptor>> GetToolsAsync(CancellationToken cancellationToken = default);

    Task<McpToolResult> CallToolAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default);
}

public sealed record McpToolDescriptor(string Name, string Description, string InputSchemaJson);

public sealed record McpToolResult(string ToolName, string ResultJson, bool IsError = false);
