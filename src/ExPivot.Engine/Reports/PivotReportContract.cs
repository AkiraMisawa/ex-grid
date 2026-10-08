using System.Globalization;
using System.Text.Json.Serialization;

namespace ExPivot.Engine;

/// <summary>An opaque identity of one immutable report, including its layout and display settings.</summary>
/// <param name="Value">The provider's opaque identity.</param>
public sealed record PivotReportVersion(string Value);

/// <summary>The requested contiguous report rows; the complete report extent is metadata.</summary>
/// <param name="Start">The first row, zero based.</param>
/// <param name="Count">The maximum number of rows.</param>
public sealed record PivotReportWindow(int Start, int Count);

/// <summary>How a report provider learns data changes (ADR-0153).</summary>
public enum PivotReportUpdateMode
{
    /// <summary>The provider can identify changes and update the affected calculation.</summary>
    Incremental,
    /// <summary>The provider explicitly recalculates after a data-changed notification.</summary>
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
/// <param name="Baseline">The Report Version the Consumer holds — the data it shows — or null when it
/// holds none. A source answers a delta only from it, and only when its Window is the requested
/// Window; otherwise a complete Window. A request that marks no changes (a layout gesture) lays out
/// the data of this version, when the source still holds it, rather than newer data the Consumer
/// has not been shown.</param>
/// <param name="MaxLeaves">The existing leaf cap.</param>
public sealed record PivotReportRequest(string RequestId, PivotLayout Layout, PivotReportSettings Settings,
    PivotReportWindow Window, PivotReportVersion? Baseline = null, int MaxLeaves = PivotQuery.DefaultMaxLeaves)
{
    /// <summary>Whether this gesture can mark data changes; layout and display-setting gestures cannot.</summary>
    public bool MarkChanges { get; init; } = true;
    /// <summary>Explicit Refresh/Retry asks a full-refresh provider again even without a notification.</summary>
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
    /// <summary>The raw number when format is null and the provider is invariant.</summary>
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
    /// changed it, while the source still marks that change (<see cref="PivotReportMetadata.ChangeMarks"/>);
    /// null for a cell with no recent change, and for every cell of a report a layout gesture made.
    /// It says which change, never when: the time a Change Highlight starts is the Consumer's, on its
    /// own clock, when it first shows the change (<see cref="PivotChangeTimes"/>) — a server's clock
    /// need not agree with the browser's (ADR-0068).
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
/// <param name="SourceVersion">The data version behind the report.</param>
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
    /// <summary>The changes the source still marks, newest first: every
    /// <see cref="PivotDisplayRow.ChangedIn"/> of this report names one of them. A change leaves the
    /// list once the source's evidence for it has aged past
    /// <see cref="PivotReportSettings.ChangeHighlightDuration"/>, and is never marked again.</summary>
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
    /// layout, and is answered while the data provider holds that Source Version.
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
    /// <summary>The server no longer retains a delta's baseline.</summary>
    BaselineNotHeld,
    /// <summary>A versioned operation's report is no longer available.</summary>
    ReportVersionNotHeld,
    /// <summary>A named Order Key policy is not registered.</summary>
    UnknownOrderKeyPolicy,
    /// <summary>The response does not satisfy its request or its structural contract.</summary>
    InvalidResponse,
    /// <summary>Window recovery failed; the previous report is stale.</summary>
    StaleReport,
    /// <summary>The underlying data provider refused the calculation.</summary>
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
    /// <summary>The provider's typed refusal, when computation was refused by the source.</summary>
    public PivotSourceRefusal? SourceRefusal { get; init; }
}

/// <summary>
/// A replacement at one zero-based position within the requested Window. A delta carries one for
/// every row of the Window whose key, labels or shown values differ from its baseline — a subtotal
/// or grand total row whose total moved, a row whose percentage moved with its denominator, as much
/// as the row whose data changed. A row left out keeps its old values on the client; the delta's
/// <see cref="PivotReportUpdate.WindowDigest"/> is what catches it.
/// </summary>
/// <param name="Offset">Its position within the Window.</param>
/// <param name="Row">The complete detached replacement row.</param>
public sealed record PivotReportRowChange(int Offset, PivotDisplayRow Row);

/// <summary>
/// One atomic report update: a complete Window, a delta from the requested baseline Window, or an
/// explicit refusal (ADR-0152).
/// <para>
/// <b>A delta is verified, not trusted.</b> It names the digest of the Window it produces
/// (<see cref="WindowDigest"/>, <see cref="PivotReportDigest"/>). The client applies the delta to
/// the Window it holds, computes the digest of the result and compares — before anything of it is
/// shown. A delta whose result differs, or that names no digest, is discarded, and a complete
/// current Window is asked for in its place; if that cannot be had, the last complete report stays,
/// as a Stale Report.
/// </para>
/// <para>
/// <b>Building deltas yourself.</b> A Consumer that builds deltas — a server that does not run
/// <see cref="LocalPivotReportSource"/>, a relay that coalesces or drops deltas, a delegate that
/// turns push messages into deltas — includes a change for every row of the requested Window whose
/// shown values changed, subtotals, grand totals and rows whose percentage changed included, and
/// computes the digest over the whole Window as it stands after the delta, from its own complete
/// copy of it: a digest computed over only the rows it sends, or by applying its own delta, would
/// match its own mistake. <see cref="LocalPivotReportSource"/> builds complete deltas and their
/// digests by construction; a relay passes them through untouched.
/// </para>
/// </summary>
/// <param name="RequestId">The echoed request identity.</param>
/// <param name="Window">The echoed Window identity.</param>
/// <param name="Metadata">The resulting whole-report metadata, absent on refusal.</param>
/// <param name="Baseline">The required baseline; null for a complete Window.</param>
/// <param name="Rows">The complete Window, or null for a delta.</param>
/// <param name="Changes">Complete replacement rows for a delta: every row of the Window whose
/// shown values changed, never only the rows whose data did.</param>
/// <param name="Refusal">Why it was not answered.</param>
public sealed record PivotReportUpdate(string RequestId, PivotReportWindow Window, PivotReportMetadata? Metadata,
    PivotReportVersion? Baseline, IReadOnlyList<PivotDisplayRow>? Rows,
    IReadOnlyList<PivotReportRowChange> Changes, PivotReportRefusal? Refusal = null)
{
    /// <summary>
    /// The digest of the Window this update produces (<see cref="PivotReportDigest.Of"/>): for a
    /// delta, of the baseline Window with the changes applied — every row of it, not only the
    /// rows changed — which the client checks before it shows anything of the delta, and without
    /// which it does not apply one; for a complete Window, of its rows, checked when present.
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
    /// A delta from the exact requested baseline. <paramref name="changes"/> replaces every row of
    /// the Window whose key, labels or shown values changed — subtotal and grand total rows, and rows
    /// whose percentage changed, included — and <paramref name="windowDigest"/> is the digest of
    /// the whole Window after them (<see cref="PivotReportDigest.Of"/>), computed from the source's
    /// own complete Window, never from the changes: the client refuses a delta whose result does
    /// not reproduce it.
    /// </summary>
    /// <param name="request">The request answered.</param>
    /// <param name="metadata">The resulting whole-report metadata.</param>
    /// <param name="changes">Every row of the Window that differs from the baseline.</param>
    /// <param name="windowDigest">The digest of the whole Window after the changes.</param>
    public static PivotReportUpdate Delta(PivotReportRequest request, PivotReportMetadata metadata,
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
