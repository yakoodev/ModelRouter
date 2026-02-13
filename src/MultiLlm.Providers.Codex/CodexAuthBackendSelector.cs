namespace MultiLlm.Providers.Codex;

public static class CodexAuthBackendSelector
{
    public static IReadOnlyList<ICodexAuthBackend> Select(CodexProviderOptions options, IEnumerable<ICodexAuthBackend> backends)
    {
        options.EnsureRuntimeGuards();

        var backendById = backends.ToDictionary(backend => backend.BackendId, StringComparer.Ordinal);

        if (!backendById.TryGetValue(OfficialDeviceCodeBackend.BackendIdValue, out var officialBackend))
        {
            throw new InvalidOperationException($"Missing required auth backend '{OfficialDeviceCodeBackend.BackendIdValue}'.");
        }

        var selected = new List<ICodexAuthBackend> { officialBackend };

        if (options.EnableExperimentalAuthAdapters
            && backendById.TryGetValue(ExperimentalAuthBackend.BackendIdValue, out var experimentalBackend))
        {
            selected.Add(experimentalBackend);
        }

        return selected;
    }
}
