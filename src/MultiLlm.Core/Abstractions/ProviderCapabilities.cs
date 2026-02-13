namespace MultiLlm.Core.Abstractions;

public sealed record ProviderCapabilities(
    bool SupportsStreaming,
    bool SupportsImages,
    bool SupportsTools,
    bool SupportsFileAttachments);
