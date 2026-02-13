namespace MultiLlm.Core.Auth;

public interface IAuthStrategy
{
    ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}
