namespace MultiLlm.Providers.Codex;

public sealed record CodexProviderOptions(
    bool IsDevelopment = true,
    bool EnableExperimentalAuthAdapters = false)
{
    public void EnsureRuntimeGuards()
    {
        if (!IsDevelopment)
        {
            throw new InvalidOperationException("Codex provider is dev-only and cannot be enabled in production runtime.");
        }
    }
}
