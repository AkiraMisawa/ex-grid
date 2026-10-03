namespace ExGrid.Components.Tests.Support;

/// <summary>
/// The sources of the JavaScript and CSS the packages ship (ADR-0123): <c>src/&lt;Package&gt;/Assets</c>.
/// What ships is their minified form in <c>wwwroot</c>, so a rule about how the code is written is read
/// where it is written; <c>ShippedAssetTests</c> holds each source and its minified form together.
/// </summary>
internal static class AssetSources
{
    /// <summary>The repository's root: the folder that holds <c>ExGrid.slnx</c>.</summary>
    internal static string Repository { get; } = FindRepository();

    /// <summary>The package's <c>Assets</c> folder.</summary>
    internal static string Folder(string package) => Path.Combine(Repository, "src", package, "Assets");

    /// <summary>One source's text.</summary>
    internal static string Read(string package, string file) => File.ReadAllText(Path.Combine(Folder(package), file));

    private static string FindRepository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExGrid.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException($"no ExGrid.slnx above {AppContext.BaseDirectory}");
    }
}
