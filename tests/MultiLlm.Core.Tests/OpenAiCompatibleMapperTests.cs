using System.Text.Json;
using MultiLlm.Core.Contracts;
using MultiLlm.Providers.OpenAICompatible;

namespace MultiLlm.Core.Tests;

public class OpenAiCompatibleMapperTests
{
    [Fact]
    public void BuildChatPayload_MapsRolesAndParts()
    {
        var request = new ChatRequest(
            Model: "gpt-4.1-mini",
            Messages:
            [
                new Message(MessageRole.System, [new TextPart("policy")]),
                new Message(MessageRole.User, [new TextPart("hello")])
            ]);

        var payload = OpenAiCompatibleMapper.BuildChatPayload(request, stream: true);
        var json = JsonSerializer.Serialize(payload);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("gpt-4.1-mini", root.GetProperty("model").GetString());
        Assert.True(root.GetProperty("stream").GetBoolean());

        var messages = root.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("policy", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("hello", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public void BuildChatPayload_MapsToolCallAndToolResultParts()
    {
        var request = new ChatRequest(
            Model: "gpt-4.1-mini",
            Messages:
            [
                new Message(MessageRole.Assistant, [new ToolCallPart("weather", "{\"city\":\"Moscow\"}", "call-42")]),
                new Message(MessageRole.Tool, [new ToolResultPart("call-42", "{\"temp\":21}")])
            ]);

        var payload = OpenAiCompatibleMapper.BuildChatPayload(request, stream: false);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload));

        var messages = document.RootElement.GetProperty("messages");
        var toolCall = messages[0].GetProperty("content")[0];
        Assert.Equal("tool_call", toolCall.GetProperty("type").GetString());
        Assert.Equal("weather", toolCall.GetProperty("tool_name").GetString());
        Assert.Equal("call-42", toolCall.GetProperty("call_id").GetString());

        var toolResult = messages[1].GetProperty("content")[0];
        Assert.Equal("tool_result", toolResult.GetProperty("type").GetString());
        Assert.Equal("call-42", toolResult.GetProperty("call_id").GetString());
        Assert.Equal("{\"temp\":21}", toolResult.GetProperty("result").GetString());
    }

    [Fact]
    public void ParseChatResponse_ReturnsUsageAndAssistantMessage()
    {
        const string responseJson = """
        {
          "choices": [{ "message": { "content": "result text" } }],
          "usage": {
            "prompt_tokens": 10,
            "completion_tokens": 5,
            "total_tokens": 15
          }
        }
        """;

        using var document = JsonDocument.Parse(responseJson);
        var response = OpenAiCompatibleMapper.ParseChatResponse(document.RootElement, "openai-compatible", "gpt-4.1-mini", "req-1", "corr-1");

        Assert.Equal("openai-compatible", response.ProviderId);
        Assert.Equal("gpt-4.1-mini", response.Model);
        Assert.Equal(MessageRole.Assistant, response.Message.Role);
        Assert.Equal("result text", ((TextPart)response.Message.Parts[0]).Text);
        Assert.Equal(10, response.Usage!.InputTokens);
        Assert.Equal(5, response.Usage.OutputTokens);
        Assert.Equal(15, response.Usage.TotalTokens);
    }

    [Fact]
    public void ParseStreamDelta_HandlesStringAndStructuredContent()
    {
        const string stringChunk = """
        { "choices": [{ "delta": { "content": "hel" } }] }
        """;
        using var stringDoc = JsonDocument.Parse(stringChunk);

        Assert.Equal("hel", OpenAiCompatibleMapper.ParseStreamDelta(stringDoc.RootElement));

        const string structuredChunk = """
        {
          "choices": [{
            "delta": {
              "content": [
                { "type": "text", "text": "lo" }
              ]
            }
          }]
        }
        """;
        using var structuredDoc = JsonDocument.Parse(structuredChunk);

        Assert.Equal("lo", OpenAiCompatibleMapper.ParseStreamDelta(structuredDoc.RootElement));
    }
}
