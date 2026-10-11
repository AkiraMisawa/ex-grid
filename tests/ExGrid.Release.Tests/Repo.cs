using System.Diagnostics;
using System.Text;
using ExGrid.Release;

namespace ExGrid.Release.Tests;

/// <summary>
/// A git repository a test makes in a directory of its own and deletes afterwards: files written,
/// commits made, tags and the remote-tracking branches a checkout would fetch set by hand. Every
/// git command runs with the user's and the system's configuration left out, so a signing key or a
/// default branch set on the machine cannot change what a test sees.
/// </summary>
public sealed class Repo : IDisposable
{
    public string Root { get; }

    public Git Git { get; }

    public Repo()
    {
        Root = Path.Combine(Path.GetTempPath(), "exgrid-release-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Run("init", "--quiet", "--initial-branch=main");
        Git = new Git(Root);
    }

    /// <summary>Writes a file, its directories made as needed.</summary>
    public Repo Write(string path, string text)
    {
        var full = Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text, new UTF8Encoding(false));
        return this;
    }

    /// <summary>Deletes a file.</summary>
    public Repo Delete(string path)
    {
        File.Delete(Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar)));
        return this;
    }

    /// <summary>Commits everything written, and returns the commit in full.</summary>
    public string Commit(string message = "A change")
    {
        Run("add", "--all");
        Run("commit", "--quiet", "--allow-empty", "-m", message);
        return Run("rev-parse", "HEAD").Trim();
    }

    /// <summary>Tags a commit.</summary>
    public Repo Tag(string name, string commit)
    {
        Run("tag", name, commit);
        return this;
    }

    /// <summary>Sets <c>refs/remotes/origin/&lt;branch&gt;</c>, as a checkout's fetch of every branch does.</summary>
    public Repo Remote(string branch, string commit)
    {
        Run("update-ref", $"refs/remotes/origin/{branch}", commit);
        return this;
    }

    /// <summary>Starts a branch at a commit and checks it out.</summary>
    public Repo Branch(string name, string commit)
    {
        Run("checkout", "--quiet", "-b", name, commit);
        return this;
    }

    public string Run(params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Root,
        };
        foreach (var setting in new[]
                 {
                     "user.name=Release Tests", "user.email=release-tests@example.invalid", "commit.gpgsign=false",
                     "tag.gpgsign=false", "core.autocrlf=false", "init.defaultBranch=main",
                 })
        {
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(setting);
        }
        foreach (var arg in args) start.ArgumentList.Add(arg);
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        start.Environment["GIT_CONFIG_GLOBAL"] = Path.Combine(Root, ".git-global-config-none");
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', args)}: {error.Result}");
        return output;
    }

    public void Dispose()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A temporary directory left behind is not the test's failure.
        }
    }
}
