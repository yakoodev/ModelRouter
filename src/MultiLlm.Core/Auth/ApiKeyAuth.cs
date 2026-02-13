namespace MultiLlm.Core.Auth;

public sealed class ApiKeyAuth(string apiKey, string headerName = "X-Api-Key") : IAuthStrategy
{
    private readonly string _apiKey = string.IsNullOrWhiteSpace(apiKey)
        ? throw new ArgumentException("API key must not be null, empty, or whitespace.", nameof(apiKey))
        : apiKey;

    private readonly string _headerName = string.IsNullOrWhiteSpace(headerName)
        ? throw new ArgumentException("Header name must not be null, empty, or whitespace.", nameof(headerName))
        : headerName;

    public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        AuthHeaderWriter.SetHeader(request, _headerName, _apiKey);
        return ValueTask.CompletedTask;
    }
}
