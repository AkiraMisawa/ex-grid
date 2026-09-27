using System.Globalization;
using Bunit;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// The Sheet Document between ExSheet and its Consumer (ADR-0048): raised after every change,
/// handed in to replace the Sheet, and recognised when it comes back.
/// </summary>
public class SheetDocumentWiringTests : SheetTestContext
{
    private static SheetDocument DocumentOf(CultureInfo culture, params (string Address, string Typed)[] cells)
    {
        var sheet = new Sheet(culture);
        foreach (var (address, typed) in cells) sheet.Enter(CellAddress.Parse(address), typed);
        return sheet.ToDocument();
    }

    [Fact] // ADR-0048: every change raises the Sheet Document — Entries, never Values
    public async Task Every_change_raises_the_sheet_document()
    {
        var raised = new List<SheetDocument>();
        var cut = RenderSheet(ps => ps.Add(s => s.DocumentChanged, raised.Add));

        await EnterAsync(cut, "A1", "2");
        await EnterAsync(cut, "B1", "=A1*21");

        Assert.Equal(2, raised.Count);
        var cells = raised[^1].Cells.ToDictionary(c => c.Address.ToString(), c => c.Entry!.ToString());
        Assert.Equal("=A1*21", cells["B1"]);
        Assert.DoesNotContain("42", raised[^1].ToJson());
    }

    [Fact] // ADR-0048, SH-17: a document handed in shows the same Values the engine alone computes
    public void A_document_handed_in_shows_the_engines_values()
    {
        var document = DocumentOf(CultureInfo.GetCultureInfo("en-US"), ("A1", "1,234.5"), ("A2", "=A1*2"));

        var cut = RenderSheet(ps => ps.Add(s => s.Document, document));

        var alone = Sheet.Open(SheetDocument.FromJson(document.ToJson()));
        Assert.Equal(alone.GetDisplay(CellAddress.Parse("A1")).Text, CellText(cut, "A1"));
        Assert.Equal(alone.GetDisplay(CellAddress.Parse("A2")).Text, CellText(cut, "A2"));
        Assert.Equal("2469", CellText(cut, "A2"));
    }

    [Fact] // ADR-0048: out, in, the same Values
    public async Task The_document_round_trips_through_the_component()
    {
        SheetDocument? raised = null;
        var first = RenderSheet(ps => ps.Add(s => s.DocumentChanged, d => raised = d));
        await EnterAsync(first, "A1", "3");
        await EnterAsync(first, "A2", "=A1+0.5");

        var second = RenderSheet(ps => ps.Add(s => s.Document, SheetDocument.FromJson(raised!.ToJson())));

        Assert.Equal("3.5", CellText(second, "A2"));
    }

    [Fact] // ADR-0048: a document the component raised, handed back (two-way binding), is the Sheet already open
    public async Task Handing_back_the_raised_document_changes_nothing()
    {
        SheetDocument? raised = null;
        var cut = RenderSheet(ps => ps.Add(s => s.DocumentChanged, d => raised = d));
        await EnterAsync(cut, "A1", "3");

        cut.Render(ps => ps.Add(s => s.Document, raised));

        Assert.True(cut.Instance.CanUndo);
        Assert.Equal("3", CellText(cut, "A1"));
    }

    [Fact] // ADR-0048: a Consumer that keeps handing in its first document does not lose the user's edits
    public async Task An_unchanged_document_parameter_does_not_reopen_the_sheet()
    {
        var document = DocumentOf(CultureInfo.GetCultureInfo("en-US"), ("A1", "1"));
        var cut = RenderSheet(ps => ps.Add(s => s.Document, document).Add(s => s.DocumentChanged, _ => { }));
        await EnterAsync(cut, "A1", "5");

        cut.Render(ps => ps.Add(s => s.Document, document));

        Assert.Equal("5", CellText(cut, "A1"));
    }

    [Fact] // ADR-0048: a different document replaces the Sheet
    public async Task A_different_document_replaces_the_sheet()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "A1", "5");

        cut.Render(ps => ps.Add(s => s.Document, DocumentOf(CultureInfo.GetCultureInfo("en-US"), ("B2", "hello"))));

        Assert.Equal("", CellText(cut, "A1"));
        Assert.Equal("hello", CellText(cut, "B2"));
    }

    [Fact] // ADR-0048: a document's culture is the Sheet's, whatever the Culture parameter says
    public void A_document_carries_its_own_culture()
    {
        var german = DocumentOf(CultureInfo.GetCultureInfo("de-DE"), ("A1", "1234,5"));

        var cut = RenderSheet(ps => ps.Add(s => s.Document, german));

        Assert.Equal("1234,5", CellText(cut, "A1"));
    }

    [Fact] // ADR-0048: the culture is the Sheet's own; changing it on a live Sheet is refused by name
    public void Changing_the_culture_of_a_live_sheet_is_refused()
    {
        var cut = RenderSheet();

        var error = Assert.Throws<InvalidOperationException>(() =>
            cut.Render(ps => ps.Add(s => s.Culture, CultureInfo.GetCultureInfo("ja-JP"))));
        Assert.Contains("en-US", error.Message);
    }
}
