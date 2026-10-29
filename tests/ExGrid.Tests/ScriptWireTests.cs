using System.Buffers;
using System.Text;
using System.Text.Json;
using ExGrid.Cells;
using ExGrid.Script;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// ADR-0350 section 7: what Blazor's ExGrid keeps of the rows it sent the Script Core, and how it reads what the
/// bridge sends back. A cell value's text is the shared cases' (<c>Cases/WireValueCaseTests</c>).
/// </summary>
public class ScriptWireTests
{
    private sealed class Trade(string book, decimal notional)
    {
        public string Book { get; } = book;
        public decimal Notional { get; } = notional;
    }

    private static readonly GridColumn<Trade>[] Columns =
    [
        new("Book", ColumnType.Text, t => t.Book),
        new("Notional", ColumnType.Number, t => t.Notional, format: v => ((decimal)v).ToString("N2", System.Globalization.CultureInfo.InvariantCulture)),
    ];

    private static JsonElement Written(ScriptRows<Trade> rows, Func<Trade, object>? key, params Trade[] written)
        => Written(rows, key, null, written);

    private static JsonElement Written(ScriptRows<Trade> rows, Func<Trade, object>? key, CellStateOf<Trade>? cellState, params Trade[] written)
    {
        var text = rows.Sending(sequence =>
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var json = new Utf8JsonWriter(buffer))
            {
                json.WriteStartObject();
                json.WriteNumber("seq", sequence);
                json.WriteStartArray("rows");
                foreach (var row in written)
                    rows.Write(json, row, Columns, key, new RowPaint<Trade>(cellState, null, null, null, null), tableRow: null);
                json.WriteEndArray();
                json.WriteEndObject();
            }
            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        });
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static JsonElement Written(ScriptRows<Trade> rows, Trade row, Func<Trade, object>? key = null)
        => Written(rows, key, row).GetProperty("rows")[0];

    [Fact] // ADR-0003: the same row is the same id while it is sent, so the bridge keeps the object it made of it
    public void ADR0350_a_row_sent_again_crosses_with_the_same_id_and_another_with_its_own()
    {
        var rows = new ScriptRows<Trade>();
        var rates = new Trade("Rates", 1500m);
        var first = Written(rows, rates);
        var again = Written(rows, rates);
        var other = Written(rows, new Trade("Rates", 1500m));
        Assert.Equal(first.GetProperty("i").GetInt64(), again.GetProperty("i").GetInt64());
        Assert.NotEqual(first.GetProperty("i").GetInt64(), other.GetProperty("i").GetInt64());
        Assert.Equal("""{"i":1,"c":["Rates",1500],"t":[null,"1,500.00"]}""", first.GetRawText());
    }

    [Fact] // ED-35 / ADR-0006: a row crosses with its cells' states where any is not Normal, and with none where all are
    public void ADR0380_a_row_carries_its_cell_states_only_where_one_is_not_normal()
    {
        var rows = new ScriptRows<Trade>();
        CellStateOf<Trade> flagged = (trade, column) => trade.Book == "FX" && column.Name == "Notional" ? CellState.Error : CellState.Normal;

        var written = Written(rows, null, flagged, new Trade("Rates", 1m), new Trade("FX", 2m)).GetProperty("rows");

        Assert.False(written[0].TryGetProperty("s", out _));
        Assert.Equal("""["normal","error"]""", written[1].GetProperty("s").GetRawText());
    }

    [Fact] // ADR-0350 s7: a row the browser may still hand back is kept; one it let go, and not sent since, is not
    public void ADR0350_a_row_is_let_go_only_when_no_message_after_the_last_one_read_carries_it()
    {
        var rows = new ScriptRows<Trade>();
        var rates = new Trade("Rates", 1500m);
        var id = Written(rows, rates).GetProperty("i").GetInt64();
        Written(rows, rates);
        // The bridge had read message 1 when its object went: message 2 carries the row again.
        rows.Release([id], seen: 1);
        Assert.Same(rates, rows.RowOf(id));
        rows.Release([id], seen: 2);
        Assert.Equal(0, rows.Count);
        Assert.Throws<InvalidOperationException>(() => rows.RowOf(id));
    }

    [Fact] // ADR-0140: a row crosses with its key, and a key that does not cross is refused by name
    public void ADR0140_a_row_key_crosses_with_the_row_and_a_key_of_another_type_is_refused()
    {
        var rows = new ScriptRows<Trade>();
        var keyed = Written(rows, new Trade("Rates", 1m), t => t.Book);
        Assert.Equal("Rates", keyed.GetProperty("k").GetString());
        Assert.Throws<NotSupportedException>(() => Written(rows, new Trade("FX", 1m), t => (object)1.5));
    }

    [Fact] // ADR-0350 s7: a message that fails half written takes no number and keeps no row, so none is awaited
    public void ADR0350_a_message_that_fails_while_written_spends_no_number_and_keeps_no_row()
    {
        var rows = new ScriptRows<Trade>();
        var rates = new Trade("Rates", 1m);
        var first = Written(rows, null, rates);
        Assert.Equal(1, first.GetProperty("seq").GetInt64());
        // The second row's key does not cross: the first row of the message, new, is not kept, and the row sent
        // before is still marked by the message that did reach the browser.
        Assert.Throws<NotSupportedException>(() => Written(rows, t => t.Book == "FX" ? 1.5 : (object)t.Book, new Trade("Credit", 1m), rates, new Trade("FX", 1m)));
        Assert.Equal(1, rows.Count);
        var id = first.GetProperty("rows")[0].GetProperty("i").GetInt64();
        rows.Release([id], seen: 1);
        Assert.Equal(0, rows.Count);
        Assert.Equal(2, Written(rows, null, rates).GetProperty("seq").GetInt64());
    }

    [Theory] // spine rule 1: a name the bridge sends that no member answers is refused, never read as another
    [InlineData("\"tooLarge\"", true)]
    [InlineData("\"TooLarge\"", false)]
    [InlineData("\"tooLargeX\"", false)]
    [InlineData("\"0\"", false)]
    public void ADR0350_a_reason_is_read_by_its_exact_name(string json, bool read)
    {
        using var document = JsonDocument.Parse(json);
        if (read)
            Assert.Equal(Clipboard.CopyRefusalReason.TooLarge, ScriptJson.Member<Clipboard.CopyRefusalReason>(document.RootElement));
        else
            Assert.Throws<InvalidOperationException>(() => ScriptJson.Member<Clipboard.CopyRefusalReason>(document.RootElement));
    }

    [Fact] // ADR-0012: the Selection the bridge sends is the one the C# surface names, its Focus in its range
    public void ADR0350_a_selection_reads_back_with_its_focus_range()
    {
        using var document = JsonDocument.Parse("""
            {"ranges":[{"topRow":0,"leftColumn":0,"rowCount":2,"columnCount":2},{"topRow":5,"leftColumn":1,"rowCount":1,"columnCount":1}],
             "focus":{"row":5,"column":1},"focusRangeIndex":1}
            """);
        var selection = ScriptJson.Selection(document.RootElement);
        Assert.Equal(2, selection.Ranges.Count);
        Assert.Equal(new Selection.CellPosition(5, 1), selection.Focus);
        Assert.Equal(new Selection.SelectionRange(5, 1, 1, 1), selection.FocusRange);
        using var empty = JsonDocument.Parse("""{"ranges":[],"focus":null,"focusRangeIndex":0}""");
        Assert.True(ScriptJson.Selection(empty.RootElement).IsEmpty);
    }
}
