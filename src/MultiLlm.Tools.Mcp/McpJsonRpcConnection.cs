using System.Text;
using System.Text.Json;

namespace MultiLlm.Tools.Mcp;

internal sealed class McpJsonRpcConnection(Stream input, Stream output)
{
    private static readonly byte[] HeaderDelimiter = "\r\n\r\n"u8.ToArray();

    public async Task WriteMessageAsync(JsonElement message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {payload.Length}\r\n\r\n");
        await output.WriteAsync(header, cancellationToken);
        await output.WriteAsync(payload, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

    public async Task<JsonDocument> ReadMessageAsync(CancellationToken cancellationToken)
    {
        var headerBuffer = new List<byte>(256);
        while (true)
        {
            var buffer = new byte[1];
            var bytesRead = await input.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("MCP connection closed while reading message headers.");
            }

            headerBuffer.Add(buffer[0]);
            if (headerBuffer.Count >= HeaderDelimiter.Length &&
                headerBuffer[^4] == HeaderDelimiter[0] &&
                headerBuffer[^3] == HeaderDelimiter[1] &&
                headerBuffer[^2] == HeaderDelimiter[2] &&
                headerBuffer[^1] == HeaderDelimiter[3])
            {
                break;
            }
        }

        var headers = Encoding.ASCII.GetString(headerBuffer.ToArray());
        var lengthLine = headers
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(static line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));

        if (lengthLine is null || !int.TryParse(lengthLine.Split(':', 2)[1].Trim(), out var contentLength) || contentLength < 0)
        {
            throw new InvalidDataException("Invalid MCP Content-Length header.");
        }

        var payload = new byte[contentLength];
        var read = 0;
        while (read < contentLength)
        {
            var bytesRead = await input.ReadAsync(payload.AsMemory(read, contentLength - read), cancellationToken);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("MCP connection closed while reading message body.");
            }

            read += bytesRead;
        }

        return JsonDocument.Parse(payload);
    }
}
