namespace MultiLlm.Core.Events;

public interface ILlmEventHook
{
    ValueTask OnStartAsync(string requestId, string? correlationId, CancellationToken cancellationToken = default);
    ValueTask OnEndAsync(string requestId, string? correlationId, CancellationToken cancellationToken = default);
    ValueTask OnErrorAsync(string requestId, string? correlationId, Exception exception, CancellationToken cancellationToken = default);
}
