namespace MultiLlm.Providers.OpenAI;

public sealed class OpenAiProviderOptions
{
    public string ProviderId { get; init; } = "openai";

    public string ApiKey { get; init; } = string.Empty;

    public string? Model { get; init; }

    public string? BaseUrl { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(100);
}
