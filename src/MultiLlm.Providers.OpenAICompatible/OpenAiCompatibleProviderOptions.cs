namespace MultiLlm.Providers.OpenAICompatible;

public sealed class OpenAiCompatibleProviderOptions
{
    public required string BaseUrl { get; init; }

    public string? Model { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(100);

    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
