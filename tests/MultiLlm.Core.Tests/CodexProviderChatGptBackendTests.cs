using System.Net;
using System.Text;
using MultiLlm.Core.Auth;
using MultiLlm.Core.Contracts;
using MultiLlm.Providers.Codex;

namespace MultiLlm.Core.Tests;

public class CodexProviderChatGptBackendTests
{
    [Fact]
    public async Task ChatAsync_UsesChatGptResponsesEndpoint_WhenEnabled()
    {
        var handler = new StubHandler(static request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://chatgpt.com/backend-api/codex/responses", request.RequestUri!.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("device-token", request.Headers.Authorization?.Parameter);
            Assert.Equal("application/json", request.Content?.Headers.ContentType?.MediaType);

            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.Contains("\"stream\":false", body, StringComparison.Ordinal);
            Assert.Contains("\"input\"", body, StringComparison.Ordinal);
            Assert.Contains("\"type\":\"message\"", body, StringComparison.Ordinal);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"output_text\":\"Привет из ChatGPT backend\"}", Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler);
        var options = new CodexProviderOptions
        {
            BaseUrl = "https://chatgpt.com/backend-api/codex/",
            Model = "gpt-5-mini",
            UseChatGptBackend = true
        };

        var store = new InMemoryTokenStore([
            new KeyValuePair<string, AuthToken>(
                OfficialDeviceCodeBackend.DeviceTokenStoreKey,
                new AuthToken("device-token", null, "refresh-token"))
        ]);

        var provider = new CodexProvider(options, [new NoOpOfficialBackend()], store, httpClient);

        var response = await provider.ChatAsync(new ChatRequest(
            Model: "gpt-5-mini",
            Messages: [new Message(MessageRole.User, [new TextPart("кто ты?")])]
        ));

        var text = string.Concat(response.Message.Parts.OfType<TextPart>().Select(x => x.Text));
        Assert.Equal("Привет из ChatGPT backend", text);
    }


    [Fact]
    public async Task ChatAsync_EncodesAssistantHistoryAsOutputText()
    {
        var handler = new StubHandler(static request =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.Contains("\"role\":\"assistant\"", body, StringComparison.Ordinal);
            Assert.Contains("\"type\":\"output_text\"", body, StringComparison.Ordinal);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"output_text\":\"ok\"}", Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler);
        var options = new CodexProviderOptions
        {
            BaseUrl = "https://chatgpt.com/backend-api/codex/",
            Model = "gpt-5-mini",
            UseChatGptBackend = true
        };

        var store = new InMemoryTokenStore([
            new KeyValuePair<string, AuthToken>(
                OfficialDeviceCodeBackend.DeviceTokenStoreKey,
                new AuthToken("device-token", null, "refresh-token"))
        ]);

        var provider = new CodexProvider(options, [new NoOpOfficialBackend()], store, httpClient);

        await provider.ChatAsync(new ChatRequest(
            Model: "gpt-5-mini",
            Messages:
            [
                new Message(MessageRole.User, [new TextPart("привет")]),
                new Message(MessageRole.Assistant, [new TextPart("ответ")]),
                new Message(MessageRole.User, [new TextPart("следующий вопрос")])
            ]
        ));
    }

    private sealed class NoOpOfficialBackend : ICodexAuthBackend
    {
        public string BackendId => OfficialDeviceCodeBackend.BackendIdValue;

        public ValueTask AuthenticateAsync(ITokenStore tokenStore, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
