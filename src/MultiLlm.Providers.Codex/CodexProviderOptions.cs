namespace MultiLlm.Providers.Codex;

public sealed record CodexProviderOptions(
    bool IsDevelopment = true,
    bool EnableExperimentalAuthAdapters = false)
{
    public string ProviderId { get; init; } = "codex-dev-only";

    public string BaseUrl { get; init; } = "https://api.openai.com/v1";

    public string? Model { get; init; }

    public string? CodexHome { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(100);

    public void EnsureRuntimeGuards()
    {
        if (!IsDevelopment)
        {
            throw new InvalidOperationException("Codex provider is dev-only and cannot be enabled in production runtime.");
        }
    }
}
