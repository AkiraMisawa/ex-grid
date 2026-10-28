using Bunit;
using ExGrid.Chrome;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// ADR-0410 sections 2 and 3: the find panel MudGridChrome draws when the Script Core draws the grid. It is drawn from
/// the core's view alone and marks each control for the adapter's script, which hands the changes and presses to the
/// core in the browser; what the marks do is the script's, tested there and in layer 3. Read here: that each control
/// carries the mark the script finds it by, that the slot draws no form of its own inside the core's, and that no
/// text field is written from a view, which arrives a round trip behind the typing (SRV-7).
/// </summary>
public class MudScriptFindPanelTests : MudTestContext
{
    private static readonly GridFindWords Words = new("Find what", "Match case", "Match entire cell contents", "Find next", "Find previous",
        "Replace with", "Replace", "Replace All");

    private static GridFindView Find(string text, bool replaceShown = false, string replaceText = "", bool waiting = false, string outcome = "")
        => new(text, true, false, replaceShown, replaceText, waiting, outcome, Words);

    private IRenderedComponent<MudExGridScriptFindPanel> Panel(GridFindView view) => Render<MudExGridScriptFindPanel>(ps => ps
        .Add(p => p.View, view)
        .Add(p => p.Chrome, MudGridChrome.Default));

    [Fact] // ADR-0410 section 2 / ADR-0055: each control carries the mark the script finds it by, and the slot draws no form inside the core's
    public void ADR0410_the_find_panels_controls_are_marked_for_the_script()
    {
        var cut = Panel(Find("Osei", outcome: "No match"));

        Assert.Equal("find", cut.Find($"input[{ScriptSlotAttributes.FindField}]").GetAttribute(ScriptSlotAttributes.FindField));
        Assert.Equal(["matchCase", "wholeCell"], cut.FindAll($"input[{ScriptSlotAttributes.FindOption}]").Select(o => o.GetAttribute(ScriptSlotAttributes.FindOption)));
        Assert.True(cut.Find($"input[{ScriptSlotAttributes.FindOption}='matchCase']").HasAttribute("checked"));
        Assert.Equal(["previous", "next"], cut.FindAll($"button[{ScriptSlotAttributes.FindStep}]").Select(b => b.GetAttribute(ScriptSlotAttributes.FindStep)));
        Assert.All(cut.FindAll("button"), button => Assert.NotEqual("submit", button.GetAttribute("type")));
        Assert.Empty(cut.FindAll("form"));
        Assert.Equal("No match", cut.Find("[role=status]").TextContent);
    }

    [Fact] // ADR-0332 section 2: Replace's field and writes are drawn while it is offered, unavailable while a Replace All is out
    public void ADR0332_Replace_is_drawn_while_offered_and_unavailable_while_a_Replace_All_is_out()
    {
        var cut = Panel(Find("Osei", replaceShown: true, replaceText: "Lee", waiting: true));

        Assert.Equal("Lee", cut.Find($"input[{ScriptSlotAttributes.FindField}='replace']").GetAttribute("value"));
        var writes = cut.FindAll($"button[{ScriptSlotAttributes.FindStep}='replace'], button[{ScriptSlotAttributes.FindStep}='replaceAll']");
        Assert.Equal(2, writes.Count);
        Assert.All(writes, write => Assert.True(write.HasAttribute("disabled")));
    }

    [Fact] // SRV-7 / ADR-0410 section 3: the find field opens with the core's text, and a view drawn later never writes it again
    public void SRV7_the_find_field_is_not_written_from_a_later_view()
    {
        var cut = Panel(Find("Os"));
        var field = cut.Find($"input[{ScriptSlotAttributes.FindField}='find']");
        Assert.Equal("Os", field.GetAttribute("value"));

        // The user typed on to "Osei"; the view of "Ose" arrives a round trip behind, and the field keeps what was typed.
        field.Change("Osei");
        cut.Render(ps => ps.Add(p => p.View, Find("Ose")));
        Assert.Equal("Osei", cut.Find($"input[{ScriptSlotAttributes.FindField}='find']").GetAttribute("value"));

        // Replace's field, drawn as Ctrl+H offers it, opens with the replacement the core holds.
        cut.Render(ps => ps.Add(p => p.View, Find("Osei", replaceShown: true, replaceText: "Lee")));
        Assert.Equal("Lee", cut.Find($"input[{ScriptSlotAttributes.FindField}='replace']").GetAttribute("value"));
    }
}
