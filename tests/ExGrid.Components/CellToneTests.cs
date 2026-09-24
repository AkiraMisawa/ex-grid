using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Tone (ADR-0006/0029): the Consumer's rule on the column says what a value means, the
/// grid paints that as one interned class and nothing else, and the value's text, the
/// copy and the raw form are untouched by it.
/// </summary>
public class CellToneTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static CellTone Sign(object value) => (decimal)value switch
    {
        < 0 => CellTone.Negative,
        > 0 => CellTone.Positive,
        _ => CellTone.None,
    };

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(GridColumn<TestRow>[] columns, TestRow[]? rows = null, int pinned = 0)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows ?? TestRows.Window())
            .Add(g => g.Columns, columns)
            .Add(g => g.RowHeight, 20)
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.PinnedColumnCount, pinned));

    private static List<string> AmountClasses(IRenderedComponent<ExGrid<TestRow>> cut, int column = 1)
        => cut.FindAll(".ex-row").Select(r => r.QuerySelectorAll(".ex-cell")[column].ClassName ?? "").ToList();

    [Fact] // ADR-0006: the rule's answer reaches the DOM as the closed tone class, per row
    public void The_rule_paints_one_class_per_answer()
    {
        var cut = RenderGrid([
            new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
            new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, tone: Sign),
        ]);

        // 100.5, -7, 0: a gain, a loss, and nothing to say.
        Assert.Equal(
            ["ex-cell ex-cell-numeric ex-tone-positive", "ex-cell ex-cell-numeric ex-tone-negative", "ex-cell ex-cell-numeric"],
            AmountClasses(cut));
    }

    [Fact] // ADR-0006: without a rule nothing is derived — the grid takes no view on a sign
    public void Without_a_rule_a_negative_is_an_ordinary_cell()
    {
        var cut = RenderGrid([
            new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
            new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
        ]);

        Assert.All(AmountClasses(cut), c => Assert.Equal("ex-cell ex-cell-numeric", c));
    }

    [Fact] // ADR-0023: a Blank has no sign; the rule is never asked about a null
    public void A_null_value_is_not_offered_to_the_rule()
    {
        var asked = false;
        var cut = RenderGrid([
            new("Nothing", ColumnType.Number, _ => null, width: Fixed100, tone: _ => { asked = true; return CellTone.Negative; }),
        ]);

        Assert.All(cut.FindAll(".ex-cell"), c => Assert.Equal("ex-cell ex-cell-numeric", c.ClassName));
        Assert.False(asked);
    }

    [Fact] // ADR-0006: a tone composes with alignment, pinning and a Cell State — the state is last, so it outranks
    public void A_tone_composes_with_the_other_vocabularies()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, [new GridColumn<TestRow>("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, align: CellAlign.Left, tone: Sign)])
            .Add(g => g.RowHeight, 20)
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.PinnedColumnCount, 1)
            .Add(g => g.CellState, (CellStateOf<TestRow>)((row, _) => row.Amount < 0 ? CellState.Error : CellState.Normal)));

        Assert.Equal(
            ["ex-cell ex-cell-numeric ex-pinned ex-align-left ex-tone-positive",
             "ex-cell ex-cell-numeric ex-pinned ex-align-left ex-tone-negative ex-state-error",
             "ex-cell ex-cell-numeric ex-pinned ex-align-left"],
            AmountClasses(cut, column: 0));
    }

    [Fact] // ADR-0005: the tone changes no text — copy's both formats are what they were
    public async Task The_tone_leaves_copy_untouched()
    {
        var cut = RenderGrid([
            new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
            new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, tone: Sign),
        ]);
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 150, OffsetY = 30 });

        var payload = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());

        Assert.Equal("-7\r\n", payload.Text);
        Assert.Equal("<table><tr><td>-7</td></tr></table>", payload.Html);
    }

    [Fact] // ADR-0029: every composed class string is interned — the same combination is the same instance
    public void The_class_string_is_the_same_instance_every_time()
    {
        Assert.Same(
            CellClasses.For(true, false, CellState.Normal, CellAlign.Auto, CellTone.Negative),
            CellClasses.For(true, false, CellState.Normal, CellAlign.Auto, CellTone.Negative));
        Assert.Equal("ex-cell ex-tone-positive ex-state-stale", CellClasses.For(false, false, CellState.Stale, CellAlign.Auto, CellTone.Positive));
        Assert.Throws<ArgumentOutOfRangeException>(() => CellClasses.For(false, false, CellState.Normal, CellAlign.Auto, (CellTone)9));
    }
}
