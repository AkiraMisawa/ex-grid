using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExPivot.Oracle;

// The C# side of @ex-grid/pivot-engine's differential runs (ADR-0370, section 2).
//
//   cultures <file>   writes the culture table the TypeScript formats read
//   kana <file>       writes the ranks .NET's collation tailoring gives kana and width forms
//   casing <file>     writes .NET's simple upper case of every code point that has one
//   words <file>      transcribes ExPivot's words, English, English (United Kingdom) and Japanese
//   serve             answers one JSON request per line on stdin with one JSON line on stdout
//
// packages/pivot-engine/differential/oracle.ts starts `serve` and drives it (differential/README.md says how).
return args switch
{
    ["cultures", var file] => Cultures.Write(file),
    ["kana", var file] => Kana.Write(file),
    ["casing", var file] => Casing.Write(file),
    ["words", var file] => Words.Write(file),
    ["serve"] => Serve.Run(),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("usage: ExPivot.Oracle cultures <file> | kana <file> | casing <file> | words <file> | serve");
    return 2;
}

namespace ExPivot.Oracle
{
    internal static class Serve
    {
        public static int Run()
        {
            using var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = false };
            using var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
            while (input.ReadLine() is { } line)
            {
                if (line.Length == 0) continue;
                JsonNode answer;
                try
                {
                    answer = Answer(JsonNode.Parse(line)!.AsObject());
                }
                catch (Exception e)
                {
                    // Every exception is an answer, so the answers stay in step with the requests: one the
                    // Script Core does not throw is a difference the run reports by name, not a stalled run.
                    answer = new JsonObject { ["error"] = e.GetType().Name, ["message"] = e.Message };
                }
                output.WriteLine(answer.ToJsonString());
                output.Flush();
            }
            return 0;
        }

        private static JsonNode Answer(JsonObject request)
        {
            var op = (string)request["op"]!;
            return op switch
            {
                "number" => Text(FormatNumber(request)),
                "date" => Text(Ticks(request).ToString((string?)request["format"], Culture(request))),
                "general" => Text(General(request)),
                "compare" => new JsonObject
                {
                    ["ignoreCase"] = Math.Sign(Culture(request).CompareInfo.Compare(
                        (string)request["a"]!, (string)request["b"]!, CompareOptions.IgnoreCase)),
                },
                "upper" => Text(((string)request["text"]!).ToUpperInvariant()),
                "fold" => new JsonObject
                {
                    ["equal"] = string.Equals((string)request["a"]!, (string)request["b"]!, StringComparison.OrdinalIgnoreCase),
                },
                "escape" => Text(Uri.EscapeDataString((string)request["text"]!)),
                "case" => PivotCaseOracle.Answer(request),
                _ => throw new NotSupportedException($"No op '{op}'."),
            };
        }

        private static JsonObject Text(string text) => new() { ["text"] = text };

        private static CultureInfo Culture(JsonObject request)
        {
            var name = (string)request["culture"]!;
            return name == "invariant" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(name);
        }

        private static DateTime Ticks(JsonObject request) => new(long.Parse((string)request["ticks"]!, CultureInfo.InvariantCulture));

        private static string FormatNumber(JsonObject request)
        {
            var format = (string?)request["format"];
            var value = (string)request["value"]!;
            return (string)request["kind"]! switch
            {
                "double" => BitConverter.Int64BitsToDouble(long.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                    .ToString(format, Culture(request)),
                "decimal" => decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture).ToString(format, Culture(request)),
                var kind => throw new NotSupportedException($"No number kind '{kind}'."),
            };
        }

        private static string General(JsonObject request)
        {
            var value = (string)request["value"]!;
            return (string)request["kind"]! switch
            {
                "double" => ExPivot.Engine.PivotGeneral.Text(
                    BitConverter.Int64BitsToDouble(long.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)), Culture(request)),
                "decimal" => ExPivot.Engine.PivotGeneral.Text(
                    decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture), Culture(request)),
                var kind => throw new NotSupportedException($"No number kind '{kind}'."),
            };
        }
    }

    /// <summary>ExPivot.Engine's words (PivotWords, PivotDateWords) transcribed into TypeScript, every
    /// table in the order it is written.</summary>
    internal static class Words
    {
        public static int Write(string file)
        {
            static IReadOnlyDictionary<string, string> Table(Type type, string field)
                => (IReadOnlyDictionary<string, string>)(type.GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    ?? throw new InvalidOperationException($"{type.Name} has no table {field}.")).GetValue(null)!;
            var text = new StringBuilder();
            text.Append("""
                // Transcribed by dotnet/tools/pivot-oracle (`dotnet run --project dotnet/tools/pivot-oracle -- words <this file>`) from
                // ExPivot.Engine's PivotWords and PivotDateWords (ADR-0060): the words ExPivot paints, by id, in Excel's
                // English (United States) edition, the English (United Kingdom) edition's where they differ, and the
                // Japanese edition's. tests/cases/pivot/words holds both engines to them.


                """);
            void Emit(string name, string doc, IReadOnlyDictionary<string, string> table)
            {
                text.Append("/** ").Append(doc).Append(" */\n");
                text.Append("export const ").Append(name).Append(": Readonly<Record<string, string>> = {\n");
                foreach (var (id, word) in table)
                {
                    // An id is written between single quotes as it is: one that would need escaping is refused.
                    if (id.Any(c => c is '\'' or '\\' || char.IsControl(c)))
                        throw new InvalidOperationException($"The word id {JsonSerializer.Serialize(id)} cannot be written between single quotes.");
                    text.Append("    '").Append(id).Append("': ").Append(JsonSerializer.Serialize(word, Json)).Append(",\n");
                }
                text.Append("};\n\n");
            }
            Emit("englishWords", "The English of Excel's English (United States) edition, for every id.", Table(typeof(ExPivot.Engine.PivotWords), "English"));
            Emit("unitedKingdomWords", "The English (United Kingdom) edition's words, where they differ.", Table(typeof(ExPivot.Engine.PivotWords), "EnglishUnitedKingdomWords"));
            Emit("japaneseWords", "The Japanese edition's words.", Table(typeof(ExPivot.Engine.PivotWords), "JapaneseWords"));
            Emit("dateEnglishWords", "A date part's Items in English: the year, the quarter and each month.", Table(typeof(ExPivot.Engine.PivotDateWords), "English"));
            File.WriteAllText(file, text.ToString().TrimEnd() + "\n", new UTF8Encoding(false));
            return 0;
        }

        private static readonly JsonSerializerOptions Json = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
    }

    /// <summary>
    /// .NET's simple upper case, <c>ToUpperInvariant</c>'s, of every code point that has one, as runs: a
    /// JavaScript engine's own upper case is the full mapping (ᾳ to ΑΙ) of whatever Unicode version it
    /// carries, so the Script Core reads .NET's from this table and folds text alike in every browser.
    /// </summary>
    internal static class Casing
    {
        public static int Write(string file)
        {
            var mapped = new List<(int CodePoint, int Upper)>();
            for (var codePoint = 0x80; codePoint <= 0x10FFFF; codePoint++)
            {
                if (codePoint is >= 0xD800 and <= 0xDFFF)
                    continue;
                var text = char.ConvertFromUtf32(codePoint);
                var upper = text.ToUpperInvariant();
                if (upper == text)
                    continue;
                if (upper.EnumerateRunes().Count() != 1)
                    throw new InvalidOperationException($"U+{codePoint:X4} upper-cases to more than one code point.");
                mapped.Add((codePoint, char.ConvertToUtf32(upper, 0)));
            }

            // A run is [first, last, delta, step]: every step-th code point from first to last maps to
            // itself plus delta.
            var runs = new List<(int First, int Last, int Delta, int Step)>();
            var i = 0;
            while (i < mapped.Count)
            {
                var (first, upper) = mapped[i];
                var delta = upper - first;
                var step = i + 1 < mapped.Count && mapped[i + 1].Upper - mapped[i + 1].CodePoint == delta
                    && mapped[i + 1].CodePoint - first is 1 or 2 ? mapped[i + 1].CodePoint - first : 1;
                var last = first;
                var j = i + 1;
                while (j < mapped.Count && mapped[j].CodePoint == last + step && mapped[j].Upper - mapped[j].CodePoint == delta)
                {
                    last = mapped[j].CodePoint;
                    j++;
                }
                runs.Add((first, last, delta, step));
                i = j;
            }

            var written = new StringBuilder();
            written.Append("""
                // Generated by dotnet/tools/pivot-oracle (`dotnet run --project dotnet/tools/pivot-oracle -- casing <this file>`):
                // .NET's simple upper case, ToUpperInvariant's, of every code point from U+0080 that has one, so text
                // folds as .NET folds it whatever Unicode the JavaScript engine carries (casing.ts). Edit the
                // generator, not this file.

                /** Runs of [first, last, delta, step]: every step-th code point from first to last upper-cases to
                 * itself plus delta. Ordered by first. */
                export const upperRuns: readonly (readonly [number, number, number, number])[] = [

                """);
            foreach (var (first, last, delta, step) in runs)
                written.Append(CultureInfo.InvariantCulture, $"    [0x{first:X}, 0x{last:X}, {delta}, {step}],\n");
            written.Append("];\n");
            File.WriteAllText(file, written.ToString(), new UTF8Encoding(false));
            return 0;
        }
    }

    /// <summary>
    /// What .NET's collation adds to ICU's when it ignores case and nothing else: a katakana sorts after its
    /// hiragana, and a half-width or full-width form after its usual one, as a difference of the first
    /// strength (System.Globalization.Native's custom rules). Within each group of characters ICU's root
    /// gives one primary weight, each character's rank is its place in .NET's order: 0 for the first.
    /// </summary>
    internal static class Kana
    {
        public static int Write(string file)
        {
            var characters = new List<string>();
            void Range(int from, int to)
            {
                for (var c = from; c <= to; c++)
                    characters.Add(((char)c).ToString());
            }
            Range(0x21, 0x7E);
            Range(0xA2, 0xA6);
            Range(0x3001, 0x3002);
            Range(0x300C, 0x300D);
            Range(0x3041, 0x3096);
            Range(0x309B, 0x309C);
            Range(0x30A1, 0x30FC);
            Range(0x3131, 0x318E);
            Range(0xFF01, 0xFFEE);
            characters.RemoveAll(c => char.GetUnicodeCategory(c[0]) is UnicodeCategory.OtherNotAssigned);

            var compare = CultureInfo.InvariantCulture.CompareInfo;
            const CompareOptions Root = CompareOptions.IgnoreCase | CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth | CompareOptions.IgnoreNonSpace;
            // A primary difference under .NET's own rules: one that outranks a later letter's.
            int Primary(string x, string y)
            {
                var low = Math.Sign(compare.Compare(x + "a", y + "z", CompareOptions.IgnoreCase));
                var high = Math.Sign(compare.Compare(x + "z", y + "a", CompareOptions.IgnoreCase));
                return low == high ? low : 0;
            }

            var groups = new List<List<string>>();
            foreach (var c in characters)
            {
                var group = groups.FirstOrDefault(g => compare.Compare(g[0], c, Root) == 0);
                if (group is null)
                    groups.Add([c]);
                else
                    group.Add(c);
            }

            var text = new StringBuilder();
            text.Append("""
                // Generated by dotnet/tools/pivot-oracle (`dotnet run --project dotnet/tools/pivot-oracle -- kana <this file>`):
                // the rank .NET's collation tailoring gives each character in its group of one primary weight under
                // ICU's root, where the rank is not 0 (collation.ts). Edit the generator, not this file.

                /** Each tailored character, by code point, and its rank. */
                export const tailoredRanks: ReadonlyMap<number, number> = new Map([

                """);
            var written = 0;
            foreach (var group in groups.Where(g => g.Count > 1))
            {
                var ordered = group.ToList();
                ordered.Sort(Primary);
                var rank = 0;
                for (var i = 0; i < ordered.Count; i++)
                {
                    if (i > 0 && Primary(ordered[i - 1], ordered[i]) != 0)
                        rank++;
                    if (i > 0 && Primary(ordered[i - 1], ordered[i]) > 0)
                        throw new InvalidOperationException($"'{ordered[i - 1]}' and '{ordered[i]}' are out of order.");
                    if (rank == 0)
                        continue;
                    text.Append("    [0x").Append(((int)ordered[i][0]).ToString("X4", CultureInfo.InvariantCulture)).Append(", ")
                        .Append(rank.ToString(CultureInfo.InvariantCulture)).Append("], // ").Append(ordered[i]).Append(" after ").Append(ordered[0]).Append('\n');
                    written++;
                }
            }
            text.Append("]);\n");
            File.WriteAllText(file, text.ToString(), new UTF8Encoding(false));
            Console.Error.WriteLine($"{written} tailored characters in {groups.Count(g => g.Count > 1)} groups.");
            return 0;
        }
    }

    /// <summary>The cultures @ex-grid/pivot-engine formats in, as .NET holds them on this runtime
    /// (ICU's data, as a .NET server on Linux reads it).</summary>
    internal static class Cultures
    {
        // Gregorian cultures written in Latin digits, specific and neutral. A locale the table does
        // not hold is refused by name on the TypeScript side.
        private static readonly string[] Names =
        [
            "en", "en-US", "en-GB", "en-AU", "en-CA", "en-IE", "en-IN", "en-NZ", "en-SG", "en-ZA",
            "de", "de-DE", "de-AT", "de-CH", "fr", "fr-FR", "fr-BE", "fr-CA", "fr-CH",
            "es", "es-ES", "es-MX", "es-US", "it", "it-IT", "it-CH", "pt", "pt-BR", "pt-PT",
            "nl", "nl-NL", "nl-BE", "sv", "sv-SE", "da", "da-DK", "nb", "nb-NO", "fi", "fi-FI",
            "pl", "pl-PL", "cs", "cs-CZ", "sk", "sk-SK", "hu", "hu-HU", "ro", "ro-RO", "el", "el-GR",
            "ru", "ru-RU", "uk", "uk-UA", "tr", "tr-TR", "he", "he-IL", "id", "id-ID", "vi", "vi-VN",
            "ja", "ja-JP", "ko", "ko-KR", "zh", "zh-CN", "zh-HK", "zh-SG", "zh-TW", "zh-Hans", "zh-Hant",
        ];

        public static int Write(string file)
        {
            // A block or a list of names many cultures share is written once and named, so the table
            // holds each distinct convention once.
            var shared = new Dictionary<string, string>(StringComparer.Ordinal);
            var declarations = new StringBuilder();
            string Share(string prefix, JsonNode node)
            {
                var json = node.ToJsonString(Json);
                if (!shared.TryGetValue(json, out var name))
                {
                    name = prefix + shared.Count.ToString(CultureInfo.InvariantCulture);
                    shared.Add(json, name);
                    declarations.Append("const ").Append(name).Append(" = ").Append(json).Append(";\n");
                }
                return name;
            }

            var entries = new StringBuilder();
            void Add(string key, CultureInfo culture) => entries.Append("    ").Append(key).Append(": ").Append(Entry(culture, Share)).Append(",\n");
            Add("invariant", CultureInfo.InvariantCulture);
            foreach (var name in Names)
            {
                var culture = CultureInfo.GetCultureInfo(name);
                if (culture.Name != name)
                    throw new InvalidOperationException($"The culture '{name}' is named '{culture.Name}' on this runtime.");
                if (culture.Calendar is not GregorianCalendar)
                    throw new InvalidOperationException($"The culture '{name}' does not use the Gregorian calendar.");
                Add("'" + name + "'", culture);
            }

            var text = new StringBuilder();
            text.Append("""
                // Generated by dotnet/tools/pivot-oracle (`dotnet run --project dotnet/tools/pivot-oracle -- cultures <this file>`):
                // the number and date conventions of each culture @ex-grid/pivot-engine formats in, as .NET holds them
                // on Linux, ICU's data, which a .NET server pivots in. Edit the generator, not this file.

                import type { CultureData } from './culture-data';


                """);
            text.Append(declarations).Append('\n');
            text.Append("""
                /** The cultures the engine formats in, by their .NET names; `invariant` is .NET's InvariantCulture. */
                export const cultureTable: Readonly<Record<string, CultureData>> = {

                """);
            text.Append(entries).Append("};\n");
            File.WriteAllText(file, text.ToString(), new UTF8Encoding(false));
            return 0;
        }

        private static readonly JsonSerializerOptions Json = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private static string Entry(CultureInfo culture, Func<string, JsonNode, string> share)
        {
            var n = culture.NumberFormat;
            var d = culture.DateTimeFormat;
            var genitive = !d.MonthNames.SequenceEqual(d.MonthGenitiveNames)
                || !d.AbbreviatedMonthNames.SequenceEqual(d.AbbreviatedMonthGenitiveNames);
            var number = share("n", new JsonObject
            {
                ["decimal"] = n.NumberDecimalSeparator,
                ["group"] = n.NumberGroupSeparator,
                ["groupSizes"] = Ints(n.NumberGroupSizes),
                ["digits"] = n.NumberDecimalDigits,
                ["negativePattern"] = n.NumberNegativePattern,
                ["negativeSign"] = n.NegativeSign,
                ["positiveSign"] = n.PositiveSign,
                ["currencyDecimal"] = n.CurrencyDecimalSeparator,
                ["currencyGroup"] = n.CurrencyGroupSeparator,
                ["currencyGroupSizes"] = Ints(n.CurrencyGroupSizes),
                ["currencyDigits"] = n.CurrencyDecimalDigits,
                ["currencySymbol"] = n.CurrencySymbol,
                ["currencyPositivePattern"] = n.CurrencyPositivePattern,
                ["currencyNegativePattern"] = n.CurrencyNegativePattern,
                ["percentDecimal"] = n.PercentDecimalSeparator,
                ["percentGroup"] = n.PercentGroupSeparator,
                ["percentGroupSizes"] = Ints(n.PercentGroupSizes),
                ["percentDigits"] = n.PercentDecimalDigits,
                ["percentSymbol"] = n.PercentSymbol,
                ["perMilleSymbol"] = n.PerMilleSymbol,
                ["percentPositivePattern"] = n.PercentPositivePattern,
                ["percentNegativePattern"] = n.PercentNegativePattern,
                ["nan"] = n.NaNSymbol,
                ["positiveInfinity"] = n.PositiveInfinitySymbol,
                ["negativeInfinity"] = n.NegativeInfinitySymbol,
            });
            var date = new JsonObject
            {
                ["shortDate"] = d.ShortDatePattern,
                ["longDate"] = d.LongDatePattern,
                ["shortTime"] = d.ShortTimePattern,
                ["longTime"] = d.LongTimePattern,
                ["fullDateTime"] = d.FullDateTimePattern,
                ["monthDay"] = d.MonthDayPattern,
                ["yearMonth"] = d.YearMonthPattern,
                ["am"] = d.AMDesignator,
                ["pm"] = d.PMDesignator,
                ["dateSeparator"] = d.DateSeparator,
                ["timeSeparator"] = d.TimeSeparator,
                ["era"] = d.GetEraName(1),
                ["useGenitive"] = genitive,
            };
            var names = new[]
            {
                ("days", d.DayNames), ("abbreviatedDays", d.AbbreviatedDayNames), ("months", d.MonthNames[..12]),
                ("abbreviatedMonths", d.AbbreviatedMonthNames[..12]), ("genitiveMonths", d.MonthGenitiveNames[..12]),
                ("abbreviatedGenitiveMonths", d.AbbreviatedMonthGenitiveNames[..12]),
            };
            var entry = new StringBuilder("{ number: ").Append(number).Append(", date: { ...");
            entry.Append(share("d", date));
            foreach (var (key, list) in names)
                entry.Append(", ").Append(key).Append(": ").Append(share("a", Strings(list)));
            return entry.Append(" } }").ToString();
        }

        private static JsonArray Ints(int[] values) => new(values.Select(v => (JsonNode)v).ToArray());

        private static JsonArray Strings(string[] values) => new(values.Select(v => (JsonNode)v).ToArray());
    }
}
