namespace MultiLlm.Core.Auth;

public interface ITokenStore
{
    ValueTask<AuthToken?> GetAsync(string key, CancellationToken cancellationToken = default);

    ValueTask SetAsync(string key, AuthToken token, CancellationToken cancellationToken = default);
}

public sealed record AuthToken(string AccessToken, DateTimeOffset? ExpiresAt = null, string? RefreshToken = null);
