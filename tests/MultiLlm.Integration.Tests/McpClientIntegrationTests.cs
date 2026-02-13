using MultiLlm.Core.Contracts;
using MultiLlm.Tools.Mcp;

namespace MultiLlm.Integration.Tests;

public sealed class McpClientIntegrationTests
{
    [Fact]
    public async Task StdioClient_CanInitialize_ListTools_AndCallTool()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "examples", "McpDemo", "McpDemo.csproj"));
        var options = new McpClientOptions
        {
            Command = "dotnet",
            Arguments = $"run --project \"{projectPath}\" -- --mcp-server",
            RequestTimeout = TimeSpan.FromSeconds(20)
        };

        await using var client = new StdioMcpClient(options);

        var tools = await client.GetToolsAsync();
        var tool = Assert.Single(tools);
        Assert.Equal("say_hello", tool.Name);

        var toolCall = ToolPartMapper.ToToolCallPart(tool.Name, "{\"name\":\"Integration\"}", "call-1");
        var result = await client.CallToolAsync(toolCall.ToolName, toolCall.ArgumentsJson);
        var mappedResult = ToolPartMapper.ToToolResultPart(toolCall.CallId, result.ResultJson, result.IsError);

        Assert.False(mappedResult.IsError);
        Assert.Contains("Hello, Integration!", mappedResult.ResultJson, StringComparison.Ordinal);
    }
}
