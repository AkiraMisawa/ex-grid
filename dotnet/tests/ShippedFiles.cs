namespace ExGrid.Testing;

/// <summary>
/// A package's shipped files as the repository holds them (ADR-0123), for the tests of each
/// product's stylesheet component and notice: the version an address carries is the first twelve
/// hex digits of the SHA-256 of the file in <c>src/&lt;Package&gt;/wwwroot</c>, and a stamp is the
/// SHA-256 of its source in <c>src/&lt;Package&gt;/Assets</c>, both read with LF line endings as
/// <c>tools/assets/minify.mjs</c> hashes them.
/// </summary>
internal static class ShippedFiles
{
    private static readonly string Repository = FindRepository();

    /// <summary>The version the address of <paramref name="minified"/> carries.</summary>
    internal static string Version(string package, string minified)
        => Hash(File.ReadAllText(PathOf(package, minified)))[..12];

    /// <summary>Where <paramref name="minified"/> is served from in the checkout.</summary>
    internal static string PathOf(string package, string minified) => Path.Combine(Repository, "dotnet", "src", package, "wwwroot", minified);

    /// <summary>The stamp of <paramref name="source"/>: the SHA-256 of the file as written.</summary>
    internal static string Stamp(string package, string source)
        => Hash(File.ReadAllText(Path.Combine(Repository, "dotnet", "src", package, "Assets", source)));

    private static string Hash(string text) => Convert.ToHexStringLower(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal))));

    private static string FindRepository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "dotnet", "ExGrid.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException($"no dotnet/ExGrid.slnx above {AppContext.BaseDirectory}");
    }
}
