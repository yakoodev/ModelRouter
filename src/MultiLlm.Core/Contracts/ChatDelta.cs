namespace MultiLlm.Core.Contracts;

public sealed record ChatDelta(
    string ProviderId,
    string Model,
    string Delta,
    bool IsFinal,
    string? RequestId = null,
    string? CorrelationId = null);
