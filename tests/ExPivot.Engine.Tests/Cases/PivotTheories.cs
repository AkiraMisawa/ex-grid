using Xunit;

namespace ExPivot.Engine.Tests.Cases;

/// <summary>The shared pivot cases as the rows of xUnit theories. The rest of reading a case is in
/// <see cref="PivotCases"/>, which tools/pivot-oracle compiles too, and so references no xUnit.</summary>
internal static class PivotTheories
{
    /// <summary>Every case of every file of the kind, as (file, name) rows a theory runs.</summary>
    public static TheoryData<string, string> Of(string kind)
    {
        var rows = new TheoryData<string, string>();
        foreach (var file in Directory.GetFiles(PivotCases.Root, "*.json").Select(Path.GetFileName).Order(StringComparer.Ordinal))
        {
            var document = PivotCases.Read(Path.Combine(PivotCases.Root, file!));
            if ((string?)document["kind"] != kind)
                continue;
            foreach (var entry in document["cases"]!.AsArray())
                rows.Add(file!, (string)entry!["name"]!);
        }
        return rows;
    }
}
