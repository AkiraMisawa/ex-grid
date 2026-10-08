using ExGrid.Data;

namespace ExPivot.Engine;

/// <summary>Items labeled and ordered by the report's culture and registered Order Key policy.</summary>
/// <param name="Version">The displayed report's version.</param>
/// <param name="Field">The requested field.</param>
/// <param name="Search">The optional provider-side search.</param>
/// <param name="Max">The maximum Items returned.</param>
public sealed record PivotReportItemsQuery(PivotReportVersion Version, string Field, string? Search = null, int Max = 10_000);

/// <summary>A bounded, already ordered Items listing at one report version.</summary>
/// <param name="Version">The answered report.</param>
/// <param name="SourceVersion">Its source version.</param>
/// <param name="Items">The labeled Items in their declared order.</param>
/// <param name="Total">How many Items matched before the cap.</param>
/// <param name="Refusal">A named refusal, if any.</param>
public sealed record PivotReportItemsResult(PivotReportVersion Version, string SourceVersion,
    IReadOnlyList<PivotItemInfo> Items, int Total, PivotReportRefusal? Refusal = null);

/// <summary>An inclusive rectangular part of a report selection.</summary>
/// <param name="Top">First row.</param>
/// <param name="Left">First column among the query's Columns.</param>
/// <param name="Bottom">Last row.</param>
/// <param name="Right">Last column among the query's Columns.</param>
public sealed record PivotReportRange(int Top, int Left, int Bottom, int Right);

/// <summary>A versioned Copy request. Rectangles are returned separately, in request order.</summary>
/// <param name="Version">The report actually selected.</param>
/// <param name="Columns">Column names in the Consumer's current order.</param>
/// <param name="Ranges">The selected rectangles.</param>
/// <param name="MaxCells">The Consumer's execution cap; refusal never truncates.</param>
public sealed record PivotReportCopyQuery(PivotReportVersion Version, IReadOnlyList<string> Columns,
    IReadOnlyList<PivotReportRange> Ranges, long MaxCells = 1_000_000);

/// <summary>A copied cell, with shown text and locale-free raw text.</summary>
/// <param name="Text">The shown text.</param>
/// <param name="Raw">The raw text, or null for a blank.</param>
/// <param name="IsNumber">Whether raw text represents a number.</param>
public sealed record PivotReportCopyCell(string Text, string? Raw, bool IsNumber);

/// <summary>One copied rectangle, including the corresponding headers.</summary>
/// <param name="Headers">Headers in the rectangle's column order.</param>
/// <param name="Rows">All cells of its rows, without Window truncation.</param>
public sealed record PivotReportCopyBlock(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<PivotReportCopyCell>> Rows);

/// <summary>The Copy result, or an explicit refusal.</summary>
/// <param name="Version">The report answered.</param>
/// <param name="Blocks">One result per requested rectangle.</param>
/// <param name="Refusal">The refusal, if any.</param>
public sealed record PivotReportCopyResult(PivotReportVersion Version, IReadOnlyList<PivotReportCopyBlock> Blocks,
    PivotReportRefusal? Refusal = null);

/// <summary>A versioned Selection Summary; overlapping selected cells are counted once.</summary>
/// <param name="Version">The report actually selected.</param>
/// <param name="Columns">Selected column names in the Consumer's order.</param>
/// <param name="Ranges">The selected rectangles.</param>
/// <param name="FocusRow">The Focus row, for its number format.</param>
/// <param name="FocusColumn">The Focus column among Columns, for its number format.</param>
public sealed record PivotReportSummaryQuery(PivotReportVersion Version, IReadOnlyList<string> Columns,
    IReadOnlyList<PivotReportRange> Ranges, int? FocusRow = null, int? FocusColumn = null);

/// <summary>The summary's neutral aggregate parts; UI packages turn them into requested figures.</summary>
/// <param name="Version">The report answered.</param>
/// <param name="Counts">Value and number counts.</param>
/// <param name="Sum">The finished sum, exact wherever possible.</param>
/// <param name="Extremes">The extrema.</param>
/// <param name="HasError">An error was selected; numeric figures must be omitted.</param>
/// <param name="NumberFormat">The Focus value's number format, if any.</param>
/// <param name="CultureName">The explicitly selected report culture.</param>
/// <param name="Refusal">The refusal, if any.</param>
public sealed record PivotReportSummaryResult(PivotReportVersion Version, AggregateCounts Counts, AggregateSum Sum,
    AggregateExtremes Extremes, bool HasError, string? NumberFormat, string CultureName,
    PivotReportRefusal? Refusal = null);
