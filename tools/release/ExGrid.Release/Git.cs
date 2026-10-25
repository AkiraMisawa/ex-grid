using System.Diagnostics;
using System.Text;

namespace ExGrid.Release;

/// <summary>What one git command answered.</summary>
public sealed record GitResult(int Code, string Out, string Error);

/// <summary>
/// The git commands the checks read a repository with. Every one runs at the top of the working
/// tree, with paths unquoted and renames not detected, so a path git prints is the path as committed
/// and a path the checks give is read from the repository's root.
/// </summary>
public sealed class Git
{
    /// <summary>
    /// Reads the repository that <paramref name="directory"/> is in. The directory may be the top of
    /// the working tree or any directory inside it: the top is resolved once, here, and every command
    /// runs there. Run from a subdirectory, <c>git diff -- &lt;paths&gt;</c> and <c>git ls-tree</c>
    /// would read their paths from that subdirectory, match nothing, and let every row hold.
    /// </summary>
    public Git(string directory)
    {
        var top = Start(directory, ["rev-parse", "--show-toplevel"]);
        Repository = top.Code == 0 && top.Out.Trim() is { Length: > 0 } root ? root : directory;
    }

    /// <summary>
    /// The top of the working tree every command runs in; the directory given, when git found no
    /// working tree there, so that every command says why.
    /// </summary>
    public string Repository { get; }

    /// <summary>The remote whose branches a tag must be on.</summary>
    public string Remote { get; init; } = "origin";

    /// <summary>Runs git with these arguments and returns what it answered, whatever its exit code.</summary>
    public GitResult Run(params string[] args) => Start(Repository, args);

    private static GitResult Start(string directory, IEnumerable<string> args)
    {
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
        };
        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(directory);
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("core.quotepath=off");
        foreach (var arg in args) start.ArgumentList.Add(arg);
        // A terminal prompt for credentials would hang a check that is only reading.
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("git could not be started.");
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return new GitResult(process.ExitCode, output, error.Result);
    }

    /// <summary>Runs git and returns its output, or throws with what it said when it fails.</summary>
    public string Out(params string[] args)
    {
        var result = Run(args);
        if (result.Code != 0)
            throw new GitFailed($"git {string.Join(' ', args)} failed ({result.Code}): {result.Error.Trim()}");
        return result.Out;
    }

    /// <summary>The full name of the commit <paramref name="revision"/> names, or null when it names none here.</summary>
    public string? Commit(string revision)
    {
        var result = Run("rev-parse", "--verify", "--quiet", revision + "^{commit}");
        return result.Code == 0 ? result.Out.Trim() : null;
    }

    /// <summary>When <paramref name="commit"/> was committed, as its committer date gives it, in UTC.</summary>
    public DateTimeOffset CommitTime(string commit) =>
        DateTimeOffset.Parse(Out("show", "-s", "--format=%cI", commit + "^{commit}").Trim(), System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime();

    /// <summary>Whether <paramref name="ancestor"/> is <paramref name="descendant"/> or one of its ancestors.</summary>
    public bool IsAncestor(string ancestor, string descendant)
    {
        var result = Run("merge-base", "--is-ancestor", ancestor, descendant);
        return result.Code switch
        {
            0 => true,
            1 => false,
            _ => throw new GitFailed($"git merge-base --is-ancestor {ancestor} {descendant} failed ({result.Code}): {result.Error.Trim()}"),
        };
    }

    /// <summary>
    /// The paths that differ between two commits' trees, limited to <paramref name="paths"/> when given:
    /// <c>git diff --name-only</c>, which is empty exactly when <c>git diff --quiet</c> succeeds. A path is
    /// read as <see cref="Pathspec"/> writes it: a file, or a directory as <c>dir/</c>, literally; a
    /// <c>:(glob)</c> pattern as a glob.
    /// </summary>
    public IReadOnlyList<string> ChangedPaths(string from, string to, IReadOnlyCollection<string>? paths = null)
    {
        var args = new List<string> { "diff", "--name-only", "-z", "--no-renames", "--no-ext-diff", from, to };
        if (paths is not null)
        {
            if (paths.Count == 0) return [];
            args.Add("--");
            args.AddRange(paths.Select(Pathspec));
        }
        return Out([.. args]).Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// The files of <paramref name="commit"/>'s tree that <paramref name="path"/> names, read as
    /// <see cref="Pathspec"/> writes it: what the commit holds of a row's path, a glob's included.
    /// </summary>
    public IReadOnlyList<string> FilesAt(string commit, string path) =>
        Out("diff", "--name-only", "-z", "--no-renames", "--no-ext-diff", EmptyTree(), commit, "--", Pathspec(path))
            .Split('\0', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// How git is given a path the checks read: a <c>:(glob)</c> pattern as it is, where <c>*</c> stays
    /// within a directory and <c>**</c> crosses them; anything else as <c>:(literal)</c>, so that a
    /// character git would read as a wildcard is a character, and a directory written <c>dir/</c> names
    /// every file under it.
    /// </summary>
    public static string Pathspec(string path) =>
        path.StartsWith(GlobMagic, StringComparison.Ordinal) ? path : ":(literal)" + path;

    /// <summary>The magic a row's path may begin with, the one git pathspec magic the index takes.</summary>
    public const string GlobMagic = ":(glob)";

    private string? _emptyTree;

    // The empty tree's name in this repository's object format, which git knows without storing it.
    private string EmptyTree() => _emptyTree ??= Out("rev-parse", "--show-object-format").Trim() switch
    {
        "sha256" => "6ef19b41225c5369f1c104d45d8d85efa9b057b53b14b4b9b939dd74decc5321",
        _ => "4b825dc642cb6eb9a060e54bf8d69288fbee4904",
    };

    /// <summary>The refs under <paramref name="prefix"/>, by full name.</summary>
    public IReadOnlyList<string> Refs(string prefix) =>
        Out("for-each-ref", "--format=%(refname)", prefix).Split('\n', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>The repository's tags, by name.</summary>
    public IReadOnlyList<string> Tags() =>
        Out("tag", "--list").Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();

    /// <summary>The commit the tag <paramref name="tag"/> names, peeled, or null when this clone has no such tag.</summary>
    public string? TagCommit(string tag)
    {
        var result = Run("rev-parse", "--verify", "--quiet", $"refs/tags/{tag}^{{commit}}");
        return result.Code == 0 ? result.Out.Trim() : null;
    }

    /// <summary>
    /// Asks the remote for the tag <paramref name="tag"/>, which this clone does not have, and the commit it
    /// names. Returns that commit, or null when the remote has no such tag or could not be asked.
    /// </summary>
    public string? FetchTag(string tag)
    {
        Run("fetch", "--quiet", "--no-tags", "--no-recurse-submodules", Remote, $"refs/tags/{tag}:refs/tags/{tag}");
        return TagCommit(tag);
    }

    /// <summary>
    /// The text of the file at <paramref name="path"/> in <paramref name="commit"/>'s tree, its bytes as committed
    /// (<c>git cat-file blob</c>, which no attribute converts), or null when the tree holds no file there.
    /// </summary>
    public string? FileAt(string commit, string path)
    {
        if (TypeAt(commit, path) != "blob") return null;
        return Out("cat-file", "blob", $"{commit}:{path}");
    }

    /// <summary>The object type at <paramref name="path"/> in <paramref name="commit"/>'s tree: blob, tree, or null.</summary>
    public string? TypeAt(string commit, string path)
    {
        var result = Run("cat-file", "-t", $"{commit}:{path.TrimEnd('/')}");
        return result.Code == 0 ? result.Out.Trim() : null;
    }
}

/// <summary>A git command the checks needed failed, so the check could not be made.</summary>
public sealed class GitFailed(string message) : Exception(message);
