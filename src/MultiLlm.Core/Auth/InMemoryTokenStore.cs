using System.Collections.Concurrent;

namespace MultiLlm.Core.Auth;

public sealed class InMemoryTokenStore : ITokenStore
{
    private readonly ConcurrentDictionary<string, AuthToken> _tokens;
    private readonly TimeProvider _timeProvider;

    public InMemoryTokenStore(
        IEnumerable<KeyValuePair<string, AuthToken>>? seededTokens = null,
        TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _tokens = seededTokens is null
            ? new ConcurrentDictionary<string, AuthToken>(StringComparer.Ordinal)
            : new ConcurrentDictionary<string, AuthToken>(seededTokens, StringComparer.Ordinal);
    }

    public ValueTask<AuthToken?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_tokens.TryGetValue(key, out var token))
        {
            return ValueTask.FromResult<AuthToken?>(null);
        }

        if (IsExpired(token))
        {
            _tokens.TryRemove(key, out _);
            return ValueTask.FromResult<AuthToken?>(null);
        }

        return ValueTask.FromResult<AuthToken?>(token);
    }

    public ValueTask SetAsync(string key, AuthToken token, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _tokens[key] = token;
        return ValueTask.CompletedTask;
    }

    public void Inject(string key, AuthToken token)
    {
        _tokens[key] = token;
    }

    private bool IsExpired(AuthToken token)
    {
        if (token.ExpiresAt is null)
        {
            return false;
        }

        return token.ExpiresAt.Value <= _timeProvider.GetUtcNow();
    }
}
