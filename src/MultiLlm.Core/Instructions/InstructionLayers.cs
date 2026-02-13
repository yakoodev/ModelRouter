namespace MultiLlm.Core.Instructions;

public sealed record InstructionLayers(
    string? System = null,
    string? Developer = null,
    string? Session = null,
    string? Request = null)
{
    public InstructionLayers Normalize() => new(
        System: NormalizeValue(System),
        Developer: NormalizeValue(Developer),
        Session: NormalizeValue(Session),
        Request: NormalizeValue(Request));

    public IEnumerable<string> OrderedByPriority()
    {
        var normalized = Normalize();

        if (normalized.Request is not null) yield return normalized.Request;
        if (normalized.Session is not null) yield return normalized.Session;
        if (normalized.Developer is not null) yield return normalized.Developer;
        if (normalized.System is not null) yield return normalized.System;
    }

    private static string? NormalizeValue(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
