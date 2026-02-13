using MultiLlm.Core.Instructions;

namespace MultiLlm.Core.Contracts;

public sealed record ChatRequest(
    string Model,
    IReadOnlyList<Message> Messages,
    InstructionLayers? Instructions = null,
    string? RequestId = null,
    string? CorrelationId = null);
