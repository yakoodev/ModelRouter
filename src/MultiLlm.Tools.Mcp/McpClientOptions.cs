namespace MultiLlm.Tools.Mcp;

public sealed class McpClientOptions
{
    public required string Command { get; init; }

    public string Arguments { get; init; } = string.Empty;

    public string? WorkingDirectory { get; init; }

    public IDictionary<string, string?> EnvironmentVariables { get; init; } = new Dictionary<string, string?>();

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(15);

    public string ClientName { get; init; } = "multillm-mcp-client";

    public string ClientVersion { get; init; } = "0.1.0";

    public string ProtocolVersion { get; init; } = "2024-11-05";
}
