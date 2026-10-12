using ExGrid.Release;

// The release workflow's checks of a tag (ADR-0042, ADR-0205, ADR-0208). Run by
// .github/workflows/release.yml, and locally from the repository's root. Run from a directory inside
// the working tree, or given one with --repo, every check still reads its paths from the root:
//
//   dotnet run --project tools/release/ExGrid.Release -- admit --tag v0.1.0-rc.1 --commit HEAD
//   dotnet run --project tools/release/ExGrid.Release -- gate --tag v0.1.0-rc.1 --commit HEAD
//   dotnet run --project tools/release/ExGrid.Release -- by-hand --at HEAD
//
// WR-10's records are held to the MudBlazor versions nuget.org gives as published within the range the
// Wrappers declare before X's commit time, or with no date (2026-10-12): read from --mudblazor-index, nuget.org's
// registration index unless another address, or a saved copy of it with its pages inlined, is given. A
// list that cannot be read refuses the tag.
//
// Exit code 0 admits, 1 refuses, 2 is a usage error or a check that could not be made.

const string Usage = """
    Usage:
      admit   --tag <v-tag> --commit <rev> [--repo <dir>] [--remote <name>]
              Reads the tag, refuses a malformed version or a commit on neither main nor a release/*
              branch, and writes version, kind, long and site to $GITHUB_OUTPUT when it is set.
      gate    --tag <v-tag> --commit <rev> [--repo <dir>] [--remote <name>] [--fetch-missing]
              [--mudblazor-index <url or file>]
              For a stable version: the Signed off block, X a release candidate's commit, and the
              structural check (ADR-0205). For a stable version and a release candidate: the corpus
              check and the rows checked by hand (ADR-0208). A plain prerelease passes.
              --fetch-missing asks the remote for a record's verified/* tag this clone lacks.
              --mudblazor-index is where MudBlazor's published versions and their dates are
              listed, for WR-10's records (default: nuget.org's registration index).
      by-hand --at <rev> [--repo <dir>] [--remote <name>] [--fetch-missing]
              [--mudblazor-index <url or file>]
              Which rows checked by hand would hold if <rev> were signed off, and which need a run,
              reading the record index and the table from the working tree (ADR-0208).
    """;

if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
{
    Console.WriteLine(Usage);
    return args.Length == 0 ? 2 : 0;
}

var options = new Dictionary<string, string>(StringComparer.Ordinal);
var flags = new HashSet<string>(StringComparer.Ordinal);
for (var i = 1; i < args.Length; i++)
{
    if (!args[i].StartsWith("--", StringComparison.Ordinal))
    {
        Console.Error.WriteLine($"Unexpected argument '{args[i]}'.\n{Usage}");
        return 2;
    }
    if (args[i] == "--fetch-missing")
    {
        flags.Add(args[i]);
        continue;
    }
    if (i + 1 >= args.Length)
    {
        Console.Error.WriteLine($"{args[i]} needs a value.\n{Usage}");
        return 2;
    }
    options[args[i]] = args[++i];
}

string? Option(string name) => options.TryGetValue(name, out var value) ? value : null;

var git = new Git(Option("--repo") ?? Directory.GetCurrentDirectory()) { Remote = Option("--remote") ?? "origin" };
var report = new Report();
// Read only when WR-10's record otherwise holds, and then once.
var mudBlazor = new NuGetRegistration(Option("--mudblazor-index") ?? WrapperRange.Registration);

try
{
    switch (args[0])
    {
        case "admit":
        {
            if (Option("--tag") is not { } tag || Option("--commit") is not { } commit) break;
            var admission = Admission.Admit(git, tag, commit, report);
            report.WriteTo(Console.Out);
            if (admission is null || !report.Admitted) return 1;
            foreach (var line in admission.Outputs()) Console.WriteLine(line);
            if (Environment.GetEnvironmentVariable("GITHUB_OUTPUT") is { Length: > 0 } output)
                File.AppendAllLines(output, admission.Outputs());
            return 0;
        }
        case "gate":
        {
            if (Option("--tag") is not { } tag || Option("--commit") is not { } commit) break;
            var version = TagVersion.Read(tag, report);
            var full = git.Commit(commit);
            if (full is null) report.Refuse($"{commit} is not a commit of this repository.");
            if (version is not null && full is not null)
                Gate.Run(git, version, full, report, flags.Contains("--fetch-missing"), mudBlazor);
            report.WriteTo(Console.Out);
            return report.Admitted ? 0 : 1;
        }
        case "by-hand":
        {
            if (Option("--at") is not { } at) break;
            var full = git.Commit(at);
            if (full is null)
            {
                Console.Error.WriteLine($"{at} is not a commit of this repository.");
                return 2;
            }
            ByHand.Check(git, new DirectoryTree(git.Repository), full, report, flags.Contains("--fetch-missing"), mudBlazor);
            report.WriteTo(Console.Out);
            return report.Admitted ? 0 : 1;
        }
    }
}
catch (GitFailed e)
{
    report.WriteTo(Console.Out);
    Console.WriteLine($"::error::A check could not be made, so the tag is not admitted: {e.Message}");
    return 2;
}

Console.Error.WriteLine(Usage);
return 2;
