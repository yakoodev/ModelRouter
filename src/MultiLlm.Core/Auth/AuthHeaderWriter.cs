namespace MultiLlm.Core.Auth;

internal static class AuthHeaderWriter
{
    public static void SetHeader(HttpRequestMessage request, string headerName, string headerValue)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        ArgumentNullException.ThrowIfNull(headerValue);

        request.Headers.Remove(headerName);
        request.Headers.TryAddWithoutValidation(headerName, headerValue);
    }
}
