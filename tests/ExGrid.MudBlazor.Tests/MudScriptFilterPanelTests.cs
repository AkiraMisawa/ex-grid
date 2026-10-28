using Bunit;
using ExGrid.Chrome;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// ADR-0410 sections 2 and 3: the filter panel MudGridChrome draws when the Script Core draws the grid. It is drawn
/// from the core's view alone and marks each control for the adapter's script, which hands the changes to the core in
/// the browser; what the marks do is the script's, tested there and in layer 3. Read here: that each control carries
/// the mark the script finds it by, and that no text field is written from a view, which arrives a round trip behind
/// the typing (SRV-7).
/// </summary>
public class MudScriptFilterPanelTests : MudTestContext
{
    private static readonly GridFilterWords Words = new("OK", "Cancel", "Search", "Add current selection to filter", "(Select All)",
        "(Select All Search Results)", "And", "Or", "(none)");

    private static GridFilterView ValueList(params (string Text, bool Blank, bool Checked)[] entries) => new(
        "Book", ColumnType.Text,
        new GridFilterValueListView("", false, false, SelectAllState.Mixed, [.. entries.Select(e => new GridFilterEntryView(e.Text, e.Blank, e.Checked))]),
        null, true, false, Words);

    private static GridFilterView Conditions(string operand, GridFilterConditionView? second = null, bool canApply = true) => new(
        "Notional", ColumnType.Number, null,
        new GridFilterConditionsView(
            [new("equals", "Equals"), new("greaterThan", "GreaterThan"), new("isBlank", "IsBlank")],
            new GridFilterConditionView("greaterThan", operand, true, null),
            FilterCombinator.And,
            second),
        canApply, false, Words);

    private IRenderedComponent<MudExGridScriptFilterPanel> Panel(GridFilterView view) => Render<MudExGridScriptFilterPanel>(ps => ps
        .Add(p => p.View, view)
        .Add(p => p.Chrome, MudGridChrome.Default));

    [Fact] // ADR-0410 section 2 / ADR-0009: the value list's controls carry the marks the script finds them by, each value its entry's index
    public void ADR0410_the_value_lists_controls_are_marked_for_the_script()
    {
        var cut = Panel(ValueList(("Novak", false, true), ("(Blanks)", true, false)));

        var search = cut.Find($"input[{ScriptSlotAttributes.FilterSearch}]");
        Assert.Equal("first search", search.GetAttribute(ScriptSlotAttributes.FilterFocus));
        Assert.True(string.IsNullOrEmpty(search.GetAttribute("value")));
        Assert.NotNull(cut.Find($"[{ScriptSlotAttributes.FilterValues}]").QuerySelector($"input[{ScriptSlotAttributes.FilterSelectAll}]"));
        var values = cut.FindAll($"input[{ScriptSlotAttributes.FilterValue}]");
        Assert.Equal(["0", "1"], values.Select(v => v.GetAttribute(ScriptSlotAttributes.FilterValue)));
        Assert.Equal([true, false], values.Select(v => v.HasAttribute("checked")));
        // The Blank in the core's words for it, never the cell's empty text.
        Assert.Contains("(Blanks)", cut.FindAll(".mud-ex-grid-filter-value")[1].TextContent, StringComparison.Ordinal);
        Assert.Equal("last", cut.Find($"[{ScriptSlotAttributes.FilterCancel}]").GetAttribute(ScriptSlotAttributes.FilterFocus));
    }

    [Fact] // ADR-0410 section 2 / WR-2: the form's default button is a hidden one never disabled, ahead of OK, which shows the view's CanApply
    public void ADR0410_Enter_submits_through_a_default_button_that_is_never_disabled()
    {
        var cut = Panel(Conditions("", canApply: false));

        var submits = cut.FindAll("button[type=submit]");
        Assert.Equal(2, submits.Count);
        Assert.True(submits[0].HasAttribute("hidden"));
        Assert.False(submits[0].HasAttribute("disabled"));
        Assert.True(submits[1].HasAttribute("disabled"));
    }

    [Fact] // ADR-0410 section 2 / FN-18: the operators are native selects of the core's operators, the second's "(none)" chosen while it has none
    public void ADR0410_the_operators_are_native_selects_of_the_cores_operators()
    {
        var cut = Panel(Conditions("5"));

        var first = cut.Find($"select[{ScriptSlotAttributes.FilterOperator}='first']");
        Assert.Equal("first", first.GetAttribute(ScriptSlotAttributes.FilterFocus));
        Assert.Equal(["equals", "greaterThan", "isBlank"], first.QuerySelectorAll("option").Select(o => o.GetAttribute("value")));
        Assert.Equal("greaterThan", first.QuerySelector("option[selected]")?.GetAttribute("value"));
        var second = cut.Find($"select[{ScriptSlotAttributes.FilterOperator}='second']");
        Assert.Equal("", second.QuerySelector("option[selected]")?.GetAttribute("value"));
        Assert.Equal(["and", "or"], cut.FindAll($"input[{ScriptSlotAttributes.FilterJoin}]").Select(r => r.GetAttribute(ScriptSlotAttributes.FilterJoin)));
    }

    [Fact] // SRV-7 / ADR-0410 section 3: a value field opens with the core's operand, and a view drawn later never writes it again
    public void SRV7_a_value_field_is_not_written_from_a_later_view()
    {
        var cut = Panel(Conditions("5"));
        var field = cut.Find($"input[{ScriptSlotAttributes.FilterOperand}='first']");
        Assert.Equal("search", field.GetAttribute(ScriptSlotAttributes.FilterFocus));
        Assert.Equal("5", field.GetAttribute("value"));

        // The user typed on to "512"; the view of "51" arrives a round trip behind, and the field keeps what was typed.
        field.Change("512");
        cut.Render(ps => ps.Add(p => p.View, Conditions("51")));
        Assert.Equal("512", cut.Find($"input[{ScriptSlotAttributes.FilterOperand}='first']").GetAttribute("value"));

        // A second condition's field, drawn as it is chosen, opens with the operand the core holds for it.
        cut.Render(ps => ps.Add(p => p.View, Conditions("512", new GridFilterConditionView("equals", "7", true, null))));
        Assert.Equal("7", cut.Find($"input[{ScriptSlotAttributes.FilterOperand}='second']").GetAttribute("value"));
    }
}
