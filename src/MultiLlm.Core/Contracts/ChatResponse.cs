namespace MultiLlm.Core.Contracts;

public sealed record ChatResponse(
    string ProviderId,
    string Model,
    Message Message,
    string? RequestId = null,
    string? CorrelationId = null,
    UsageStats? Usage = null);
