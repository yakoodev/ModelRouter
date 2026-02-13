namespace MultiLlm.Core.Auth;

public sealed class NoAuth : IAuthStrategy
{
    public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.CompletedTask;
    }
}
