using System.Net;
using System.Text;
using System.Text.Json;
using MultiLlm.Core.Contracts;
using MultiLlm.Providers.OpenAICompatible;

namespace MultiLlm.Integration.Tests;

public class OpenAiCompatibleProviderIntegrationTests
{
    [Fact]
    public async Task ChatAsync_SendsOpenAiPayload_AndReadsResponse()
    {
        using var server = new MockOpenAiServer();
        server.Start();

        var provider = new OpenAiCompatibleProvider(new OpenAiCompatibleProviderOptions
        {
            BaseUrl = server.BaseUri.ToString(),
            Headers = new Dictionary<string, string> { ["X-Test-Key"] = "secret" },
            Timeout = TimeSpan.FromSeconds(10)
        });

        var request = new ChatRequest(
            Model: "gpt-4.1-mini",
            Messages: [new Message(MessageRole.User, [new TextPart("Привет")])],
            RequestId: "req-1",
            CorrelationId: "corr-1");

        var response = await provider.ChatAsync(request);

        Assert.Equal("assistant-response", ((TextPart)response.Message.Parts[0]).Text);
        Assert.NotNull(server.LastRequestJson);
        Assert.Equal("gpt-4.1-mini", server.LastRequestJson!.RootElement.GetProperty("model").GetString());
        Assert.Equal("Привет", server.LastRequestJson.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
        Assert.Equal("secret", server.LastRequestHeaders["X-Test-Key"]);
    }

    [Fact]
    public async Task ChatStreamAsync_ParsesSseChunks_AndYieldsFinalDelta()
    {
        using var server = new MockOpenAiServer();
        server.Start();

        var provider = new OpenAiCompatibleProvider(new OpenAiCompatibleProviderOptions
        {
            BaseUrl = server.BaseUri.ToString(),
            Timeout = TimeSpan.FromSeconds(10)
        });

        var request = new ChatRequest(
            Model: "gpt-4.1-mini",
            Messages: [new Message(MessageRole.User, [new TextPart("stream")])]);

        var deltas = await provider.ChatStreamAsync(request).ToListAsync();

        Assert.Equal(3, deltas.Count);
        Assert.Equal("hel", deltas[0].Delta);
        Assert.Equal("lo", deltas[1].Delta);
        Assert.False(deltas[0].IsFinal);
        Assert.True(deltas[^1].IsFinal);
    }

    private sealed class MockOpenAiServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cts = new();
        private Task? _serverTask;

        public MockOpenAiServer()
        {
            var port = GetFreePort();
            BaseUri = new Uri($"http://127.0.0.1:{port}/v1/");
            _listener.Prefixes.Add(BaseUri.ToString());
        }

        public Uri BaseUri { get; }

        public JsonDocument? LastRequestJson { get; private set; }

        public IReadOnlyDictionary<string, string> LastRequestHeaders { get; private set; } = new Dictionary<string, string>();

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
            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
            var requestBody = await reader.ReadToEndAsync(cancellationToken);
            LastRequestJson?.Dispose();
            LastRequestJson = JsonDocument.Parse(requestBody);

            LastRequestHeaders = context.Request.Headers.AllKeys
                .Where(static key => key is not null)
                .ToDictionary(static key => key!, key => context.Request.Headers[key!]!, StringComparer.OrdinalIgnoreCase);

            var stream = LastRequestJson.RootElement.GetProperty("stream").GetBoolean();

            if (!stream)
            {
                const string body = """
                {
                  "choices": [{"message": {"content": "assistant-response"}}],
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
                "data: {\"choices\":[{\"delta\":{\"content\":\"hel\"}}]}\n\n",
                "data: {\"choices\":[{\"delta\":{\"content\":[{\"type\":\"text\",\"text\":\"lo\"}]}}]}\n\n",
                "data: [DONE]\n\n"
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
                // ignored on dispose path
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
}
