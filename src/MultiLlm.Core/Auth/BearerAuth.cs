namespace MultiLlm.Core.Auth;

public sealed class BearerAuth(string token) : IAuthStrategy
{
    private readonly string _token = string.IsNullOrWhiteSpace(token)
        ? throw new ArgumentException("Bearer token must not be null, empty, or whitespace.", nameof(token))
        : token;

    public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        AuthHeaderWriter.SetHeader(request, "Authorization", $"Bearer {_token}");
        return ValueTask.CompletedTask;
    }
}
