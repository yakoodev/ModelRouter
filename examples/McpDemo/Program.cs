using System.Text;
using System.Text.Json;
using MultiLlm.Core.Contracts;
using MultiLlm.Tools.Mcp;

if (args.Contains("--mcp-server"))
{
    await RunMockServerAsync();
    return;
}

var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "McpDemo.csproj"));
var clientOptions = new McpClientOptions
{
    Command = "dotnet",
    Arguments = $"run --project \"{projectPath}\" -- --mcp-server",
    RequestTimeout = TimeSpan.FromSeconds(10)
};

await using var client = new StdioMcpClient(clientOptions);

var tools = await client.GetToolsAsync();
var tool = tools.Single();
Console.WriteLine($"Tool discovered: {tool.Name}");

var callId = Guid.NewGuid().ToString("N");
var toolCallPart = ToolPartMapper.ToToolCallPart(tool.Name, "{\"name\":\"ModelRouter\"}", callId);
var result = await client.CallToolAsync(toolCallPart.ToolName, toolCallPart.ArgumentsJson);
var toolResultPart = ToolPartMapper.ToToolResultPart(toolCallPart.CallId, result.ResultJson, result.IsError);

Console.WriteLine($"Tool call id: {toolCallPart.CallId}");
Console.WriteLine($"Tool result: {toolResultPart.ResultJson}");

static async Task RunMockServerAsync()
{
    while (true)
    {
        using var message = await ReadMessageAsync(Console.OpenStandardInput());
        var root = message.RootElement;

        if (!root.TryGetProperty("id", out var idElement))
        {
            continue;
        }

        var method = root.GetProperty("method").GetString();
        switch (method)
        {
            case "initialize":
                await WriteMessageAsync(Console.OpenStandardOutput(), $$"""
                {
                  "jsonrpc": "2.0",
                  "id": {{idElement.GetRawText()}},
                  "result": {
                    "protocolVersion": "2024-11-05",
                    "capabilities": {
                      "tools": {}
                    },
                    "serverInfo": {
                      "name": "mcp-demo-server",
                      "version": "0.1.0"
                    }
                  }
                }
                """);
                break;
            case "tools/list":
                await WriteMessageAsync(Console.OpenStandardOutput(), $$"""
                {
                  "jsonrpc": "2.0",
                  "id": {{idElement.GetRawText()}},
                  "result": {
                    "tools": [
                      {
                        "name": "say_hello",
                        "description": "Returns greeting",
                        "inputSchema": {
                          "type": "object",
                          "properties": {
                            "name": { "type": "string" }
                          },
                          "required": ["name"]
                        }
                      }
                    ]
                  }
                }
                """);
                break;
            case "tools/call":
                var name = root.GetProperty("params").GetProperty("arguments").GetProperty("name").GetString() ?? "unknown";
                await WriteMessageAsync(Console.OpenStandardOutput(), $$"""
                {
                  "jsonrpc": "2.0",
                  "id": {{idElement.GetRawText()}},
                  "result": {
                    "content": [
                      {
                        "type": "text",
                        "text": "Hello, {{name}}!"
                      }
                    ],
                    "isError": false
                  }
                }
                """);
                break;
        }
    }
}

static async Task<JsonDocument> ReadMessageAsync(Stream input)
{
    var bytes = new List<byte>();
    while (true)
    {
        var buffer = new byte[1];
        var read = await input.ReadAsync(buffer);
        if (read == 0)
        {
            throw new EndOfStreamException("stdin closed");
        }

        bytes.Add(buffer[0]);
        if (bytes.Count >= 4 && bytes[^4] == (byte)'\r' && bytes[^3] == (byte)'\n' && bytes[^2] == (byte)'\r' && bytes[^1] == (byte)'\n')
        {
            break;
        }
    }

    var header = Encoding.ASCII.GetString(bytes.ToArray());
    var contentLength = int.Parse(header.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Single(x => x.StartsWith("Content-Length", StringComparison.OrdinalIgnoreCase)).Split(':')[1]);
    var payload = new byte[contentLength];
    var total = 0;
    while (total < contentLength)
    {
        var read = await input.ReadAsync(payload.AsMemory(total, contentLength - total));
        if (read == 0)
        {
            throw new EndOfStreamException("stdin closed");
        }

        total += read;
    }

    return JsonDocument.Parse(payload);
}

static async Task WriteMessageAsync(Stream output, string json)
{
    var payload = Encoding.UTF8.GetBytes(json);
    var header = Encoding.ASCII.GetBytes($"Content-Length: {payload.Length}\r\n\r\n");
    await output.WriteAsync(header);
    await output.WriteAsync(payload);
    await output.FlushAsync();
}
