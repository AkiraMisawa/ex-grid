using System.Globalization;
using System.Text.Json.Nodes;
using ExGrid.Data;
using ExPivot.Engine;

namespace ExPivot.Engine.Tests.Cases;

/// <summary>
/// What the C# engine answers to a report source case (kind <c>pivotReportSource</c>;
/// <see cref="PivotReportSourceCaseTests"/> says what a case holds). A Window is adopted as a Consumer holds
/// one: its Window Changes, where the C# source sends them, are made against the Window it held, so a case
/// reads the whole Window either way. tools/pivot-oracle compiles it too.
/// </summary>
internal static class PivotReportSourceCases
{
    // The Consumer's clock and the source's, which a case moves on by hand.
    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    // A report source over a case's Pivot Source; every Window it answered, by its Report Version; the one
    // adopted last; what the next Window is asked under; and the versions it named, as the case writes them.
    private sealed class Run
    {
        public required PivotSourceCases.Bench Bench { get; init; }
        public required LocalPivotReportSource Reports { get; init; }
        public required ManualClock Clock { get; init; }
        public Dictionary<string, PivotReportState> States { get; } = new(StringComparer.Ordinal);
        public PivotReportState? Adopted { get; set; }
        public PivotLayout? Layout { get; set; }
        public JsonObject? Settings { get; set; }
        public required string Locale { get; set; }
        public required JsonNode Words { get; set; }
        public Dictionary<string, string> Tokens { get; } = new(StringComparer.Ordinal);
        public Dictionary<char, int> Counts { get; } = [];

        // A version as the case writes it: v1, v2… for Report Versions, s1… for Source Versions and r1… for
        // row sequences, in the order the source first named them.
        public string Token(char kind, string raw)
        {
            if (Tokens.TryGetValue(raw, out var token))
                return token;
            Counts[kind] = Counts.GetValueOrDefault(kind) + 1;
            return Tokens[raw] = $"{kind}{Counts[kind]}";
        }

        // The version a case's token names, or the token itself where no version was named so ("stale").
        public string Raw(string token) => Tokens.FirstOrDefault(pair => pair.Value == token).Key ?? token;

        // A message with every version it names written as its token.
        public string Saying(string message)
        {
            foreach (var (raw, token) in Tokens.OrderByDescending(pair => pair.Key.Length))
                message = message.Replace(raw, token, StringComparison.Ordinal);
            return message;
        }
    }

    /// <summary>The C# engine's answer to a report source case: each step's answer, or what it threw.</summary>
    public static async Task<JsonNode> Answer(JsonObject document, JsonObject entry, CancellationToken ct)
    {
        var bench = PivotSourceCases.Open(document, entry);
        var clock = new ManualClock();
        var orderKeys = entry["orderKeys"]?.AsObject().ToDictionary(k => k.Key, k => OrderKeyNamed((string)k.Value!), StringComparer.Ordinal);
        var kept = entry["versionsKept"] is { } count ? (int)count : 2;
        await using var reports = PivotReportSource.From(bench.Source, orderKeys, kept, clock);
        var run = new Run
        {
            Bench = bench,
            Reports = reports,
            Clock = clock,
            Settings = entry["settings"]?.AsObject(),
            Locale = (string)PivotCases.Inherited(document, entry, "locale")!,
            Words = PivotCases.Inherited(document, entry, "words"),
        };
        var steps = new JsonArray();
        foreach (var node in PivotCases.Must(entry, "asks").AsArray())
        {
            try
            {
                steps.Add(await Ask(run, node!.AsObject(), ct));
            }
            catch (Exception thrown) when (thrown is ArgumentException or InvalidOperationException)
            {
                var refusal = PivotCases.Refusal(thrown);
                refusal["saying"] = run.Saying((string)refusal["saying"]!);
                steps.Add(refusal);
            }
        }
        return steps;
    }

    // The Order Keys a case registers by a policy's name, each the Consumer's function the C# tests declared.
    private static Func<object, IComparable?> OrderKeyNamed(string name) => name switch
    {
        "longFirst" => item => -((string)item).Length,
        "throwsOnBB" => item => (string)item == "BB" ? throw new FormatException("no BB") : (string)item,
        _ => throw new InvalidOperationException($"'{name}' is not an Order Key the report source cases register."),
    };

    private static async Task<JsonNode> Ask(Run run, JsonObject step, CancellationToken ct)
    {
        if (step["window"] is { } window)
            return await Window(run, step, window.AsArray(), ct);
        if (step["apply"] is { } batch)
            return Apply(run, batch.AsObject());
        if (step["advance"] is { } ms)
        {
            run.Clock.Now += TimeSpan.FromMilliseconds((double)ms);
            return new JsonObject { ["advanced"] = (double)ms };
        }
        if (step["copy"] is { } copy)
            return await Copy(run, copy.AsObject(), ct);
        if (step["summary"] is { } summary)
            return await Summary(run, summary.AsObject(), ct);
        if (step["reportItems"] is { } items)
            return await Items(run, items.AsObject(), ct);
        if (step["details"] is { } details)
            return await Details(run, details.AsObject(), ct);
        if (step["detailsEach"] is { } each)
            return await DetailsEach(run, (string)each!, ct);
        throw new InvalidOperationException($"{step.ToJsonString()} asks nothing the report source cases ask.");
    }

    // ---- Windows ----------------------------------------------------------------------------------

    // A Window of the report the layout makes: the layout, the settings, the locale and the words carry over
    // from the step before where a step does not name them; the baseline is the Window adopted last unless the
    // step names one (null for none); a Window answered with "discard" is not adopted.
    private static async Task<JsonNode> Window(Run run, JsonObject step, JsonArray asked, CancellationToken ct)
    {
        if (step["layout"] is { } written)
            run.Layout = PivotCases.Layout(written);
        if (step["settings"] is { } settings)
            run.Settings = settings.AsObject();
        if (step["locale"] is { } locale)
            run.Locale = (string)locale!;
        if (step["words"] is { } words)
            run.Words = words;
        var layout = run.Layout ?? throw new InvalidOperationException("The first Window of a case names its layout.");
        var baseline = step.TryGetPropertyValue("baseline", out var named)
            ? named is null ? null : new PivotReportVersion(run.Raw((string)named!))
            : run.Adopted?.Metadata.Version;
        var request = new PivotReportRequest(Guid.NewGuid().ToString("N"), layout, Settings(run), new((int)asked[0]!, (int)asked[1]!),
            baseline, step["maxLeaves"] is { } max ? (int)max : PivotQuery.DefaultMaxLeaves)
        {
            MarkChanges = step["markChanges"] is not { } marks || (bool)marks,
            RefreshData = step["refreshData"] is { } refresh && (bool)refresh,
        };
        var update = await run.Reports.WindowAsync(request, ct);
        if (update.Refusal is { } refusal)
            return Refused(run, refusal);
        var metadata = update.Metadata!;
        var rows = update.Rows ?? Changed(run.States[update.Baseline!.Value], update.Changes);
        var state = new PivotReportState(metadata, update.Window, rows);
        run.States[metadata.Version.Value] = state;
        if (step["discard"] is not { } discard || !(bool)discard)
            run.Adopted = state;
        var also = step["also"] is { } readings ? readings.AsArray().Select(r => (string)r!).ToArray() : [];
        return WindowJson(run, state, also);
    }

    // The settings a Window is asked under: the case's locale (invariant, or a .NET culture name) and named
    // words, or those the settings give, with the settings' highlight, label geometry and Order Key policies.
    private static PivotReportSettings Settings(Run run)
    {
        var written = run.Settings ?? [];
        var locale = (string?)written["locale"] ?? run.Locale;
        var named = PivotCases.Words(run.Words);
        var settings = PivotReportSettings.From(new PivotOptions
        {
            Culture = locale == "invariant" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(locale),
            Label = named,
        });
        if (written["words"] is JsonObject own)
            settings = settings with { Words = own.ToDictionary(w => w.Key, w => (string)w.Value!, StringComparer.Ordinal) };
        if (written["changeHighlightMs"] is { } ms)
            settings = settings with { ChangeHighlightDuration = TimeSpan.FromMilliseconds((double)ms) };
        if (written["labelMetrics"] is JsonObject metrics)
            settings = settings with { LabelMetrics = Metrics(metrics) };
        if (written["orderKeyPolicies"] is JsonObject policies)
            settings = settings with { OrderKeyPolicies = policies.ToDictionary(p => p.Key, p => (string)p.Value!, StringComparer.Ordinal) };
        return settings;
    }

    private static PivotReportLabelMetrics Metrics(JsonObject written) => new(
        (double)written["wideWidthPx"]!, (double)written["digitWidthPx"]!, (double)written["narrowWidthPx"]!,
        (double)written["fullWidthPx"]!, (double)written["otherWidthPx"]!, (double)written["cellHorizontalPaddingPx"]!,
        written["glyphWidths"]?.AsObject().ToDictionary(g => int.Parse(g.Key, CultureInfo.InvariantCulture), g => (double)g.Value!));

    // The Window that Window Changes make of the one they were made against.
    private static PivotDisplayRow[] Changed(PivotReportState baseline, IReadOnlyList<PivotReportRowChange> changes)
    {
        var rows = baseline.Rows.ToArray();
        foreach (var change in changes)
            rows[change.Offset] = change.Row;
        return rows;
    }

    // A Window as a case reads it: its versions as tokens, the whole report's row count, its leaf headers, a
    // line a row, the changes still marked, each row's marks where any cell has one, the label widths where the
    // settings give a geometry, and the readings `also` asks for.
    private static JsonObject WindowJson(Run run, PivotReportState state, IReadOnlyList<string> also)
    {
        var metadata = state.Metadata;
        var read = new JsonObject
        {
            ["version"] = run.Token('v', metadata.Version.Value),
            ["sourceVersion"] = run.Token('s', metadata.SourceVersion),
            ["rowSequence"] = run.Token('r', metadata.RowSequenceVersion),
            ["rowCount"] = metadata.RowCount,
            ["headers"] = Strings(metadata.LabelColumns.Select(c => c.Header).Concat(metadata.ValueColumns.Select(c => c.Header))),
            ["lines"] = Strings(state.Rows.Select(Line)),
            ["changeMarks"] = Strings(metadata.ChangeMarks.Select(m => run.Token('v', m.Value))),
        };
        if (state.Rows.Any(r => r.ChangedIn.Any(m => m is not null)))
            read["changed"] = new JsonArray([.. state.Rows.Select(r => (JsonNode)new JsonArray([.. r.ChangedIn.Select(m => m is null ? null : JsonValue.Create(run.Token('v', m.Value)))]))]);
        if (metadata.LabelWidths.Count != 0)
            read["labelWidths"] = new JsonArray([.. metadata.LabelWidths.Select(w => JsonValue.Create(w))]);
        foreach (var more in also)
        {
            read[more] = more switch
            {
                "keys" => new JsonArray([.. state.Rows.Select(r => (JsonNode)new JsonObject
                {
                    ["role"] = PivotCases.Name(r.Role),
                    ["valueField"] = r.ValueField,
                    ["items"] = new JsonArray([.. r.Key.Items.Select(PivotCases.ItemKey)]),
                })]),
                "paths" => new JsonArray([.. state.Rows.Select(r => (JsonNode)Strings(r.RowPath.Select(Item)))]),
                "numbers" => new JsonArray([.. state.Rows.Select(Numbers)]),
                "columns" => new JsonArray([.. metadata.ValueColumns.Select(c => (JsonNode)new JsonObject
                {
                    ["name"] = c.Name,
                    ["role"] = PivotCases.Name(c.Role),
                    ["valueField"] = c.ValueField,
                    ["path"] = Strings(c.ColumnPath.Select(Item)),
                })]),
                "spans" => Strings(metadata.HeaderSpans
                    .OrderBy(s => -s.Tier).ThenBy(s => s.FirstColumn)
                    .Select(s => $"{s.Label}@{s.FirstColumn}+{s.ColumnCount} t{s.Tier}x{s.TierSpan}")),
                "captions" => Strings(metadata.ValueCaptions),
                "tiers" => JsonValue.Create(metadata.HeaderTierCount),
                _ => throw new InvalidOperationException($"'{more}' is not a reading of a Window the cases name."),
            };
        }
        return read;
    }

    // One line a row, as a report's line is read (PivotReadings).
    private static string Line(PivotDisplayRow row)
    {
        var role = row.Role switch
        {
            PivotRowRole.Item => "i",
            PivotRowRole.Group => "g",
            PivotRowRole.Subtotal => "s",
            _ => "t",
        };
        var labels = row.Labels.Select(label =>
            new string(' ', label.Indent * 2)
            + (label.Toggle is { } toggle ? toggle.IsCollapsed ? "[+]" : "[-]" : "")
            + (label.Text ?? ""));
        var values = row.Values.Select(v => v?.Text ?? "");
        return (role + " " + string.Join(" | ", labels) + " || " + string.Join(" | ", values)).TrimEnd();
    }

    // A row's numbers, as a report's are read (PivotReadings).
    private static JsonNode Numbers(PivotDisplayRow row) => new JsonObject
    {
        ["labels"] = new JsonArray([.. row.Labels.Select(l => l.Number is { } n ? JsonValue.Create(Double(n)) : null)]),
        ["values"] = new JsonArray([.. row.Values.Select(v => v switch
        {
            null => null,
            { Error: { } error } => JsonValue.Create(error),
            { Exact: { } exact } => JsonValue.Create(Exact(exact)),
            _ => JsonValue.Create(Double(v.Number)),
        })]),
    };

    // ---- Change Batches -----------------------------------------------------------------------------

    // A Change Batch applied to the Pivot Source over a Snapshot: the Source Version it made, or the
    // Snapshot's refusal.
    private static JsonObject Apply(Run run, JsonObject written)
    {
        if (run.Bench.Source is not SnapshotPivotSource source || run.Bench.Shape is not { } shape)
            throw new InvalidOperationException("A batch is applied to a source over a Snapshot.");
        Snapshot? Records(string role) => written[role] is JsonArray rows ? PivotCases.Rows(shape, rows, null) : null;
        var removed = written["removedKeys"]?.AsArray().Select(k => PivotCases.Value(k) is double d ? (object)checked((long)d) : PivotCases.Value(k)!).ToArray();
        try
        {
            source.Apply(ChangeBatch.Of(Records("added"), Records("changed"), removed));
            return new JsonObject { ["applied"] = run.Token('s', source.SourceVersion) };
        }
        catch (SnapshotException refused)
        {
            return PivotCases.Refusal(refused);
        }
    }

    // ---- Versioned operations -------------------------------------------------------------------------

    // A selection's columns: each by its index among the label columns and then the value columns of the
    // Window the version names (or of the one adopted last, for a version none was answered under), or by a
    // name of its own.
    private static string[] Columns(Run run, string version, JsonNode written)
    {
        var metadata = (run.States.TryGetValue(version, out var state) ? state : run.Adopted)?.Metadata
            ?? throw new InvalidOperationException("A selection is made on a Window answered before it.");
        var names = metadata.LabelColumns.Select(c => c.Name).Concat(metadata.ValueColumns.Select(c => c.Name)).ToArray();
        return [.. written.AsArray().Select(c => c is JsonValue value && value.TryGetValue<int>(out var index) ? names[index] : (string)c!)];
    }

    private static PivotReportRange[] Ranges(JsonNode written)
        => [.. written.AsArray().Select(r => new PivotReportRange((int)r![0]!, (int)r[1]!, (int)r[2]!, (int)r[3]!))];

    // A Copy: each block's headers, their raw forms where any has one, and each cell as its text, its raw
    // form and whether that is a number.
    private static async Task<JsonNode> Copy(Run run, JsonObject written, CancellationToken ct)
    {
        var version = run.Raw((string)written["version"]!);
        var query = new PivotReportCopyQuery(new(version), Columns(run, version, written["columns"]!), Ranges(written["ranges"]!),
            written["maxCells"] is { } max ? (long)max : 1_000_000);
        var result = await run.Reports.CopyAsync(query, ct);
        if (result.Refusal is { } refusal)
            return Refused(run, refusal);
        return new JsonObject
        {
            ["version"] = run.Token('v', result.Version.Value),
            ["blocks"] = new JsonArray([.. result.Blocks.Select(block =>
            {
                var json = new JsonObject { ["headers"] = Strings(block.Headers) };
                if (block.HeaderRaws is { } raws)
                    json["headerRaws"] = new JsonArray([.. raws.Select(r => r is null ? null : JsonValue.Create(r))]);
                json["rows"] = new JsonArray([.. block.Rows.Select(row => (JsonNode)new JsonArray([.. row.Select(cell => (JsonNode)new JsonArray(
                    JsonValue.Create(cell.Text), cell.Raw is null ? null : JsonValue.Create(cell.Raw), JsonValue.Create(cell.IsNumber)))]))]);
                return (JsonNode)json;
            })]),
        };
    }

    // A Selection Summary: Count and Numerical Count; the sum and extremes where a number was selected, each an
    // exact decimal ("m…") or a double ("d…"); whether an error was; the Focus value's format; the locale.
    private static async Task<JsonNode> Summary(Run run, JsonObject written, CancellationToken ct)
    {
        var version = run.Raw((string)written["version"]!);
        var focus = written["focus"]?.AsArray();
        var query = new PivotReportSummaryQuery(new(version), Columns(run, version, written["columns"]!), Ranges(written["ranges"]!),
            focus is null ? null : (int)focus[0]!, focus is null ? null : (int)focus[1]!);
        var result = await run.Reports.SummaryAsync(query, ct);
        if (result.Refusal is { } refusal)
            return Refused(run, refusal);
        var json = new JsonObject
        {
            ["version"] = run.Token('v', result.Version.Value),
            ["values"] = result.Counts.Values,
            ["numbers"] = result.Counts.Numbers,
        };
        if (result.Counts.Numbers > 0)
        {
            json["sum"] = result.Sum.Inexact ? Double(result.Sum.Double) : Exact(result.Sum.Exact);
            json["min"] = result.Extremes.Inexact ? Double(result.Extremes.Min) : Exact(result.Extremes.ExactMin);
            json["max"] = result.Extremes.Inexact ? Double(result.Extremes.Max) : Exact(result.Extremes.ExactMax);
        }
        json["hasError"] = result.HasError;
        if (result.NumberFormat is { } format)
            json["numberFormat"] = format;
        json["locale"] = result.CultureName.Length == 0 ? "invariant" : result.CultureName;
        return json;
    }

    // A field's Items as the report labels and orders them.
    private static async Task<JsonNode> Items(Run run, JsonObject written, CancellationToken ct)
    {
        var query = new PivotReportItemsQuery(new(run.Raw((string)written["version"]!)), (string)written["field"]!,
            (string?)written["search"], written["max"] is { } max ? (int)max : 10_000);
        var result = await run.Reports.ItemsAsync(query, ct);
        if (result.Refusal is { } refusal)
            return Refused(run, refusal);
        return new JsonObject
        {
            ["version"] = run.Token('v', result.Version.Value),
            ["sourceVersion"] = run.Token('s', result.SourceVersion),
            ["total"] = result.Total,
            ["items"] = new JsonArray([.. result.Items.Select(i => (JsonNode)new JsonObject
            {
                ["key"] = PivotCases.ItemKey(i.Key),
                ["label"] = i.Label,
                ["isHidden"] = i.IsHidden,
            })]),
        };
    }

    // The records behind a cell of a Window the case was answered: its row by its place in the report, and
    // its value column, −1 for its label.
    private static async Task<JsonNode> Details(Run run, JsonObject written, CancellationToken ct)
    {
        var state = run.States[run.Raw((string)written["version"]!)];
        var row = state.Rows[(int)written["row"]! - state.Window.Start];
        var query = state.Metadata.DetailsQuery(row, (int)written["column"]!,
            written["start"] is { } start ? (int)start : 0, written["count"] is { } count ? (int)count : int.MaxValue);
        var page = await run.Reports.DetailsAsync(query, ct);
        if (page.IsRefused)
            return new JsonObject { ["refused"] = PivotCases.Name(page.Refusal!.Kind), ["saying"] = run.Saying(page.Refusal.Message) };
        return new JsonObject
        {
            ["sourceVersion"] = run.Token('s', page.SourceVersion),
            ["start"] = page.Start,
            ["total"] = page.Total,
            ["fields"] = Strings(page.Fields.Select(f => f.Name)),
            ["records"] = new JsonArray([.. page.Records.Select(r => (JsonNode)new JsonArray([.. r.Values.Select(PivotSourceCases.DetailValue)]))]),
        };
    }

    // The question for the records behind every cell of a Window, resolved from its rows, and its records: a
    // line a cell, "column: the row's Items / the column's / the Hidden Items -> the total: each record".
    private static async Task<JsonNode> DetailsEach(Run run, string version, CancellationToken ct)
    {
        var state = run.States[run.Raw(version)];
        var lines = new JsonArray();
        foreach (var row in state.Rows)
        {
            var cells = new JsonArray();
            for (var column = -1; column < state.Metadata.ValueColumns.Count; column++)
            {
                var query = state.Metadata.DetailsQuery(row, column);
                var page = await run.Reports.DetailsAsync(query, ct);
                var hidden = query.HiddenItems.SelectMany(f => f.HiddenItems.Select(k => f.Field + "!=" + PivotCases.ItemKey(k).ToJsonString()));
                var records = page.Records.Select(r => new JsonArray([.. r.Values.Select(PivotSourceCases.DetailValue)]).ToJsonString());
                cells.Add($"{column}: {string.Join(", ", query.RowItems.Select(Item))} / {string.Join(", ", query.ColumnItems.Select(Item))}"
                    + $" / {string.Join(", ", hidden)} -> {page.Total}: {string.Join("; ", records)}");
            }
            lines.Add(cells);
        }
        return lines;
    }

    // ---- What a report source says ------------------------------------------------------------------

    // A refusal by its kind, the field it names, and its message with every version written as its token.
    private static JsonObject Refused(Run run, PivotReportRefusal refusal)
    {
        var json = new JsonObject { ["refused"] = PivotCases.Name(refusal.Kind) };
        if (refusal.Field is { } field)
            json["field"] = field;
        json["saying"] = run.Saying(refusal.Message);
        return json;
    }

    // An Item with its field, as a path writes it.
    private static string Item(PivotFieldItem item) => item.Field + "=" + PivotCases.ItemKey(item.Item).ToJsonString();

    private static string Exact(decimal exact) => "m" + exact.ToString(CultureInfo.InvariantCulture);

    private static string Double(double value) => "d" + value.ToString("R", CultureInfo.InvariantCulture);

    private static JsonArray Strings(IEnumerable<string> strings) => new([.. strings.Select(s => JsonValue.Create(s))]);
}
