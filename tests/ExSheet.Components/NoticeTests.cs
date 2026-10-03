using System.Globalization;
using Bunit;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// ExSheet's notice (ADR-0048, ADR-0049): what it has to tell the user stands until the user's
/// own next action — an edit, a paste, a command, a selection change — and a Consumer's
/// programmatic change, such as a Linked Table's snapshot, does not clear it.
/// </summary>
public class NoticeTests : SheetTestContext
{
    private const string Unreadable = "is not a cell address";

    private static string Notice(IRenderedComponent<ExSheet> cut) => cut.Find(".ex-sheet-notice").TextContent;

    private static SheetDocument DocumentOf(params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        return sheet.ToDocument();
    }

    /// <summary>A Sheet at <paramref name="at"/> with a notice standing: an address the Name Box cannot read.</summary>
    private async Task<IRenderedComponent<ExSheet>> WithNoticeAsync(SheetDocument? document = null, string at = "B2")
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, document ?? DocumentOf()));
        await GoToAsync(cut, at);
        await GoToAsync(cut, "not an address");
        Assert.Contains(Unreadable, Notice(cut));
        return cut;
    }

    [Fact] // ADR-0049: a Linked Table's declaration and snapshot are the Consumer's, not the user's action; the notice stands
    public async Task A_linked_table_push_leaves_the_notice()
    {
        var cut = await WithNoticeAsync(DocumentOf(("A1", "=SUM(Positions[PV])")));

        await cut.Instance.DeclareLinkedTableAsync("Positions", ["Id", "PV"]);
        await cut.Instance.PushLinkedTableAsync("Positions", [[Value.FromText("R-1"), Value.FromNumber(100)]]);

        cut.WaitForAssertion(() => Assert.Equal("100", CellText(cut, "A1")));
        Assert.Contains(Unreadable, Notice(cut));
    }

    [Fact] // ADR-0048: a Sheet Document the Consumer hands in is a programmatic change; the notice stands
    public async Task A_document_from_the_consumer_leaves_the_notice()
    {
        var cut = await WithNoticeAsync();

        cut.Render(ps => ps.Add(s => s.Document, DocumentOf(("A1", "replaced"))));

        Assert.Equal("replaced", CellText(cut, "A1"));
        Assert.Contains(Unreadable, Notice(cut));
    }

    [Fact] // ADR-0048: a selection change is the user's own action, and clears the notice
    public async Task A_selection_change_clears_the_notice()
    {
        var cut = await WithNoticeAsync();

        await PressAsync(cut, "ArrowDown");

        cut.WaitForAssertion(() => Assert.Equal("", Notice(cut)));
    }

    [Fact] // ADR-0048: an edit is the user's own action, and clears the notice
    public async Task An_edit_clears_the_notice()
    {
        var cut = await WithNoticeAsync();

        await PressAsync(cut, "5");
        await TypeAsync(cut, "5");
        Assert.Contains(Unreadable, Notice(cut));
        await PressAsync(cut, "Enter");

        Assert.Equal("5", CellText(cut, "B2"));
        Assert.Equal("", Notice(cut));
    }

    [Fact] // ADR-0048: a paste is the user's own action, and clears the notice
    public async Task A_paste_clears_the_notice()
    {
        var cut = await WithNoticeAsync();

        var grid = Grid(cut);
        await grid.InvokeAsync(() => grid.Instance.OnPasteAsync("7\r\n", null));

        Assert.Equal("7", CellText(cut, "B2"));
        Assert.Equal("", Notice(cut));
    }

    [Fact] // ADR-0046, ADR-0048: a command — a Consumer's format command, an undo — is the user's own action, and clears the notice
    public async Task A_command_clears_the_notice()
    {
        var cut = await WithNoticeAsync(DocumentOf(("B2", "0.5")));

        Assert.True(await cut.Instance.SetNumberFormatAsync(NumberFormat.Parse("0%")));
        Assert.Equal("", Notice(cut));

        await GoToAsync(cut, "not an address");
        Assert.Contains(Unreadable, Notice(cut));
        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal("", Notice(cut));
    }

    [Fact] // ADR-0048: a refusal's notice is not cleared by the Consumer's snapshot that follows it
    public async Task A_refused_paste_stays_said_through_a_linked_table_push()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, DocumentOf(("A1", "=SUM(Positions[PV])"))));
        await cut.Instance.DeclareLinkedTableAsync("Positions", ["Id", "PV"]);
        await GoToAsync(cut, "B2");
        var grid = Grid(cut);
        await grid.InvokeAsync(() => grid.Instance.OnPasteAsync("########\r\n", null));
        Assert.Contains("source column was too narrow", Notice(cut));

        await cut.Instance.PushLinkedTableAsync("Positions", [[Value.FromText("R-1"), Value.FromNumber(100)]]);

        cut.WaitForAssertion(() => Assert.Equal("100", CellText(cut, "A1")));
        Assert.Contains("source column was too narrow", Notice(cut));
    }
}
