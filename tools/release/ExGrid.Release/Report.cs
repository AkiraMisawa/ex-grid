using System.Text;

namespace ExGrid.Release;

/// <summary>One refusal: what is wrong, and where when a file is to blame.</summary>
public sealed record Refusal(string Message, string? File = null, int? Line = null);

/// <summary>
/// What the checks found. Every refusal is collected and said, not only the first, so one run
/// of the release workflow names everything a tag still needs. Printed as GitHub's workflow
/// commands, which a terminal shows as plain lines.
/// </summary>
public sealed class Report
{
    private readonly List<Refusal> _refusals = [];
    private readonly List<string> _notices = [];

    /// <summary>The refusals, in the order the checks made them.</summary>
    public IReadOnlyList<Refusal> Refusals => _refusals;

    /// <summary>What the checks found that refuses nothing: a row whose record holds, a site not published.</summary>
    public IReadOnlyList<string> Notices => _notices;

    /// <summary>Whether nothing was refused.</summary>
    public bool Admitted => _refusals.Count == 0;

    /// <summary>Refuses the tag.</summary>
    public void Refuse(string message, string? file = null, int? line = null) => _refusals.Add(new Refusal(message, file, line));

    /// <summary>Says something that refuses nothing.</summary>
    public void Notice(string message) => _notices.Add(message);

    /// <summary>Writes every notice and refusal as workflow commands.</summary>
    public void WriteTo(TextWriter output)
    {
        foreach (var notice in _notices)
            output.WriteLine($"::notice::{Escape(notice)}");
        foreach (var refusal in _refusals)
        {
            var where = new StringBuilder();
            if (refusal.File is not null) where.Append(" file=").Append(EscapeProperty(refusal.File));
            if (refusal.Line is not null) where.Append(where.Length > 0 ? "," : " ").Append("line=").Append(refusal.Line);
            output.WriteLine($"::error{where}::{Escape(refusal.Message)}");
        }
    }

    // GitHub's workflow commands: https://docs.github.com/actions/reference/workflow-commands-for-github-actions
    private static string Escape(string text) => text.Replace("%", "%25").Replace("\r", "%0D").Replace("\n", "%0A");

    private static string EscapeProperty(string text) => Escape(text).Replace(":", "%3A").Replace(",", "%2C");
}
