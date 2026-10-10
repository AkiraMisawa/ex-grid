using System.Globalization;
using System.Text.Json.Serialization;

namespace ExPivot.Engine;

/// <summary>An opaque identity of one immutable report, including its layout and display settings.</summary>
/// <param name="Value">The report source's opaque identity.</param>
public sealed record PivotReportVersion(string Value);

/// <summary>The requested contiguous report rows; the complete report extent is metadata.</summary>
/// <param name="Start">The first row, zero based.</param>
/// <param name="Count">The maximum number of rows.</param>
public sealed record PivotReportWindow(int Start, int Count);

/// <summary>
/// How a report source learns that its data changed (ADR-0153), declared explicitly: one whose Pivot
/// Source can only announce that its data changed does not claim to update incrementally. Both use
/// the same Windows, Window Changes, versions and recovery; what differs is what a notice of newer
/// data asks.
/// </summary>
public enum PivotReportUpdateMode
{
    /// <summary>The Pivot Source identifies its changes — Change Batches, leaf changes — and the
    /// computation updates only what they affect. A notice of newer data asks for those changes.</summary>
    Incremental,
    /// <summary>The Pivot Source cannot identify its changes, and recomputes its complete aggregate
    /// result. A notice of newer data asks it to (<see cref="PivotReportRequest.RefreshData"/>), so
    /// a server whose Pivot Source cannot tell its data moved still answers with the newest; the
    /// browser still receives only the Window and its Window Changes.</summary>
    FullRefresh,
}

/// <summary>Serializable, explicitly selected display settings. Culture never selects words.</summary>
public sealed record PivotReportSettings
{
    /// <summary>Invariant numbers and English words.</summary>
    public static PivotReportSettings Invariant { get; } = new();
    /// <summary>How long data-change evidence must remain available for cells first requested after the change.</summary>
    public TimeSpan ChangeHighlightDuration { get; init; } = TimeSpan.FromSeconds(1);
    /// <summary>The .NET culture name; the empty name is invariant.</summary>
    public string CultureName { get; init; } = "";
    /// <summary>Resolved word overrides, by PivotWords or PivotDateWords identifier.</summary>
    public IReadOnlyDictionary<string, string> Words { get; init; } = new Dictionary<string, string>();
    /// <summary>The explicitly resolved label geometry, or null when widths are not requested.</summary>
    public PivotReportLabelMetrics? LabelMetrics { get; init; }
    /// <summary>Server-registered Order Key policy identifiers, by field name.</summary>
    public IReadOnlyDictionary<string, string> OrderKeyPolicies { get; init; } = new Dictionary<string, string>();
    /// <summary>Captures the options' words as values, without carrying a delegate over a transport.</summary>
    public static PivotReportSettings From(PivotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var words = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in PivotWords.Ids.Concat(PivotDateWords.Ids))
            if (options.Label?.Invoke(id) is { } word)
                words[id] = word;
        return new() { CultureName = options.Culture.Name, Words = words };
    }
    /// <summary>These explicit settings as local engine options.</summary>
    public PivotOptions ToOptions() => new()
    {
        Culture = CultureInfo.GetCultureInfo(CultureName),
        Label = id => Words.TryGetValue(id, out var word) ? word : null,
    };
}

/// <summary>A request identity, its report settings and the Window the Consumer currently needs.</summary>
/// <param name="RequestId">A new identity for each request; echoed by its response.</param>
/// <param name="Layout">The report's View State.</param>
/// <param name="Settings">Its explicit culture, words and registered policies.</param>
/// <param name="Window">The requested rows.</param>
/// <param name="Baseline">The Report Version of the Baseline Window — the Window the Consumer holds,
/// the data it shows — or null when it holds none. A source answers with Window Changes only against
/// it, and only when its Window is the requested Window; otherwise with a complete Window. A request
/// that marks no changes (a layout gesture) lays out the data of this version, when the source still
/// holds it, rather than newer data the Consumer has not been shown.</param>
/// <param name="MaxLeaves">The existing leaf cap.</param>
public sealed record PivotReportRequest(string RequestId, PivotLayout Layout, PivotReportSettings Settings,
    PivotReportWindow Window, PivotReportVersion? Baseline = null, int MaxLeaves = PivotQuery.DefaultMaxLeaves)
{
    /// <summary>Whether this gesture can mark data changes; layout and display-setting gestures cannot.</summary>
    public bool MarkChanges { get; init; } = true;
    /// <summary>Asks a full-refresh source's Pivot Source again even without a notification of its
    /// own: set by an explicit Refresh or Retry, and by a notice of newer data from a source that
    /// declared <see cref="PivotReportUpdateMode.FullRefresh"/>.</summary>
    public bool RefreshData { get; init; }
}

/// <summary>A detached value cell, including its exact value and shown text.</summary>
/// <param name="Number">The approximate number; zero for an error.</param>
/// <param name="Exact">The exact decimal where applicable.</param>
/// <param name="Error">An Excel error, or null.</param>
/// <param name="Text">The formatted text.</param>
public sealed record PivotDisplayValue(double Number, decimal? Exact, string? Error, string Text) : IFormattable
{
    /// <summary>Whether the cell is an error.</summary>
    [JsonIgnore] public bool IsError => Error is not null;
    /// <summary>The shown text.</summary>
    public override string ToString() => Text;
    /// <summary>The raw number when <paramref name="format"/> is null and <paramref name="formatProvider"/> is invariant.</summary>
    public string ToString(string? format, IFormatProvider? formatProvider)
        => Error ?? (Exact is { } exact ? exact.ToString(format, formatProvider) : Number.ToString(format, formatProvider));
    internal static PivotDisplayValue? From(PivotValue? value)
        => value is null ? null : new(value.Number, value.Exact, value.Error, value.Text);
}

/// <summary>A displayed row with no reference to a report, cube, source or branching axis tree.</summary>
public sealed class PivotDisplayRow
{
    /// <summary>Creates a row from detached values. The lists are copied.</summary>
    /// <param name="key">The row's value identity.</param>
    /// <param name="role">What the row stands for.</param>
    /// <param name="valueField">The Value Field in rows, or minus one.</param>
    /// <param name="carriesValues">Whether the row carries aggregate values.</param>
    /// <param name="labels">The label cells.</param>
    /// <param name="values">The value cells.</param>
    /// <param name="rowPath">The row's detached Item path.</param>
    /// <param name="changedIn">Per value cell, the Report Version whose data last changed its shown
    /// text, or null; null altogether when no cell changed recently.</param>
    [JsonConstructor]
    public PivotDisplayRow(PivotRowKey key, PivotRowRole role, int valueField, bool carriesValues,
        IReadOnlyList<PivotRowLabel> labels, IReadOnlyList<PivotDisplayValue?> values,
        IReadOnlyList<PivotFieldItem> rowPath, IReadOnlyList<PivotReportVersion?>? changedIn = null)
    {
        Key = key;
        Role = role;
        ValueField = valueField;
        CarriesValues = carriesValues;
        Labels = Array.AsReadOnly(labels.ToArray());
        Values = Array.AsReadOnly(values.ToArray());
        RowPath = Array.AsReadOnly(rowPath.ToArray());
        ChangedIn = Array.AsReadOnly(changedIn?.ToArray() ?? new PivotReportVersion?[values.Count]);
    }
    /// <summary>The row's value identity.</summary>
    public PivotRowKey Key { get; }
    /// <summary>What the row stands for.</summary>
    public PivotRowRole Role { get; }
    /// <summary>The Value Field in rows, or minus one.</summary>
    public int ValueField { get; }
    /// <summary>Whether the row carries aggregate values.</summary>
    public bool CarriesValues { get; }
    /// <summary>The label cells.</summary>
    public IReadOnlyList<PivotRowLabel> Labels { get; }
    /// <summary>The value cells, excluding label columns.</summary>
    public IReadOnlyList<PivotDisplayValue?> Values { get; }
    /// <summary>The row's detached Item path.</summary>
    public IReadOnlyList<PivotFieldItem> RowPath { get; }
    /// <summary>
    /// Per value cell, the change that last moved its shown text: the Report Version whose data
    /// changed it; null for a cell with no recent change, and for every cell of a report a layout
    /// gesture made. A cell keeps its mark while its text does not change, after the source stops
    /// listing the change (<see cref="PivotReportMetadata.ChangeMarks"/>), so that its row keeps
    /// its instance. It says which change, never when: the time a Change Highlight starts is the
    /// Consumer's, on its own clock, when it first shows a listed change (<see cref="PivotChangeTimes"/>)
    /// — a server's clock need not agree with the browser's (ADR-0068).
    /// </summary>
    public IReadOnlyList<PivotReportVersion?> ChangedIn { get; }
    /// <summary>A value cell by its value-column index.</summary>
    public PivotDisplayValue? ValueAt(int valueColumn) => Values[valueColumn];
}

/// <summary>A report value column with its detached Item path.</summary>
/// <param name="Name">The stable column name.</param>
/// <param name="Header">The shown leaf header.</param>
/// <param name="Role">Its role.</param>
/// <param name="ValueField">The Value Field, or minus one.</param>
/// <param name="ColumnPath">The column's Item path.</param>
public sealed record PivotDisplayColumn(string Name, string Header, PivotColumnRole Role, int ValueField,
    IReadOnlyList<PivotFieldItem> ColumnPath);

/// <summary>The whole report's extent and columns, without its rows or aggregate data.</summary>
/// <param name="Version">The immutable Report Version.</param>
/// <param name="SourceVersion">The Source Version the report was computed from.</param>
/// <param name="RowSequenceVersion">Changes when the ordered sequence of row identities changes.</param>
/// <param name="Layout">The accepted layout.</param>
/// <param name="Settings">The accepted explicit display settings.</param>
/// <param name="RowCount">The whole report row count, including rows outside the Window.</param>
/// <param name="LabelColumns">The label columns.</param>
/// <param name="ValueColumns">The detached value columns.</param>
/// <param name="HeaderSpans">The header groups.</param>
/// <param name="HeaderTierCount">The number of header tiers.</param>
/// <param name="ValueCaptions">The resolved Value Field captions.</param>
public sealed record PivotReportMetadata(PivotReportVersion Version, string SourceVersion, string RowSequenceVersion,
    PivotLayout Layout, PivotReportSettings Settings, int RowCount,
    IReadOnlyList<PivotLabelColumn> LabelColumns, IReadOnlyList<PivotDisplayColumn> ValueColumns,
    IReadOnlyList<PivotHeaderSpan> HeaderSpans, int HeaderTierCount, IReadOnlyList<string> ValueCaptions)
{
    /// <summary>Maximum label widths computed at the report source under the requested metrics.</summary>
    public IReadOnlyList<double> LabelWidths { get; init; } = [];
    /// <summary>The changes the source still marks, newest first: a cell marked with one of them
    /// changed recently, and a Consumer stamps each the first time it shows it
    /// (<see cref="PivotChangeTimes"/>). A change leaves the list once the source's evidence for it
    /// has aged past <see cref="PivotReportSettings.ChangeHighlightDuration"/>, and is never listed
    /// again.</summary>
    public IReadOnlyList<PivotReportVersion> ChangeMarks { get; init; } = [];
    /// <summary>The explicitly selected culture.</summary>
    [JsonIgnore] public CultureInfo Culture => CultureInfo.GetCultureInfo(Settings.CultureName);
    /// <summary>Whether the layout asks for no report.</summary>
    [JsonIgnore] public bool IsEmpty => Layout.Rows.Count == 0 && Layout.Columns.Count == 0 && Layout.Values.Count == 0;

    /// <summary>
    /// The question for the Source Records behind a cell of this report — Show Details (ADR-0063,
    /// ADR-0151) — resolved from the detached row and this report's own layout and columns: the
    /// row's Items, the value column's Items (none for a label cell), the Hidden Items the report
    /// was computed under, the range, and its <see cref="SourceVersion"/>, under which the records
    /// add up to the cell. Being made of values only, it stays the same question after any later
    /// layout, and is answered while the Pivot Source holds that Source Version.
    /// </summary>
    /// <param name="row">A row of this report's Window.</param>
    /// <param name="valueColumn">A value column's index, or −1 for the row's label cell.</param>
    /// <param name="start">The first record wanted.</param>
    /// <param name="count">How many records are wanted.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="valueColumn"/> is not a value
    /// column of this report, nor −1.</exception>
    public PivotDetailsQuery DetailsQuery(PivotDisplayRow row, int valueColumn, int start = 0, int count = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (valueColumn < -1 || valueColumn >= ValueColumns.Count)
            throw new ArgumentOutOfRangeException(nameof(valueColumn), valueColumn, "Not a value column of the report, nor -1.");
        if (row.Values.Count != ValueColumns.Count)
            throw new ArgumentException("The row is not a row of this report's columns.", nameof(row));
        var columnItems = valueColumn < 0 ? [] : ValueColumns[valueColumn].ColumnPath;
        var hidden = PivotQuery.For(Layout).Placed.Where(field => field.HiddenItems.Count > 0).ToArray();
        return new PivotDetailsQuery(SourceVersion, row.RowPath, columnItems, hidden, start, count);
    }
}

/// <summary>Why a report-source operation could not be answered.</summary>
public enum PivotReportRefusalKind
{
    /// <summary>The source no longer holds the Baseline Window the request names, so it cannot answer
    /// with Window Changes against it.</summary>
    BaselineNotHeld,
    /// <summary>A versioned operation's report is no longer available.</summary>
    ReportVersionNotHeld,
    /// <summary>A named Order Key policy is not registered.</summary>
    UnknownOrderKeyPolicy,
    /// <summary>The response does not satisfy its request or its structural contract.</summary>
    InvalidResponse,
    /// <summary>Window recovery failed; the previous report is stale.</summary>
    StaleReport,
    /// <summary>The Pivot Source refused the calculation.</summary>
    SourceRefused,
    /// <summary>The request itself cannot be answered.</summary>
    InvalidRequest,
    /// <summary>A field's Order Key — a registered policy's function on a server, or a local field's
    /// own — failed on an Item, or gave its Items keys of two types. The refusal names the field
    /// (<see cref="PivotReportRefusal.Field"/>) and the Item; the Items are never ordered by their
    /// labels instead (ADR-0060, ADR-0152).</summary>
    OrderKeyFailed,
}

/// <summary>A named refusal; an empty successful result is never used in its place.</summary>
/// <param name="Kind">The machine-readable reason.</param>
/// <param name="Message">The explanation suitable for a Consumer to present.</param>
/// <param name="Field">The offending field where relevant.</param>
public sealed record PivotReportRefusal(PivotReportRefusalKind Kind, string Message, string? Field = null)
{
    /// <summary>The Pivot Source's typed refusal, when it refused the computation.</summary>
    public PivotSourceRefusal? SourceRefusal { get; init; }
}

/// <summary>
/// A replacement at one zero-based position within the requested Window. Window Changes carry one
/// for every row of the Window whose key, labels or shown values differ from the Baseline Window's —
/// a subtotal or grand total row whose total moved, a row whose percentage moved with its
/// denominator, as much as the row whose data changed. A row left out keeps its old values on the
/// client; the <see cref="PivotReportUpdate.WindowDigest"/> the changes carry is what catches it.
/// </summary>
/// <param name="Offset">Its position within the Window.</param>
/// <param name="Row">The complete detached replacement row.</param>
public sealed record PivotReportRowChange(int Offset, PivotDisplayRow Row);

/// <summary>
/// One atomic report update: a complete Window, Window Changes from the requested Baseline Window —
/// the rows of the Window that changed since it, sent with the digest of the Window they make, in
/// place of the whole Window — or an explicit refusal (ADR-0152).
/// <para>
/// <b>Window Changes are verified, not trusted.</b> They name the digest of the Window they make
/// (<see cref="WindowDigest"/>, <see cref="PivotReportDigest"/>). The client applies them to the
/// Baseline Window it holds, computes the digest of the result and compares — before anything of
/// it is shown. Window Changes whose result differs, or that name no digest, are discarded, and a
/// complete current Window is asked for in their place; if that cannot be had, the last complete
/// report stays, as a Stale Report.
/// </para>
/// <para>
/// <b>Building Window Changes yourself.</b> A Consumer that builds Window Changes — a server that
/// does not run <see cref="LocalPivotReportSource"/>, a relay that coalesces or drops them, a
/// delegate that turns push messages into them — includes a change for every row of the requested
/// Window whose shown values changed, subtotals, grand totals and rows whose percentage changed
/// included, and computes the digest over the whole Window as it stands after the changes, from its
/// own complete copy of it: a digest computed over only the rows it sends, or by applying its own
/// changes, would match its own mistake. <see cref="LocalPivotReportSource"/> builds complete Window
/// Changes and their digests by construction; a relay passes them through untouched.
/// </para>
/// </summary>
/// <param name="RequestId">The echoed request identity.</param>
/// <param name="Window">The echoed Window identity.</param>
/// <param name="Metadata">The resulting whole-report metadata, absent on refusal.</param>
/// <param name="Baseline">The Report Version of the Baseline Window the changes apply to; null for a
/// complete Window.</param>
/// <param name="Rows">The complete Window, or null for Window Changes.</param>
/// <param name="Changes">The Window Changes: complete replacement rows for every row of the Window
/// whose shown values changed, never only the rows whose data did.</param>
/// <param name="Refusal">Why it was not answered.</param>
public sealed record PivotReportUpdate(string RequestId, PivotReportWindow Window, PivotReportMetadata? Metadata,
    PivotReportVersion? Baseline, IReadOnlyList<PivotDisplayRow>? Rows,
    IReadOnlyList<PivotReportRowChange> Changes, PivotReportRefusal? Refusal = null)
{
    /// <summary>
    /// The digest of the Window this update makes (<see cref="PivotReportDigest.Of"/>): for Window
    /// Changes, of the Baseline Window with the changes applied — every row of it, not only the rows
    /// changed — which the client checks before it shows anything of them, and without which it
    /// does not apply them; for a complete Window, of its rows, checked when present.
    /// </summary>
    public string? WindowDigest { get; init; }

    /// <summary>A complete Window, also used when a retained baseline has expired. Its digest is
    /// computed from <paramref name="rows"/>.</summary>
    public static PivotReportUpdate Complete(PivotReportRequest request, PivotReportMetadata metadata, IReadOnlyList<PivotDisplayRow> rows)
        => new(request.RequestId, request.Window, metadata, null, rows, [])
        {
            WindowDigest = PivotReportDigest.Of(metadata, request.Window.Start, rows),
        };

    /// <summary>
    /// Window Changes from the exact Baseline Window the request names. <paramref name="changes"/>
    /// replaces every row of the Window whose key, labels or shown values changed — subtotal and
    /// grand total rows, and rows whose percentage changed, included — and
    /// <paramref name="windowDigest"/> is the digest of the whole Window after them
    /// (<see cref="PivotReportDigest.Of"/>), computed from the source's own complete Window, never
    /// from the changes: the client refuses Window Changes whose result does not reproduce it.
    /// </summary>
    /// <param name="request">The request answered.</param>
    /// <param name="metadata">The resulting whole-report metadata.</param>
    /// <param name="changes">Every row of the Window that differs from the Baseline Window.</param>
    /// <param name="windowDigest">The digest of the whole Window after the changes.</param>
    public static PivotReportUpdate Changed(PivotReportRequest request, PivotReportMetadata metadata,
        IReadOnlyList<PivotReportRowChange> changes, string windowDigest)
    {
        ArgumentException.ThrowIfNullOrEmpty(windowDigest);
        return new(request.RequestId, request.Window, metadata, request.Baseline, null, changes) { WindowDigest = windowDigest };
    }

    /// <summary>An explicit refusal.</summary>
    public static PivotReportUpdate Refused(PivotReportRequest request, PivotReportRefusal refusal)
        => new(request.RequestId, request.Window, null, null, null, [], refusal);
}

/// <summary>A completely validated, immutable current Window.</summary>
/// <param name="Metadata">The whole report metadata.</param>
/// <param name="Window">The request's Window.</param>
/// <param name="Rows">Its detached displayed rows.</param>
public sealed record PivotReportState(PivotReportMetadata Metadata, PivotReportWindow Window, IReadOnlyList<PivotDisplayRow> Rows);
