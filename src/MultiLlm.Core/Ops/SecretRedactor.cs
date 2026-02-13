using System.Text.RegularExpressions;

namespace MultiLlm.Core.Ops;

public interface ISecretRedactor
{
    string Redact(string? text);

    Exception Redact(Exception exception);
}

public sealed partial class SecretRedactor : ISecretRedactor
{
    private const string Redacted = "[REDACTED]";

    public string Redact(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text ?? string.Empty;
        }

        var redacted = BearerTokenRegex().Replace(text, "$1 " + Redacted);
        redacted = ApiKeyAssignmentRegex().Replace(redacted, "$1=" + Redacted);
        return GenericSecretRegex().Replace(redacted, "$1=" + Redacted);
    }

    public Exception Redact(Exception exception)
    {
        var sanitizedMessage = Redact(exception.Message);
        if (ReferenceEquals(sanitizedMessage, exception.Message) || sanitizedMessage == exception.Message)
        {
            return exception;
        }

        return new SanitizedException(sanitizedMessage, exception.GetType().FullName ?? exception.GetType().Name);
    }

    [GeneratedRegex("(?i)(authorization\\s*:\\s*bearer)\\s+[^\\s,;]+")]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex("(?i)(api[_-]?key|token|secret|password)\\s*=\\s*[^\\s,;]+")]
    private static partial Regex ApiKeyAssignmentRegex();

    [GeneratedRegex("(?i)(x-api-key|access_token|refresh_token)\\s*=\\s*[^\\s,;]+")]
    private static partial Regex GenericSecretRegex();

    private sealed class SanitizedException(string message, string originalType) : Exception(message)
    {
        public override string StackTrace => $"Sanitized exception from {originalType}.";
    }
}
