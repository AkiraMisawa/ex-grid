using System.Diagnostics;
using System.Text;

namespace ExGrid.Release;

/// <summary>What one git command answered.</summary>
public sealed record GitResult(int Code, string Out, string Error);

/// <summary>
/// The git commands the checks read a repository with. Every one runs in the repository given,
/// with paths unquoted and renames not detected, so a path git prints is the path as committed.
/// </summary>
public sealed class Git(string repository)
{
    /// <summary>The working tree, or any directory inside it.</summary>
    public string Repository { get; } = repository;

    /// <summary>The remote whose branches a tag must be on.</summary>
    public string Remote { get; init; } = "origin";

    /// <summary>Runs git with these arguments and returns what it answered, whatever its exit code.</summary>
    public GitResult Run(params string[] args)
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
        start.ArgumentList.Add(Repository);
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
    /// The paths that differ between two commits' trees, limited to <paramref name="paths"/> when given
    /// (a directory as <c>dir/</c>): <c>git diff --name-only</c>, which is empty exactly when
    /// <c>git diff --quiet</c> succeeds.
    /// </summary>
    public IReadOnlyList<string> ChangedPaths(string from, string to, IReadOnlyCollection<string>? paths = null)
    {
        var args = new List<string> { "--literal-pathspecs", "diff", "--name-only", "-z", "--no-renames", "--no-ext-diff", from, to };
        if (paths is not null)
        {
            if (paths.Count == 0) return [];
            args.Add("--");
            args.AddRange(paths);
        }
        return Out([.. args]).Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>The refs under <paramref name="prefix"/>, by full name.</summary>
    public IReadOnlyList<string> Refs(string prefix) =>
        Out("for-each-ref", "--format=%(refname)", prefix).Split('\n', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>The repository's tags, by name.</summary>
    public IReadOnlyList<string> Tags() =>
        Out("tag", "--list").Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();

    /// <summary>Asks the remote for a commit this clone does not have, by its full name. True when it is here after.</summary>
    public bool TryFetch(string commit)
    {
        Run("fetch", "--quiet", "--no-tags", "--no-recurse-submodules", Remote, commit);
        return Commit(commit) is not null;
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
