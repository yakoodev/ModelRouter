namespace MultiLlm.Providers.Ollama;

public sealed class OllamaProviderOptions
{
    public string ProviderId { get; init; } = "ollama-openai-compat";

    public string BaseUrl { get; init; } = "http://localhost:11434/v1";

    public string? Model { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(100);

    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
