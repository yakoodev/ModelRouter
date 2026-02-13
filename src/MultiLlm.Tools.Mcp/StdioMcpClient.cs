using System.Diagnostics;
using System.Text.Json;

namespace MultiLlm.Tools.Mcp;

public sealed class StdioMcpClient : IMcpClient, IAsyncDisposable
{
    private readonly Process _process;
    private readonly McpClientOptions _options;
    private readonly McpJsonRpcConnection _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _requestId;
    private bool _initialized;

    public StdioMcpClient(McpClientOptions options)
    {
        _options = options;
        _process = StartProcess(options);
        _connection = new McpJsonRpcConnection(_process.StandardOutput.BaseStream, _process.StandardInput.BaseStream);
    }

    public async Task<IReadOnlyList<McpToolDescriptor>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        using var response = await SendRequestAsync(
            method: "tools/list",
            parametersJson: "{}",
            cancellationToken);

        var tools = new List<McpToolDescriptor>();
        if (!response.RootElement.GetProperty("result").TryGetProperty("tools", out var toolsElement))
        {
            return tools;
        }

        foreach (var tool in toolsElement.EnumerateArray())
        {
            var schema = tool.TryGetProperty("inputSchema", out var inputSchema) ? inputSchema.GetRawText() : "{}";
            tools.Add(new McpToolDescriptor(
                Name: tool.GetProperty("name").GetString() ?? string.Empty,
                Description: tool.TryGetProperty("description", out var description) ? description.GetString() ?? string.Empty : string.Empty,
                InputSchemaJson: schema));
        }

        return tools;
    }

    public async Task<McpToolResult> CallToolAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        using var response = await SendRequestAsync(
            method: "tools/call",
            parametersJson: $$"""
            {
              "name": {{JsonSerializer.Serialize(toolName)}},
              "arguments": {{NormalizeJson(argumentsJson)}}
            }
            """,
            cancellationToken);

        var resultElement = response.RootElement.GetProperty("result");
        var isError = resultElement.TryGetProperty("isError", out var isErrorElement) && isErrorElement.GetBoolean();

        return new McpToolResult(
            ToolName: toolName,
            ResultJson: resultElement.GetRawText(),
            IsError: isError);
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
            _process.Dispose();
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            using var initializeResponse = await SendRequestCoreAsync(
                method: "initialize",
                parametersJson: $$"""
                {
                  "protocolVersion": {{JsonSerializer.Serialize(_options.ProtocolVersion)}},
                  "capabilities": {},
                  "clientInfo": {
                    "name": {{JsonSerializer.Serialize(_options.ClientName)}},
                    "version": {{JsonSerializer.Serialize(_options.ClientVersion)}}
                  }
                }
                """,
                cancellationToken);

            if (!initializeResponse.RootElement.TryGetProperty("result", out _))
            {
                throw new InvalidOperationException("MCP initialize failed: missing result payload.");
            }

            using var initializedDoc = JsonDocument.Parse("""
            {
              "jsonrpc": "2.0",
              "method": "notifications/initialized"
            }
            """);
            await _connection.WriteMessageAsync(initializedDoc.RootElement, cancellationToken);
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<JsonDocument> SendRequestAsync(string method, string parametersJson, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await SendRequestCoreAsync(method, parametersJson, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<JsonDocument> SendRequestCoreAsync(string method, string parametersJson, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.RequestTimeout);

        var requestId = Interlocked.Increment(ref _requestId);
        using var requestDocument = JsonDocument.Parse($$"""
        {
          "jsonrpc": "2.0",
          "id": {{requestId}},
          "method": {{JsonSerializer.Serialize(method)}},
          "params": {{parametersJson}}
        }
        """);

        await _connection.WriteMessageAsync(requestDocument.RootElement, timeoutCts.Token);

        while (true)
        {
            var response = await _connection.ReadMessageAsync(timeoutCts.Token);
            if (!response.RootElement.TryGetProperty("id", out var idElement))
            {
                response.Dispose();
                continue;
            }

            if (idElement.GetInt64() != requestId)
            {
                response.Dispose();
                continue;
            }

            if (response.RootElement.TryGetProperty("error", out var error))
            {
                throw new InvalidOperationException($"MCP request '{method}' failed: {error.GetRawText()}");
            }

            return response;
        }
    }

    private static string NormalizeJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetRawText();
    }

    private static Process StartProcess(McpClientOptions options)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = options.Command,
            Arguments = options.Arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(options.WorkingDirectory))
        {
            startInfo.WorkingDirectory = options.WorkingDirectory;
        }

        foreach (var variable in options.EnvironmentVariables)
        {
            startInfo.Environment[variable.Key] = variable.Value;
        }

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Cannot start MCP server process '{options.Command}'.");
        return process;
    }
}
