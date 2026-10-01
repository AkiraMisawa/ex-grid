using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GlyphWidths;

/// <summary>
/// One face as <c>tests/GlyphWidths/measure.mjs</c> measured it (ADR-0016; tickets 82 and 83):
/// what Chrome painted for every string in <c>corpus.json</c> and for every glyph they hold, at
/// each size and weight, in 64ths of a pixel rounded up. Compiled into each test project that
/// holds an estimate to it; the files are copied beside the test binary.
/// </summary>
internal sealed class GlyphWidthRecord
{
    public static readonly int[] Sizes = [14, 12];

    public static readonly int[] Weights = [400, 500, 600, 700];

    private static string Folder => Path.Combine(AppContext.BaseDirectory, "GlyphWidths");

    /// <summary>The strings ExSheet's built-in formats paint (ExSheet.Components'
    /// <c>GlyphCorpusTests</c> keeps it so), in the order every record lists them.</summary>
    public static IReadOnlyList<string> Corpus { get; } =
        JsonSerializer.Deserialize<string[]>(System.IO.File.ReadAllText(Path.Combine(Folder, "corpus.json")))!;

    /// <summary>The corpus's fingerprint, as a record states the corpus it measured.</summary>
    public static string CorpusSha256 { get; } =
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", Corpus))));

    /// <summary>The records of <paramref name="face"/>, one per platform it was measured on.</summary>
    public static IEnumerable<string> FilesOf(string face) =>
        Directory.GetFiles(Folder, face + ".*.json").Select(Path.GetFileName).Order(StringComparer.Ordinal)!;

    private static readonly Dictionary<string, GlyphWidthRecord> Loaded = [];

    public static GlyphWidthRecord Load(string file)
    {
        lock (Loaded)
        {
            if (!Loaded.TryGetValue(file, out var record))
                Loaded[file] = record = new GlyphWidthRecord(file);
            return record;
        }
    }

    private readonly Dictionary<(int Size, int Weight), int[]> _glyphs = [];
    private readonly Dictionary<(int Size, int Weight), int[]> _strings = [];

    private GlyphWidthRecord(string file)
    {
        File = file;
        using var json = JsonDocument.Parse(System.IO.File.ReadAllText(Path.Combine(Folder, file)));
        var root = json.RootElement;
        Face = root.GetProperty("face").GetString()!;
        UserAgent = root.GetProperty("userAgent").GetString()!;
        MeasuredCorpusSha256 = root.GetProperty("corpusSha256").GetString()!;
        Glyphs = [.. root.GetProperty("glyphs").EnumerateArray().Select(g => g.GetString()!)];
        var painters = root.GetProperty("painters");
        Painters = Weights.ToDictionary(w => w, w => (IReadOnlyList<string>)
            [.. painters.GetProperty(w.ToString()).EnumerateArray().Select(p => p.GetString()!)]);
        foreach (var size in Sizes)
        {
            var at = root.GetProperty("sizes").GetProperty(size.ToString());
            foreach (var weight in Weights)
            {
                _glyphs[(size, weight)] = [.. at.GetProperty("glyphs").GetProperty(weight.ToString()).EnumerateArray().Select(n => n.GetInt32())];
                _strings[(size, weight)] = [.. at.GetProperty("strings").GetProperty(weight.ToString()).EnumerateArray().Select(n => n.GetInt32())];
            }
        }
    }

    public string File { get; }

    public string Face { get; }

    public string UserAgent { get; }

    /// <summary>The fingerprint of the corpus this record measured; <see cref="CorpusSha256"/>
    /// when it is still the corpus.</summary>
    public string MeasuredCorpusSha256 { get; }

    /// <summary>Every glyph of the corpus, the Latin letters and every glyph
    /// <c>CellTextMetrics</c> names in a class, by code point.</summary>
    public IReadOnlyList<string> Glyphs { get; }

    /// <summary>The face that painted each glyph at each weight: the family, or a fallback.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<string>> Painters { get; }

    /// <summary>A glyph's width: the wider of the glyph alone and a hundred in a row, a
    /// hundredth of it.</summary>
    public double GlyphPx(int size, int weight, int index) => _glyphs[(size, weight)][index] / 64d;

    /// <summary>What corpus string <paramref name="index"/> painted.</summary>
    public double StringPx(int size, int weight, int index) => _strings[(size, weight)][index] / 64d;
}
