using System.Text;
using System.Text.Json;

namespace ExGrid.Release.Tests;

/// <summary>A row of a fixture's record index and table, and the <c>verified/*</c> tag its record names.</summary>
public sealed record Row(string Name, string Where, string[] Paths, (string Commit, string Path, string Verdict)? Record = null, string Tag = Fixture.RunTag);

/// <summary>The files a fixture repository holds, written as the release check reads them.</summary>
public static class Fixture
{
    public const string Corpus = "tests/ExSheet.Engine.Tests/ExcelCases";

    public const string Results = "verification/2026-11-02-windows/results.md";

    /// <summary>The tag a fixture's run puts on the commit it verified (ADR-0208, 2026-10-12).</summary>
    public const string RunTag = "verified/2026-10-12-windows-17";

    /// <summary>A record index naming these rows.</summary>
    public static string Index(params Row[] rows)
    {
        var json = new
        {
            about = "fixture",
            rows = rows.Select(r => new
            {
                row = r.Name,
                where = r.Where,
                paths = r.Paths,
                record = r.Record is { } held ? new { commit = held.Commit, tag = r.Tag, path = held.Path, verdict = held.Verdict } : null,
            }),
        };
        return JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>A Markdown file holding the table of these rows under its heading, as the Definition of Done would.</summary>
    public static string Table(params Row[] rows)
    {
        var text = new StringBuilder();
        text.AppendLine("# Definition of Done").AppendLine();
        text.AppendLine("### Step 4 — Layer 3, real browsers").AppendLine();
        text.AppendLine("#### Rows checked by hand, and the code each covers").AppendLine();
        text.AppendLine("Each row a person checks.").AppendLine();
        text.AppendLine("| Row | Where | Checked by hand | The code it covers |");
        text.AppendLine("|---|---|---|---|");
        foreach (var row in rows)
            text.AppendLine($"| **{row.Name}** | {row.Where} | A check. | {string.Join(", ", row.Paths.Select(p => $"`{p}`"))} |");
        text.AppendLine().AppendLine("### Step 5 — The randomised consistency run");
        return text.ToString();
    }

    /// <summary>A corpus file holding these cases.</summary>
    public static string Cases(params (string Id, string? Source, string? Skip, string? Differs)[] cases)
    {
        var json = new
        {
            area = "fixture",
            cases = cases.Select(c =>
            {
                var item = new Dictionary<string, object?> { ["id"] = c.Id, ["description"] = "A case", ["expect"] = new { value2 = 1 } };
                if (c.Source is not null) item["source"] = c.Source;
                if (c.Skip is not null) item["oracleSkip"] = c.Skip;
                if (c.Differs is not null) item["engineDiffersByDecision"] = c.Differs;
                return item;
            }),
        };
        return JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>A stable version's notes, with this block under Signed off.</summary>
    public static string Notes(string version, string? signedOff) =>
        $"# {version}\n\nPublished: pending\nPackages: ExGrid\nTarget: net10.0\n\n## Highlights\n\nNone.\n\n"
        + (signedOff is null ? "" : $"## Signed off\n\n{signedOff}\n\n")
        + "## SHOULD criteria accepted failing\n\nNone.\n";

    /// <summary>
    /// A repository ready for a stable tag: code under <c>src/</c>, a corpus every case of which is
    /// observed, one row checked by hand recorded at the commit its code was last changed, which its run
    /// tagged, and the table in the Definition of Done. Returns the commit X to sign off.
    /// </summary>
    public static string ReadyToSignOff(Repo repo, Row? row = null)
    {
        repo.Write("src/Code/a.cs", "class A {}")
            .Write("src/Other/b.cs", "class B {}")
            .Write("README.md", "A repository.")
            .Write($"{Corpus}/fixtures.json", "{\"not\": \"cases\"}")
            .Write($"{Corpus}/abs.json", Cases(("ABS-001", "observed", null, null), ("ABS-002", "documented", "Excel cannot be asked this through COM", null)));
        var coded = repo.Commit("Code");
        var held = row ?? new Row("VZ-14", "Windows", ["src/Code/"]);
        held = held with { Record = held.Record ?? (coded, "verification/2026-10-12-windows-17/report.md", "pass") };
        repo.Tag(held.Tag, held.Record!.Value.Commit);
        repo.Write("verification/2026-10-12-windows-17/report.md", $"Verified commit: `{coded[..7]}`")
            .Write("verification/by-hand.json", Index(held))
            .Write("docs/definition-of-done.md", Table(held));
        return repo.Commit("Records the row");
    }

    /// <summary>
    /// Tags X as a release candidate of <paramref name="version"/>, against which the sign-off is made, unless
    /// <paramref name="candidate"/> is false; adds the sign-off's record and the notes on top of X; and returns
    /// that commit, Y.
    /// </summary>
    public static string SignedOff(Repo repo, string x, string version = "0.1.0", string? block = null, bool candidate = true)
    {
        if (candidate && repo.Git.Commit(x) is not null)
            repo.Tag($"v{version}-rc.1", x);
        repo.Write(Results, $"# Results\n\nVerified commit: `{x}`\n")
            .Write($"docs/releases/{version}.md", Notes(version, block ?? $"Against `{Results}`, made at `{x}`.\n\n- Toolchain: .NET 10.0.100."));
        return repo.Commit("Signs off");
    }
}
