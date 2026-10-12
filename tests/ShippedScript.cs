using System.Reflection;
using ExGrid.Components;
using Microsoft.AspNetCore.Components;

namespace ExGrid.Testing;

/// <summary>
/// What a test's stand-in for ExGrid's script answers to (ADR-0123): the address the grid imports it
/// at under bUnit, whose renderer knows no fingerprinted files, and the stamp the grid was built with,
/// which the module answers before the grid attaches anything. Read from the assembly under test, so
/// the stand-ins follow every build; whether those values are true of the shipped file is
/// ShippedAssetTests' question, which fails by name. Compiled into each test project that references
/// bUnit (tests/Directory.Build.targets).
/// </summary>
internal static class ShippedScript
{
    private static readonly Type Shipped = typeof(global::ExGrid.Components.ExGrid<>).Assembly.GetType("ExGrid.ShippedAssets", throwOnError: true)!;

    /// <summary>The address ExGrid imports its script at where the host knows no fingerprinted
    /// address: the plain path with the shipped file's version as <c>?v=</c>.</summary>
    internal static string Address { get; } =
        "./" + PackageStylesheet.AddressOf(ResourceAssetCollection.Empty, Constant("ScriptPath"), Constant("ScriptVersion"));

    /// <summary>The SHA-256 of the script's source this build of ExGrid was made with.</summary>
    internal static string Stamp { get; } = Constant("ScriptStamp");

    private static string Constant(string name)
        => (string)Shipped.GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;
}

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
        => Hash(File.ReadAllText(Path.Combine(Repository, "src", package, "wwwroot", minified)))[..12];

    /// <summary>The stamp of <paramref name="source"/>: the SHA-256 of the file as written.</summary>
    internal static string Stamp(string package, string source)
        => Hash(File.ReadAllText(Path.Combine(Repository, "src", package, "Assets", source)));

    private static string Hash(string text) => Convert.ToHexStringLower(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal))));

    private static string FindRepository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExGrid.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException($"no ExGrid.slnx above {AppContext.BaseDirectory}");
    }
}
