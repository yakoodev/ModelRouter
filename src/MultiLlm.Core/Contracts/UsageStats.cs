namespace MultiLlm.Core.Contracts;

public sealed record UsageStats(int InputTokens, int OutputTokens, int TotalTokens);
