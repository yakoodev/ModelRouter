using System.Net;
using System.Text;
using System.Text.Json;

namespace MultiLlm.Integration.Tests;

internal sealed class MockOpenAiSdkServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _serverTask;

    public MockOpenAiSdkServer()
    {
        var port = GetFreePort();
        BaseUri = new Uri($"http://127.0.0.1:{port}/");
        _listener.Prefixes.Add(BaseUri.ToString());
    }

    public Uri BaseUri { get; }

    public JsonDocument? LastRequestJson { get; private set; }

    public void Start()
    {
        _listener.Start();
        _serverTask = Task.Run(() => RunAsync(_cts.Token));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }

            await HandleRequestAsync(context, cancellationToken);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
            var requestBody = await reader.ReadToEndAsync(cancellationToken);
            LastRequestJson?.Dispose();
            LastRequestJson = JsonDocument.Parse(requestBody);

            var stream = LastRequestJson.RootElement.TryGetProperty("stream", out var streamElement)
                && streamElement.ValueKind == JsonValueKind.True;

            if (!stream)
            {
                const string body = """
                {
                  "choices": [{"message": {"role": "assistant", "content": "sdk-response"}}],
                  "usage": {"prompt_tokens": 3, "completion_tokens": 2, "total_tokens": 5}
                }
                """;

                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes, cancellationToken);
                context.Response.Close();
                return;
            }

            var chunks = new[]
            {
                "data: {\"choices\":[{\"delta\":{\"content\":\"hel\"}}]}\r\n\r\n",
                "data: {\"choices\":[{\"delta\":{\"content\":\"lo\"}}]}\r\n\r\n",
                "data: [DONE]\r\n\r\n"
            };

            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/event-stream";
            foreach (var chunk in chunks)
            {
                var bytes = Encoding.UTF8.GetBytes(chunk);
                await context.Response.OutputStream.WriteAsync(bytes, cancellationToken);
                await context.Response.OutputStream.FlushAsync(cancellationToken);
            }

            context.Response.Close();
        }
        catch
        {
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch
            {
                // no-op, response might already be closed
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Close();
        try
        {
            _serverTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // ignore dispose race
        }

        LastRequestJson?.Dispose();
        _cts.Dispose();
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
