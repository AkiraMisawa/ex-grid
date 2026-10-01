using System.Globalization;
using System.Text;
using global::ExGrid.Data;
using global::ExPivot.Engine;

namespace ExGrid.DemoPages;

/// <summary>
/// The /pivot-csv page's files: the demo trades — /pivot's, from the same seed — written as two
/// applications would export them, the Schema one of them is declared with, and the Pivot Fields
/// over the Snapshot it is read into. Everything is invented; the account numbers are derived from
/// the book names.
/// </summary>
public static class DemoCsv
{
    #region The code: schema
    // The trade export this application knows, declared: nothing is guessed. Its header names the
    // columns; one the Schema does not declare is skipped, and a declared one missing is refused.
    public static readonly CsvSchema TradeExport = new(
    [
        new CsvColumn("Id", SnapshotKind.Text),
        new CsvColumn("Account", SnapshotKind.Text),                     // "004417" stays "004417"
        new CsvColumn("Region", SnapshotKind.Text),
        new CsvColumn("Desk", SnapshotKind.Text),
        new CsvColumn("Book", SnapshotKind.Text),
        new CsvColumn("Product", SnapshotKind.Text),
        new CsvColumn("Currency", SnapshotKind.Text),
        new CsvColumn("TradeDate", SnapshotKind.Date) { Header = "Trade date", Caption = "Trade date", DateFormats = ["yyyy-MM-dd"] },
        new CsvColumn("Notional", SnapshotKind.Decimal) { ThousandsSeparator = "," },   // "1,250,000.00"
        new CsvColumn("Pnl", SnapshotKind.Decimal) { Header = "P&L", Caption = "P&L", ThousandsSeparator = "," },
        new CsvColumn("Quantity", SnapshotKind.Integer),
        new CsvColumn("Confirmed", SnapshotKind.Boolean),                // TRUE and FALSE, as Excel writes them
    ])
    {
        BlankText = ["NULL"],
        RecordKey = "Id",
    };

    // The Pivot Fields over the Snapshot's columns, by name. Month is a part of the trade date.
    public static readonly PivotField[] TradeFields =
    [
        new("Region", PivotFieldType.Text),
        new("Desk", PivotFieldType.Text),
        new("Book", PivotFieldType.Text),
        new("Account", PivotFieldType.Text),
        new("Product", PivotFieldType.Text),
        new("Currency", PivotFieldType.Text),
        new("TradeDate", PivotFieldType.Date, caption: "Trade date", format: "yyyy-MM-dd"),
        PivotField.DatePartOf("Month", column: "TradeDate", PivotDatePart.Month),
        new("Notional", PivotFieldType.Number, format: "#,##0.00"),
        new("Pnl", PivotFieldType.Number, caption: "P&L", format: "#,##0.00"),
        new("Quantity", PivotFieldType.Number),
        new("Confirmed", PivotFieldType.Boolean),
    ];
    #endregion

    /// <summary>The trade export's header row.</summary>
    public const string TradeExportHeader = "Id,Account,Region,Desk,Book,Product,Currency,Trade date,Notional,P&L,Quantity,Confirmed";

    /// <summary>The data record the malformed sample breaks, counted from one.</summary>
    public const int MalformedRow = 1234;

    private static readonly NumberFormatInfo DecimalComma = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    /// <summary>
    /// <paramref name="trades"/> demo trades as the trade export writes them, in UTF-8 with CR LF line
    /// ends: a comma between fields, ISO dates, money with a thousands separator and in quotes where it
    /// has one. With <paramref name="malformed"/>, the notional of data record <see cref="MalformedRow"/>
    /// has a letter O for its last zero, as a hand-edited file might.
    /// </summary>
    public static byte[] TradeExportFile(int trades, bool malformed = false)
        => Write(writer =>
        {
            writer.Write(TradeExportHeader + "\r\n");
            var row = 0;
            foreach (var t in DemoPivotData.Trades(count: trades))
            {
                row++;
                var notional = t.Notional.ToString("#,##0.00", CultureInfo.InvariantCulture);
                if (malformed && row == MalformedRow)
                    notional = notional[..^4] + "O" + notional[^3..];
                writer.Write(t.Id);
                writer.Write(',');
                writer.Write(AccountOf(t.Book));
                writer.Write(',');
                writer.Write(t.Region);
                writer.Write(',');
                writer.Write(t.Desk);
                writer.Write(',');
                writer.Write(t.Book);
                writer.Write(',');
                writer.Write(t.Product);
                writer.Write(',');
                writer.Write(t.Currency);
                writer.Write(',');
                writer.Write(t.TradeDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.Write(Quoted(notional));
                writer.Write(',');
                writer.Write(Quoted(t.Pnl.ToString("#,##0.00", CultureInfo.InvariantCulture)));
                writer.Write(',');
                writer.Write(t.Quantity.ToString(CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.Write(t.Confirmed ? "TRUE" : "FALSE");
                writer.Write("\r\n");
            }
        });

    /// <summary>
    /// The demo trades as a spreadsheet set to German saves them, a file this application has no
    /// Schema for: a semicolon between fields, dates day first, money with a decimal comma and a full
    /// stop between thousands.
    /// </summary>
    public static byte[] SpreadsheetFile(int trades)
        => Write(writer =>
        {
            writer.Write("Account;Desk;Book;Trade date;Notional;P&L\r\n");
            foreach (var t in DemoPivotData.Trades(count: trades))
            {
                writer.Write(AccountOf(t.Book));
                writer.Write(';');
                writer.Write(t.Desk);
                writer.Write(';');
                writer.Write(t.Book);
                writer.Write(';');
                writer.Write(t.TradeDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
                writer.Write(';');
                writer.Write(t.Notional.ToString("#,##0.00", DecimalComma));
                writer.Write(';');
                writer.Write(t.Pnl.ToString("#,##0.00", DecimalComma));
                writer.Write("\r\n");
            }
        });

    // UTF-8 without a byte-order mark, as the bytes a file would hold.
    private static byte[] Write(Action<StreamWriter> write)
    {
        using var bytes = new MemoryStream();
        using (var writer = new StreamWriter(bytes, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 1 << 16))
            write(writer);
        return bytes.ToArray();
    }

    // An invented account number with leading zeros, the same for every trade of a book.
    private static string AccountOf(string book)
    {
        var sum = 0;
        foreach (var c in book)
            sum = ((sum * 31) + c) % 9000;
        return (1000 + sum).ToString("000000", CultureInfo.InvariantCulture);
    }

    // RFC 4180: a field holding the separator is quoted.
    private static string Quoted(string field) => field.Contains(',', StringComparison.Ordinal) ? $"\"{field}\"" : field;
}
