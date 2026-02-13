namespace MultiLlm.Tools.Mcp;

public interface IMcpToolProvider
{
    IMcpClient CreateClient(string serverName);
}
