namespace MultiLlm.Core.Auth;

public sealed class CustomHeadersAuth : IAuthStrategy
{
    private readonly IReadOnlyDictionary<string, string> _headers;

    public CustomHeadersAuth(IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        if (headers.Count == 0)
        {
            throw new ArgumentException("Custom headers must contain at least one entry.", nameof(headers));
        }

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, value) in headers)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Header name must not be null, empty, or whitespace.", nameof(headers));
            }

            if (value is null)
            {
                throw new ArgumentException($"Header '{name}' must not have a null value.", nameof(headers));
            }

            normalized[name] = value;
        }

        _headers = normalized;
    }

    public ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        foreach (var (name, value) in _headers)
        {
            AuthHeaderWriter.SetHeader(request, name, value);
        }

        return ValueTask.CompletedTask;
    }
}
