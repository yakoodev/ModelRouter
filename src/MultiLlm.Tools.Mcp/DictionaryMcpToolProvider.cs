namespace MultiLlm.Tools.Mcp;

public sealed class DictionaryMcpToolProvider(IReadOnlyDictionary<string, McpClientOptions> servers) : IMcpToolProvider
{
    public IMcpClient CreateClient(string serverName)
    {
        if (!servers.TryGetValue(serverName, out var options))
        {
            throw new KeyNotFoundException($"MCP server '{serverName}' is not configured.");
        }

        return new StdioMcpClient(options);
    }
}
