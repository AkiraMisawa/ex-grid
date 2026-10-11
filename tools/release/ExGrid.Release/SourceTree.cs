using System.Text;

namespace ExGrid.Release;

/// <summary>
/// The files a check reads: a commit's tree, which is what the release workflow checks, or a
/// working tree, which is what layer 1 checks. Paths are relative to the repository's root and
/// written with <c>/</c>; a directory is named with or without its trailing <c>/</c>.
/// </summary>
public interface ISourceTree
{
    /// <summary>How a message names this tree: a commit, or the working tree.</summary>
    string Name { get; }

    /// <summary>Whether a file is at <paramref name="path"/>.</summary>
    bool IsFile(string path);

    /// <summary>Whether a directory is at <paramref name="path"/>.</summary>
    bool IsDirectory(string path);

    /// <summary>The file's text as UTF-8, or null when no file is there.</summary>
    string? ReadText(string path);

    /// <summary>The files directly inside <paramref name="directory"/>, by their paths from the root, in ordinal order.</summary>
    IReadOnlyList<string> Files(string directory);
}

/// <summary>A commit's tree, read through git.</summary>
public sealed class CommitTree(Git git, string commit) : ISourceTree
{
    /// <summary>The commit, in full.</summary>
    public string Commit { get; } = commit;

    /// <inheritdoc />
    public string Name => $"commit {Commit[..Math.Min(12, Commit.Length)]}";

    /// <inheritdoc />
    public bool IsFile(string path) => git.TypeAt(Commit, path) == "blob";

    /// <inheritdoc />
    public bool IsDirectory(string path) => git.TypeAt(Commit, path) == "tree";

    /// <inheritdoc />
    public string? ReadText(string path)
    {
        if (!IsFile(path)) return null;
        return git.Out("cat-file", "blob", $"{Commit}:{path}");
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Files(string directory)
    {
        var dir = directory.TrimEnd('/') + "/";
        var result = git.Run("ls-tree", "-z", Commit, "--", dir);
        if (result.Code != 0) return [];
        var files = new List<string>();
        foreach (var entry in result.Out.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            // "<mode> <type> <object>\t<path>"
            var tab = entry.IndexOf('\t');
            var meta = entry[..tab].Split(' ');
            if (meta[1] == "blob") files.Add(entry[(tab + 1)..]);
        }
        files.Sort(StringComparer.Ordinal);
        return files;
    }
}

/// <summary>A directory on disk: the working tree as it stands, committed or not.</summary>
public sealed class DirectoryTree(string root) : ISourceTree
{
    /// <summary>The repository's root on disk.</summary>
    public string Root { get; } = Path.GetFullPath(root);

    /// <inheritdoc />
    public string Name => "the working tree";

    private string Full(string path) => Path.Combine(Root, path.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));

    /// <inheritdoc />
    public bool IsFile(string path) => File.Exists(Full(path));

    /// <inheritdoc />
    public bool IsDirectory(string path) => Directory.Exists(Full(path));

    /// <inheritdoc />
    public string? ReadText(string path) => IsFile(path) ? File.ReadAllText(Full(path), new UTF8Encoding(false)) : null;

    /// <inheritdoc />
    public IReadOnlyList<string> Files(string directory)
    {
        var full = Full(directory);
        if (!Directory.Exists(full)) return [];
        var dir = directory.TrimEnd('/');
        return Directory.EnumerateFiles(full)
            .Select(f => $"{dir}/{Path.GetFileName(f)}")
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}
