using Bunit;
using ExGrid.Chrome;
using ExGrid.Columns;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// The Wrapper's text fields never write the user's own typing back into themselves (SRV-5,
/// ED-22). On a Server circuit the seam's context carries each reported text back a round trip
/// late; the field renders what the browser last reported, and takes only a text the core wrote
/// itself.
/// </summary>
public class MudFieldTextTests : MudTestContext
{
    private static NameBoxContext NameBox(string text, List<string> typed)
        => new(text, typed.Add, () => Task.CompletedTask, () => { });

    private static string Value(IRenderedComponent<MudNameBox> cut) => cut.Find("input").GetAttribute("value") ?? "";

    [Fact] // SRV-5/ED-22: an echo of a report a round trip old does not replace later typing
    public async Task An_echo_of_earlier_typing_is_not_written_back()
    {
        var typed = new List<string>();
        var cut = Render<MudNameBox>(ps => ps.Add(c => c.Context, NameBox("F3", typed)).Add(c => c.Label, "Name Box"));
        Assert.Equal("F3", Value(cut));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "n" });
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "no" });
        // The core's context catches up with the first report only.
        cut.Render(ps => ps.Add(c => c.Context, NameBox("n", typed)));

        Assert.Equal(["n", "no"], typed);
        Assert.Equal("no", Value(cut));
    }

    [Fact] // ADR-0051: a text the core wrote itself is shown
    public async Task A_text_the_core_wrote_is_shown()
    {
        var typed = new List<string>();
        var cut = Render<MudNameBox>(ps => ps.Add(c => c.Context, NameBox("F3", typed)).Add(c => c.Label, "Name Box"));
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "D200" });

        // Entered: the core shows the new Focus's label.
        cut.Render(ps => ps.Add(c => c.Context, NameBox("D199", typed)));

        Assert.Equal("D199", Value(cut));
    }

    [Fact] // SRV-5/ED-22: the Cell Editor's control, likewise
    public async Task The_cell_editor_keeps_later_typing_over_an_echo()
    {
        var typed = new List<string>();
        CellEditorContext Editor(string text) => new("Trader", ColumnType.Text, text, CellEditMode.Overwrite, null,
            typed.Add, () => { }, () => { });
        var cut = Render<MudCellEditor>(ps => ps.Add(c => c.Context, Editor("X")));

        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "Xa" });
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "Xab" });
        cut.Render(ps => ps.Add(c => c.Context, Editor("Xa")));

        Assert.Equal("Xab", cut.Find("input").GetAttribute("value"));
        // A Reference the core pointed replaces it.
        cut.Render(ps => ps.Add(c => c.Context, Editor("=A2")));
        Assert.Equal("=A2", cut.Find("input").GetAttribute("value"));
    }
}
