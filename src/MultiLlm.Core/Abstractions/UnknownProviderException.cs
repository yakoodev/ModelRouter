namespace MultiLlm.Core.Abstractions;

public sealed class UnknownProviderException(string providerId)
    : InvalidOperationException($"Provider '{providerId}' is not registered.")
{
    public string ProviderId { get; } = providerId;
}
