using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using ExSheet.Engine;
using ExSheet.Engine.Tests.Cases;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// What is .NET's own about computing a saved Sheet Document headless (ticket 17): the process culture, and the
/// assemblies the engine loads. The Values themselves are shared cases (<c>tests/cases/sheet/headless.json</c>).
/// </summary>
public class HeadlessTests
{
    /// <summary>
    /// A document as a Consumer would have saved it, Entries only and never a Value (ADR-0048): the one the shared
    /// cases open, read from their file so that there is one copy of it.
    /// </summary>
    private static readonly string Saved =
        (string)JsonNode.Parse(File.ReadAllText(Path.Combine(SheetCases.Root, "headless.json")))!["setups"]!["saved"]![0]!["reopen"]!["sheetJson"]!;

    [Theory] // ADR-0048 (SH-17): the same document gives the same Values whatever culture the process runs under
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("ja-JP")]
    [InlineData("")]
    public void The_process_culture_plays_no_part(string processCulture)
    {
        var reference = Picture(Sheet.Open(SheetDocument.FromJson(Saved)));
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(processCulture);
            Assert.Equal(reference, Picture(Sheet.Open(SheetDocument.FromJson(Saved))));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact] // ADR-0047 (SH-1): nothing the engine loads, directly or through what it references, is Blazor or ours
    public void The_engine_loads_nothing_of_blazor()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<AssemblyName>([typeof(Sheet).Assembly.GetName()]);
        while (pending.Count > 0)
        {
            var name = pending.Dequeue();
            if (!seen.Add(name.Name!)) continue;
            var assembly = Assembly.Load(name);
            foreach (var referenced in assembly.GetReferencedAssemblies()) pending.Enqueue(referenced);
        }

        Assert.DoesNotContain(seen, n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(seen, n => n.StartsWith("Microsoft.JSInterop", StringComparison.Ordinal));
        Assert.DoesNotContain(seen, n => n.StartsWith("ExGrid", StringComparison.Ordinal));
    }

    /// <summary>Every held cell's Value and displayed text.</summary>
    private static string Picture(Sheet sheet) =>
        string.Join("\n", sheet.EntryAddresses.Select(a => $"{a}={sheet.GetValue(a)}|{sheet.GetDisplay(a).Text}"));
}
