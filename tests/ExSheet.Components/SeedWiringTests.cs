using System.Globalization;
using Bunit;
using ExSheet.Components.Tests.Support;
using ExGrid;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// The Sheet Seed through the component (ADR-0180, docs/specs/exsheet-functions ticket 15, SH-66,
/// SH-17): ExSheet takes the Consumer's <c>Seed</c>, or chooses one when it opens a Sheet Document;
/// the count of changes starts from 0 there, every change draws again, and the document records
/// neither seed nor draw. Whole draws are read, so the painted text is the Value's in full.
/// </summary>
public class SeedWiringTests : SheetTestContext
{
    private const ulong Seed = 20261011;

    private static readonly string[] Drawn = ["A1", "A2", "A3", "B1"];

    private static SheetDocument Draws()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.Enter(CellAddress.Parse("A1"), "=RANDBETWEEN(1,1000000)");
        sheet.Enter(CellAddress.Parse("A2"), "=RANDBETWEEN(1,1000000)");
        sheet.Enter(CellAddress.Parse("A3"), "=ROUND(RAND()*1000000,0)");
        sheet.Enter(CellAddress.Parse("B1"), "=A1+A2");
        return sheet.ToDocument();
    }

    private static string[] Shown(IRenderedComponent<Components.ExSheet> cut) => [.. Drawn.Select(a => CellText(cut, a))];

    /// <summary>What the engine alone shows for the document at the seed and count of changes given (SH-17).</summary>
    private static string[] Alone(SheetDocument document, ulong seed, long changes)
    {
        var alone = Sheet.Open(SheetDocument.FromJson(document.ToJson()));
        alone.SetSeed(seed, changes);
        return [.. Drawn.Select(a => alone.GetDisplay(CellAddress.Parse(a)).Text)];
    }

    [Fact] // ADR-0180, SH-66, SH-17: a Seed given shows the same draws whenever the document is opened, and the engine alone shows them too
    public void ADR0180_a_seed_given_draws_the_same_whenever_the_document_is_opened()
    {
        var first = RenderSheet(ps => ps.Add(s => s.Document, Draws()).Add(s => s.Seed, Seed));
        var second = RenderSheet(ps => ps.Add(s => s.Document, Draws()).Add(s => s.Seed, Seed));

        Assert.Equal(Shown(first), Shown(second));
        Assert.Equal(Alone(Draws(), Seed, 0), Shown(first));
        Assert.Equal(int.Parse(CellText(first, "A1"), CultureInfo.InvariantCulture) + int.Parse(CellText(first, "A2"), CultureInfo.InvariantCulture),
            int.Parse(CellText(first, "B1"), CultureInfo.InvariantCulture));
    }

    [Fact] // ADR-0180, SH-66, SH-17: each edit draws again, and the engine alone, given the seed and the count of changes, shows the same Values
    public async Task ADR0180_each_edit_draws_again_and_the_engine_alone_follows()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Draws()).Add(s => s.Seed, Seed));
        var opened = Shown(cut);

        await EnterAsync(cut, "D1", "1");
        var once = Shown(cut);
        await EnterAsync(cut, "D2", "2");
        var twice = Shown(cut);

        Assert.NotEqual(opened, once);
        Assert.NotEqual(once, twice);
        Assert.Equal(Alone(cut.Instance.ToDocument(), Seed, 2), twice);
    }

    [Fact] // ADR-0180: an undo is a change like any other, and draws again; a change of format draws nothing
    public async Task ADR0180_an_undo_draws_again_and_a_format_does_not()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Draws()).Add(s => s.Seed, Seed));
        await EnterAsync(cut, "D1", "1");
        var edited = Shown(cut);

        await cut.InvokeAsync(() => cut.Instance.DoAsync(SheetEdit.SetNumberFormat(CellRange.Parse("C1"), NumberFormat.Parse("0.00"))));
        Assert.Equal(edited, Shown(cut));

        // The format's undo is no change either; the edit's undo is the second change since opening.
        await cut.InvokeAsync(() => cut.Instance.UndoAsync());
        Assert.Equal(edited, Shown(cut));
        await cut.InvokeAsync(() => cut.Instance.UndoAsync());

        Assert.Equal(Alone(Draws(), Seed, 2), Shown(cut));
    }

    private static Task PasteAsync(IRenderedComponent<Components.ExSheet> cut, string text, string? html = null)
    {
        var grid = Grid(cut);
        return grid.InvokeAsync(() => grid.Instance.OnPasteAsync(text, html));
    }

    private static async Task CtrlClickAsync(IRenderedComponent<Components.ExSheet> cut, string address)
    {
        var at = CellAddress.Parse(address);
        var heading = double.Parse(cut.Find(".ex-row-heading").GetAttribute("style")!.Replace("width:", "").Replace("px", "").Trim(), CultureInfo.InvariantCulture);
        await cut.Find(".ex-viewport").MouseDownAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs
        {
            Button = 0, Buttons = 1, CtrlKey = true,
            OffsetX = heading + at.Column * SheetColumns.DefaultWidthPx + 5,
            OffsetY = at.Row * Components.ExSheet.DefaultRowHeightPx + 5,
        });
        await cut.Find(".ex-viewport").MouseUpAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { Button = 0 });
    }

    [Fact] // ADR-0180, ADR-0048: typed fields and exact numbers pasted together are one change, and its undo and its redo one each
    public async Task ADR0180_a_paste_of_typed_fields_and_exact_numbers_is_one_change()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Draws()).Add(s => s.Seed, Seed));
        await GoToAsync(cut, "D1");

        await PasteAsync(cut, "", "<table><tr><td x:num=\"1.0000000000000002\">1</td><td>=D1*2</td></tr></table>");
        Assert.Equal("2", CellText(cut, "E1"));
        Assert.Equal(Alone(cut.Instance.ToDocument(), Seed, 1), Shown(cut));

        await cut.InvokeAsync(() => cut.Instance.UndoAsync());
        Assert.Equal(Alone(Draws(), Seed, 2), Shown(cut));
        await cut.InvokeAsync(() => cut.Instance.RedoAsync());
        Assert.Equal(Alone(cut.Instance.ToDocument(), Seed, 3), Shown(cut));
    }

    [Fact] // ADR-0180, ADR-0048, ADR-0014: the Sheet's own copy of one cell pasted over several ranges is one change, and its undo and its redo one each
    public async Task ADR0180_a_paste_over_several_ranges_is_one_change()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Draws()).Add(s => s.Seed, Seed));
        await EnterAsync(cut, "D1", "=ROW()");
        await GoToAsync(cut, "D1");
        var payload = Grid(cut).Instance.BuildCopyPayload();
        await GoToAsync(cut, "D2");
        await CtrlClickAsync(cut, "F2");
        await CtrlClickAsync(cut, "F4");

        await PasteAsync(cut, payload.Text!, payload.Html);
        // The own copy, its Reference-free Formula written into each range.
        Assert.Equal("4", CellText(cut, "F4"));
        Assert.Equal(Alone(cut.Instance.ToDocument(), Seed, 2), Shown(cut));

        await cut.InvokeAsync(() => cut.Instance.UndoAsync());
        Assert.Equal("", CellText(cut, "F4"));
        Assert.Equal(Alone(cut.Instance.ToDocument(), Seed, 3), Shown(cut));
        await cut.InvokeAsync(() => cut.Instance.RedoAsync());
        Assert.Equal(Alone(cut.Instance.ToDocument(), Seed, 4), Shown(cut));
    }

    [Fact] // ADR-0180, ADR-0048: a refused paste is no change, so it draws nothing, though the fields before the refused one were read
    public async Task ADR0180_a_refused_paste_draws_nothing()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Draws()).Add(s => s.Seed, Seed));
        var opened = Shown(cut);
        await GoToAsync(cut, "D1");

        await PasteAsync(cut, "", "<table><tr><td x:num=\"1.0000000000000002\">1</td><td>=D1*2</td><td class=xl65>#####</td></tr></table>");

        Assert.Equal("", CellText(cut, "D1"));
        Assert.False(cut.Instance.CanUndo);
        Assert.Equal(opened, Shown(cut));
        await EnterAsync(cut, "G1", "1");
        Assert.Equal(Alone(cut.Instance.ToDocument(), Seed, 1), Shown(cut));
    }

    [Fact] // ADR-0180: a Sheet Document handed over again is opened again, its count of changes from 0, so a fixed seed shows its opening draws
    public async Task ADR0180_a_document_opened_again_counts_its_changes_from_zero()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Draws()).Add(s => s.Seed, Seed));
        var opened = Shown(cut);
        await EnterAsync(cut, "D1", "1");
        Assert.NotEqual(opened, Shown(cut));

        cut.Render(ps => ps.Add(s => s.Document, Draws()));

        Assert.Equal(opened, Shown(cut));
    }

    [Fact] // ADR-0180, ADR-0122: a prerender chooses no seed, so its draws wait rather than show numbers the interactive page would replace with nothing done; a Seed given draws there too
    public void ADR0180_a_prerender_without_a_seed_waits_and_one_with_a_seed_draws()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        IRenderedComponent<Components.ExSheet> Prerender(ulong? seed) => Render<Components.ExSheet>(ps => ps
            .Add(s => s.ViewportHeight, (ViewportSize)400)
            .Add(s => s.ViewportWidth, (ViewportSize)700)
            .Add(s => s.Culture, CultureInfo.GetCultureInfo("en-US"))
            .Add(s => s.Document, Draws())
            .Add(s => s.Seed, seed));

        var unseeded = Prerender(null);
        var seeded = Prerender(Seed);

        Assert.All(Drawn, a => Assert.Equal("#GETTING_DATA", CellText(unseeded, a)));
        Assert.Equal(Alone(Draws(), Seed, 0), Shown(seeded));
    }

    [Fact] // ADR-0180, SH-66: without a Seed, ExSheet chooses one, so the draws show at once; a new Seed draws again; the document records neither
    public void ADR0180_without_a_seed_exsheet_chooses_one_and_records_none()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Draws()));

        Assert.All(Drawn, a => Assert.InRange(double.Parse(CellText(cut, a), CultureInfo.InvariantCulture), 0, 2000000));
        Assert.DoesNotContain("seed", cut.Instance.ToDocument().ToJson(), StringComparison.OrdinalIgnoreCase);

        cut.Render(ps => ps.Add(s => s.Seed, Seed));

        Assert.Equal(Alone(Draws(), Seed, 0), Shown(cut));
    }
}
