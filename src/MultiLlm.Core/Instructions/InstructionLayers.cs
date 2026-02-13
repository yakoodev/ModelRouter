namespace MultiLlm.Core.Instructions;

public sealed record InstructionLayers(
    string? System = null,
    string? Developer = null,
    string? Session = null,
    string? Request = null)
{
    public IEnumerable<string> OrderedByPriority()
    {
        if (!string.IsNullOrWhiteSpace(Request)) yield return Request;
        if (!string.IsNullOrWhiteSpace(Session)) yield return Session;
        if (!string.IsNullOrWhiteSpace(Developer)) yield return Developer;
        if (!string.IsNullOrWhiteSpace(System)) yield return System;
    }
}
