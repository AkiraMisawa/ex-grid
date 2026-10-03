using System.Reflection;
using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

// The render tree is read to see what an input's handler tells the renderer: there is no
// markup for it, and it is the whole of the mechanism under test.
#pragma warning disable BL0006

namespace ExGrid.Components.Tests;

/// <summary>
/// The text fields the core renders — the Cell Editor, the Formula Bar's field and the Name Box
/// — never write the user's own typing back into themselves (ED-22, SRV-5, DC-28, ADR-0007). On
/// a Server circuit an input event arrives a round trip after it was typed; a render answering it
/// that set the field's value would put back the text as it stood then, over the characters
/// typed since, move the caret to the end and empty the field's undo history. Each field's
/// input event therefore tells the renderer which attribute it updates (<c>value</c>): the
/// renderer takes the value the browser reports as what the field already shows, and a render
/// writes the field only when the core itself changes its text (an edit opening, a candidate
/// accepted, a Reference pointed).
/// </summary>
public class SurfaceWriteBackTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid()
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, [new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true)])
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.NameBoxLabel, cell => FormattableString.Invariant($"R{cell.Row + 1}C{cell.Column + 1}")));

    /// <summary>For each input element with <paramref name="cssClass"/> in the grid's own render
    /// tree: the attribute its <c>oninput</c> handler tells the renderer it updates.</summary>
    private List<string?> InputUpdates(IRenderedComponent<ExGrid<TestRow>> cut, string cssClass)
    {
        // Renderer's own accessor, protected: the frames as the renderer holds them.
        var read = typeof(Renderer).GetMethod("GetCurrentRenderTreeFrames", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var frames = (ArrayRange<RenderTreeFrame>)read.Invoke(Renderer, [cut.ComponentId])!;
        var found = new List<string?>();
        for (var i = 0; i < frames.Count; i++)
        {
            ref var element = ref frames.Array[i];
            if (element.FrameType != RenderTreeFrameType.Element || element.ElementName != "input")
                continue;
            string? classes = null;
            string? updates = null;
            var hasInput = false;
            for (var j = i + 1; j < i + element.ElementSubtreeLength && frames.Array[j].FrameType == RenderTreeFrameType.Attribute; j++)
            {
                ref var attribute = ref frames.Array[j];
                if (attribute.AttributeName == "class")
                    classes = attribute.AttributeValue as string;
                if (attribute.AttributeName == "oninput")
                {
                    hasInput = true;
                    updates = attribute.AttributeEventUpdatesAttributeName;
                }
            }
            if (hasInput && classes is not null && classes.Split(' ').Contains(cssClass))
                found.Add(updates);
        }
        return found;
    }

    [Fact] // ED-22/SRV-5/ADR-0007: the Cell Editor's typing is never written back into it
    public async Task The_cell_editor_takes_what_the_browser_reports_as_already_shown()
    {
        var cut = RenderGrid();
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("X", false, false, false, false, false));

        // Two: the Cell Editor and the Formula Bar's field, both editor surfaces.
        Assert.Equal(["value", "value"], InputUpdates(cut, "ex-editor"));
        Assert.Equal("X", cut.Find(".ex-viewport .ex-editor").GetAttribute("value"));
    }

    [Fact] // ED-22/SRV-5/DC-28: the Formula Bar's and the Name Box's typing is never written back into them
    public void The_bar_and_the_name_box_take_what_the_browser_reports_as_already_shown()
    {
        var cut = RenderGrid();

        Assert.Equal(["value"], InputUpdates(cut, "ex-formula-bar-text"));
        Assert.Equal(["value"], InputUpdates(cut, "ex-name-box"));
    }
}
