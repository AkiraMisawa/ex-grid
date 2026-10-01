using ExPivot.Engine;
using Xunit;
using static ExPivot.Engine.Tests.Pivot;

namespace ExPivot.Engine.Tests;

/// <summary>
/// The Layout menu on the report's toolbar (ADR-0061): Excel's Design tab choices as the engine's
/// pure functions — what each one sets, which one is current, and which ones would change nothing.
/// </summary>
public class LayoutMenuTests
{
    private static readonly PivotLayout TwoByTwo = new()
    {
        Rows = [P("Region"), P("Product")],
        Columns = [P("Online")],
        Values = [Sum("Amount")],
    };

    private static PivotLayoutChoice[] Chosen(PivotLayout layout)
        => Enum.GetValues<PivotLayoutChoice>().Where(c => PivotLayoutEdits.IsChosen(layout, c)).ToArray();

    private static PivotLayoutChoice[] Disabled(PivotLayout layout)
        => Enum.GetValues<PivotLayoutChoice>().Where(c => !PivotLayoutEdits.Changes(layout, c)).ToArray();

    [Fact] // ADR-0061: the menu's choices are Excel's Design tab's, in Excel's order, Blank Rows left out
    public void The_choices_are_excels_in_excels_order()
        => Assert.Equal(
        [
            PivotLayoutChoice.DoNotShowSubtotals, PivotLayoutChoice.ShowSubtotalsAtBottom, PivotLayoutChoice.ShowSubtotalsAtTop,
            PivotLayoutChoice.GrandTotalsOff, PivotLayoutChoice.GrandTotalsOn, PivotLayoutChoice.GrandTotalsOnRowsOnly,
            PivotLayoutChoice.GrandTotalsOnColumnsOnly, PivotLayoutChoice.CompactForm, PivotLayoutChoice.OutlineForm,
            PivotLayoutChoice.TabularForm, PivotLayoutChoice.RepeatItemLabels, PivotLayoutChoice.DoNotRepeatItemLabels,
        ], Enum.GetValues<PivotLayoutChoice>());

    [Fact] // ADR-0061/0060: Do Not Show Subtotals turns every row and column field's subtotal off, and nothing else
    public void Do_not_show_subtotals()
    {
        var layout = TwoByTwo with { Filters = [P("Date")] };

        var chosen = PivotLayoutEdits.Choose(layout, PivotLayoutChoice.DoNotShowSubtotals);

        Assert.All(chosen.Rows.Concat(chosen.Columns), p => Assert.False(p.Subtotals));
        Assert.True(chosen.Filters[0].Subtotals);
        Assert.Equal(layout.SubtotalsAtTop, chosen.SubtotalsAtTop);
        Assert.Equal("g [-]East ||  |  |", Lines(Report(chosen))[0]);
    }

    [Fact] // ADR-0061/0060: Show all Subtotals at Bottom of Group turns every subtotal on, at the bottom
    public void Show_all_subtotals_at_bottom()
    {
        var layout = TwoByTwo with { Rows = [P("Region") with { Subtotals = false }, P("Product")] };

        var chosen = PivotLayoutEdits.Choose(layout, PivotLayoutChoice.ShowSubtotalsAtBottom);

        Assert.All(chosen.Rows.Concat(chosen.Columns), p => Assert.True(p.Subtotals));
        Assert.False(chosen.SubtotalsAtTop);
        Assert.Contains("s East Total || 50 | 130 | 180", Lines(Report(chosen)));
    }

    [Fact] // ADR-0061/0060: Show all Subtotals at Top of Group turns every subtotal on, on the group row
    public void Show_all_subtotals_at_top()
    {
        var layout = PivotLayoutEdits.Choose(TwoByTwo, PivotLayoutChoice.DoNotShowSubtotals) with { SubtotalsAtTop = false };

        var chosen = PivotLayoutEdits.Choose(layout, PivotLayoutChoice.ShowSubtotalsAtTop);

        Assert.All(chosen.Rows.Concat(chosen.Columns), p => Assert.True(p.Subtotals));
        Assert.True(chosen.SubtotalsAtTop);
        Assert.Equal("g [-]East || 50 | 130 | 180", Lines(Report(chosen))[0]);
    }

    [Theory] // ADR-0061/0060: the four Grand Totals choices are the two switches' four settings, Excel's "rows" being the column at the right
    [InlineData(PivotLayoutChoice.GrandTotalsOff, false, false)]
    [InlineData(PivotLayoutChoice.GrandTotalsOn, true, true)]
    [InlineData(PivotLayoutChoice.GrandTotalsOnRowsOnly, false, true)]
    [InlineData(PivotLayoutChoice.GrandTotalsOnColumnsOnly, true, false)]
    public void Grand_totals(PivotLayoutChoice choice, bool row, bool column)
    {
        var chosen = PivotLayoutEdits.Choose(TwoByTwo with { GrandTotalRow = !row, GrandTotalColumn = !column }, choice);

        Assert.Equal(row, chosen.GrandTotalRow);
        Assert.Equal(column, chosen.GrandTotalColumn);
        Assert.Equal(column, Headers(Report(chosen)).Contains("Grand Total"));
        Assert.Equal(row, Lines(Report(chosen))[^1].StartsWith("t Grand Total", StringComparison.Ordinal));
        Assert.Equal([choice], Chosen(chosen).Intersect(GrandTotalChoices));
    }

    private static readonly PivotLayoutChoice[] GrandTotalChoices =
    [
        PivotLayoutChoice.GrandTotalsOff, PivotLayoutChoice.GrandTotalsOn,
        PivotLayoutChoice.GrandTotalsOnRowsOnly, PivotLayoutChoice.GrandTotalsOnColumnsOnly,
    ];

    [Theory] // ADR-0061/0060: the Report Layout choices set the form
    [InlineData(PivotLayoutChoice.CompactForm, PivotReportForm.Compact)]
    [InlineData(PivotLayoutChoice.OutlineForm, PivotReportForm.Outline)]
    [InlineData(PivotLayoutChoice.TabularForm, PivotReportForm.Tabular)]
    public void The_form(PivotLayoutChoice choice, PivotReportForm form)
    {
        var start = TwoByTwo with { Form = form == PivotReportForm.Compact ? PivotReportForm.Tabular : PivotReportForm.Compact };

        var chosen = PivotLayoutEdits.Choose(start, choice);

        Assert.Equal(form, chosen.Form);
        Assert.True(PivotLayoutEdits.IsChosen(chosen, choice));
        Assert.False(PivotLayoutEdits.Changes(chosen, choice));
    }

    [Fact] // ADR-0061/0060: Repeat All Item Labels fills in the outer labels of the Outline and Tabular forms; Do Not Repeat clears them
    public void Repeat_and_do_not_repeat_item_labels()
    {
        var tabular = TwoByTwo with { Form = PivotReportForm.Tabular };

        var repeated = PivotLayoutEdits.Choose(tabular, PivotLayoutChoice.RepeatItemLabels);

        Assert.True(repeated.RepeatItemLabels);
        Assert.Equal("i East | Pears || 50 |  | 50", Lines(Report(repeated))[1]);
        var plain = PivotLayoutEdits.Choose(repeated, PivotLayoutChoice.DoNotRepeatItemLabels);
        Assert.False(plain.RepeatItemLabels);
        Assert.Equal("i  | Pears || 50 |  | 50", Lines(Report(plain))[1]);
    }

    [Fact] // ADR-0061: the current choice of each group is marked — the default layout is Compact, subtotals at the top, both grand totals
    public void The_current_choices_are_marked()
        => Assert.Equal(
            [PivotLayoutChoice.ShowSubtotalsAtTop, PivotLayoutChoice.GrandTotalsOn, PivotLayoutChoice.CompactForm],
            Chosen(TwoByTwo));

    [Fact] // ADR-0061: a choice that would change nothing is disabled — the current one, and the label choices in the Compact form
    public void A_choice_that_would_change_nothing_is_disabled()
    {
        Assert.Equal(
        [
            PivotLayoutChoice.ShowSubtotalsAtTop, PivotLayoutChoice.GrandTotalsOn, PivotLayoutChoice.CompactForm,
            PivotLayoutChoice.RepeatItemLabels, PivotLayoutChoice.DoNotRepeatItemLabels,
        ], Disabled(TwoByTwo));
        // Applied anyway, a current choice hands the same layout back.
        Assert.All(Chosen(TwoByTwo), c => Assert.Same(TwoByTwo, PivotLayoutEdits.Choose(TwoByTwo, c)));
    }

    [Fact] // ADR-0061/0060: the Tabular form puts every subtotal at the bottom, so Show at Bottom is current there and Show at Top changes nothing
    public void In_the_tabular_form_subtotals_are_at_the_bottom()
    {
        var tabular = TwoByTwo with { Form = PivotReportForm.Tabular, SubtotalsAtTop = true };

        Assert.Contains(PivotLayoutChoice.ShowSubtotalsAtBottom, Chosen(tabular));
        Assert.Contains(PivotLayoutChoice.ShowSubtotalsAtBottom, Disabled(tabular));
        Assert.Contains(PivotLayoutChoice.ShowSubtotalsAtTop, Disabled(tabular));
        Assert.DoesNotContain(PivotLayoutChoice.DoNotRepeatItemLabels, Chosen(TwoByTwo));
        Assert.Contains(PivotLayoutChoice.DoNotRepeatItemLabels, Chosen(tabular));
        Assert.Contains(PivotLayoutChoice.RepeatItemLabels, PivotLayoutChoicesEnabled(tabular));
    }

    private static PivotLayoutChoice[] PivotLayoutChoicesEnabled(PivotLayout layout)
        => Enum.GetValues<PivotLayoutChoice>().Where(c => PivotLayoutEdits.Changes(layout, c)).ToArray();

    [Fact] // ADR-0061: with no field in Rows or Columns there is nothing to subtotal — no Subtotals choice is current or enabled
    public void Without_axis_fields_subtotals_are_disabled()
    {
        var layout = new PivotLayout { Filters = [P("Region")], Values = [Sum("Amount")] };

        Assert.DoesNotContain(Chosen(layout), c => c <= PivotLayoutChoice.ShowSubtotalsAtTop);
        Assert.Contains(PivotLayoutChoice.DoNotShowSubtotals, Disabled(layout));
        Assert.Contains(PivotLayoutChoice.ShowSubtotalsAtBottom, Disabled(layout));
        Assert.Contains(PivotLayoutChoice.ShowSubtotalsAtTop, Disabled(layout));
    }

    [Fact] // ADR-0061: fields that disagree about their subtotals mark no Subtotals choice, and each choice that would set them all is enabled
    public void Mixed_subtotals_mark_nothing()
    {
        var layout = TwoByTwo with { Rows = [P("Region") with { Subtotals = false }, P("Product")] };

        Assert.DoesNotContain(Chosen(layout), c => c <= PivotLayoutChoice.ShowSubtotalsAtTop);
        Assert.DoesNotContain(Disabled(layout), c => c <= PivotLayoutChoice.ShowSubtotalsAtTop);
    }

    [Fact] // ADR-0061: a choice keeps everything else in the layout — the fields, their Hidden Items, collapse and order
    public void A_choice_keeps_the_rest_of_the_layout()
    {
        var layout = TwoByTwo with
        {
            Rows = [P("Region") with { HiddenItems = [PivotItemKey.Text("West")], Collapsed = true, Sort = PivotSort.Descending }, P("Product")],
        };

        var chosen = PivotLayoutEdits.Choose(layout, PivotLayoutChoice.OutlineForm);

        Assert.Equal(layout.Rows, chosen.Rows);
        Assert.Equal(layout.Columns, chosen.Columns);
        Assert.Equal(layout.Values, chosen.Values);
        Assert.Equal(PivotReportForm.Outline, chosen.Form);
    }
}
