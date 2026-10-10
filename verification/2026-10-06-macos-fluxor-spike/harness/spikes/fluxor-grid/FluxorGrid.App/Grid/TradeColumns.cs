using System.Globalization;
using ExGrid;
using ExGrid.Cells;
using ExGrid.Columns;
using FluxorGrid.Store;
using Microsoft.AspNetCore.Components;

namespace FluxorGrid.Grid;

/// <summary>
/// The blotter's columns, one reference-stable array for the app's life (ADR-0003), and what a
/// reducer needs of them: a cell's painted text, its raw text, and a typed write.
/// </summary>
public static class TradeColumns
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // Column positions, as the checks name them.
    public const int RendersColumn = 0;
    public const int IdColumn = 1;
    public const int BookColumn = 2;
    public const int CurrencyColumn = 3;
    public const int TradeDateColumn = 4;
    public const int NotionalColumn = 5;
    public const int PriceColumn = 6;
    public const int PnlColumn = 7;

    /// <summary>A Template cell holding a component that counts its row's renders: a row that
    /// skips its render (ADR-0003) does not touch it. Instrumentation only.</summary>
    private static readonly RenderFragment<TemplateCellContext<Trade>> Counter = context => builder =>
    {
        builder.OpenComponent<RenderCounter>(0);
        builder.AddComponentParameter(1, nameof(RenderCounter.Row), context.Row);
        builder.CloseComponent();
    };

    public static readonly GridColumn<Trade>[] All =
    [
        GridColumn<Trade>.TemplateColumn("Renders", ColumnType.Text, static _ => null, Counter, header: "R", width: Fixed(44)),
        new("Id", ColumnType.Text, t => t.Id, header: "Trade ID", width: Fixed(100)),
        new("Book", ColumnType.Text, t => t.Book, editable: true, width: Fixed(120)),
        new("Currency", ColumnType.Text, t => t.Currency, header: "Ccy", editable: true, width: Fixed(60)),
        new("TradeDate", ColumnType.Date, t => t.TradeDate, header: "Trade date", editable: true, width: Fixed(140), align: CellAlign.Left,
            format: v => ((DateTime)v).ToString("yyyy-MM-dd", Inv)),
        new("Notional", ColumnType.Number, t => t.Notional, editable: true, width: Fixed(150), format: Money),
        new("Price", ColumnType.Number, t => t.Price, editable: true, width: Fixed(100), format: v => ((decimal)v).ToString("0.0000", Inv)),
        new("Pnl", ColumnType.Number, t => t.Pnl, header: "P&L", width: Fixed(140), format: Money),
    ];

    /// <summary>The columns' query view, for W1's own sort (GridQueryEngine, the reference
    /// semantics of ADR-0023).</summary>
    public static readonly ColumnInfo<Trade>[] Infos = [.. All.Select(c => c.Info)];

    private static readonly Dictionary<string, GridColumn<Trade>> ByName = All.ToDictionary(c => c.Name);

    /// <summary>The value columns a change can show in: every column but the counter.</summary>
    public static readonly GridColumn<Trade>[] ValueColumns = [.. All.Where(c => c.PaintsValue)];

    public static GridColumn<Trade> Named(string name) => ByName[name];

    /// <summary>A cell's painted text, as the grid paints a value cell: the column's format
    /// (no PaintedText declaration here). What W1's reducer compares to mark a cell, and what a
    /// reducer compares to tell whether the store moved on under a write.</summary>
    public static string TextOf(Trade trade, string column) => ByName[column].Info.TextOf(trade);

    /// <summary>A trade's value as raw, locale-free text: what Ctrl+D and a fill write.</summary>
    public static string RawText(Trade trade, string column) => column switch
    {
        "Id" => trade.Id,
        "Book" => trade.Book,
        "Currency" => trade.Currency,
        "TradeDate" => trade.TradeDate.ToString("yyyy-MM-dd", Inv),
        "Notional" => trade.Notional.ToString(Inv),
        "Price" => trade.Price.ToString(Inv),
        "Pnl" => trade.Pnl.ToString(Inv),
        _ => "",
    };

    /// <summary>A new instance with one cell written (ADR-0003), or null where the text does not
    /// parse or the column is not editable. Null text clears a text cell; a number or a date has
    /// no blank in this model, so clearing one writes nothing.</summary>
    public static Trade? Written(Trade trade, string column, string? text) => column switch
    {
        "Book" => trade with { Book = text ?? "" },
        "Currency" => trade with { Currency = text ?? "" },
        "TradeDate" when text is not null && DateTime.TryParse(text, Inv, DateTimeStyles.None, out var date) => trade with { TradeDate = date.Date },
        "Notional" when text is not null && decimal.TryParse(text, NumberStyles.Number, Inv, out var notional) => trade with { Notional = notional },
        "Price" when text is not null && decimal.TryParse(text, NumberStyles.Number, Inv, out var price) => trade with { Price = price },
        _ => null,
    };

    public static object TradeKey(Trade trade) => trade.Id;

    private static ColumnWidthSpec Fixed(double width) => new(ColumnWidth.Fixed(width));

    private static string Money(object value) => ((decimal)value).ToString("#,##0.00", Inv);
}
