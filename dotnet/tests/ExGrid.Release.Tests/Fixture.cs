using System.Text;
using System.Text.Json;

namespace ExGrid.Release.Tests;

/// <summary>
/// A row of a fixture's record index and table, the <c>verified/*</c> tag its record names, and, for WR-10, the
/// MudBlazor versions its record's layer-2 runs covered.
/// </summary>
public sealed record Row(string Name, string Where, string[] Paths, (string Commit, string Path, string Verdict)? Record = null, string Tag = Fixture.RunTag, string[]? Versions = null, Later[]? Later = null, Limited? Limited = null);

/// <summary>
/// A row put out of a version by decision: the index's <c>knownLimitation</c>, and whether the table's row says so
/// (2026-10-12).
/// </summary>
public sealed record Limited(string Version = "0.1.0", string Decided = "2026-10-12", string Decision = "VZ-18 is a Known limitation of 0.1.0.", bool InTable = true);

/// <summary>A later layer-2 run's record WR-10's entry names besides its floor record (2026-10-12).</summary>
public sealed record Later(string Commit, string Tag, string Path, string Verdict, string[] Versions);

/// <summary>The files a fixture repository holds, written as the release check reads them.</summary>
public static class Fixture
{
    public const string Corpus = "dotnet/tests/ExSheet.Engine.Tests/ExcelCases";

    public const string Results = "verification/2026-11-02-windows/results.md";

    /// <summary>The directory a fixture's run records in.</summary>
    public const string RunDirectory = "verification/2026-10-12-windows-17";

    /// <summary>
    /// The tag a fixture's run puts on the commit it verified: <c>verified/</c> followed by the name of its
    /// record's directory (ADR-0208, 2026-10-12).
    /// </summary>
    public const string RunTag = "verified/2026-10-12-windows-17";

    /// <summary>A record index naming these rows.</summary>
    public static string Index(params Row[] rows)
    {
        var json = new
        {
            about = "fixture",
            rows = rows.Select(r =>
            {
                var row = new Dictionary<string, object?>
                {
                    ["row"] = r.Name,
                    ["where"] = r.Where,
                    ["paths"] = r.Paths,
                    ["record"] = r.Record is { } held ? Record(held, r.Tag, r.Versions) : null,
                };
                if (r.Later is not null)
                    row["laterRecords"] = r.Later.Select(l => Record((l.Commit, l.Path, l.Verdict), l.Tag, l.Versions)).ToList();
                if (r.Limited is { } limited)
                    row["knownLimitation"] = new Dictionary<string, string> { ["version"] = limited.Version, ["decided"] = limited.Decided, ["decision"] = limited.Decision };
                return row;
            }),
        };
        return JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true });
    }

    private static Dictionary<string, object> Record((string Commit, string Path, string Verdict) held, string tag, string[]? versions)
    {
        var record = new Dictionary<string, object> { ["commit"] = held.Commit, ["tag"] = tag, ["path"] = held.Path, ["verdict"] = held.Verdict };
        if (versions is not null) record["versions"] = versions;
        return record;
    }

    /// <summary>A Wrapper's project declaring MudBlazor at <paramref name="range"/>, as the Wrappers do.</summary>
    public static string WrapperProject(string range) =>
        $"""
        <Project Sdk="Microsoft.NET.Sdk.Razor">
          <ItemGroup>
            <PackageReference Include="MudBlazor" Version="{range}" />
          </ItemGroup>
        </Project>
        """;

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
            text.AppendLine($"| **{row.Name}** | {row.Where} | A check.{(row.Limited is { InTable: true } l ? $" *(Decided: a Known limitation of `{l.Version}`.)*" : "")} | {string.Join(", ", row.Paths.Select(p => $"`{p}`"))} |");
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
        held = held with { Record = held.Record ?? (coded, $"{RunDirectory}/report.md", "pass") };
        repo.Tag(held.Tag, held.Record!.Value.Commit);
        repo.Write($"{RunDirectory}/report.md", $"Verified commit: `{coded[..7]}`")
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
