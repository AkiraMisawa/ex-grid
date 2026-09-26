using ExGrid.Rows;
using Xunit;

namespace ExGrid.Tests;

public class RowMarkRuleTests
{
    [Theory] // ADR-0043: Space brings the rows into line — any unmarked row means all become marked
    [InlineData(0, 5, true)]
    [InlineData(3, 5, true)]
    [InlineData(4, 5, true)]
    [InlineData(5, 5, false)]
    [InlineData(1, 1, false)]
    [InlineData(0, 1, true)]
    public void Lining_up_marks_all_unless_all_are_already_marked(int marked, int rows, bool expected)
    {
        Assert.Equal(expected, RowMarkRules.LineUp(marked, rows));
    }
}

public class RowMarkHeaderTests
{
    [Theory] // ADR-0043: the header's state comes from the result's counts, not from what is loaded
    [InlineData(0, 0, RowMarkHeaderState.None)]
    [InlineData(0, 10, RowMarkHeaderState.None)]
    [InlineData(1, 10, RowMarkHeaderState.Some)]
    [InlineData(9, 10, RowMarkHeaderState.Some)]
    [InlineData(10, 10, RowMarkHeaderState.All)]
    public void The_header_state_compares_marked_rows_with_the_rows_of_the_result(int marked, int rows, RowMarkHeaderState expected)
    {
        Assert.Equal(expected, RowMarkRules.HeaderState(new RowMarkCounts(marked, rows, 0)));
    }

    [Fact] // ADR-0043: 40 loaded rows all marked in a result of a million is "some", never "all"
    public void A_fully_marked_window_inside_a_larger_result_is_some()
    {
        Assert.Equal(RowMarkHeaderState.Some, RowMarkRules.HeaderState(new RowMarkCounts(40, 1_000_000, 0)));
    }

    [Fact] // ADR-0043: marks outside the current filter do not make the header "all"
    public void Marks_outside_the_result_do_not_count_towards_the_header()
    {
        Assert.Equal(RowMarkHeaderState.None, RowMarkRules.HeaderState(new RowMarkCounts(0, 10, 70)));
    }

    [Theory] // ADR-0043: pressing "some" marks all; only "all" unmarks
    [InlineData(RowMarkHeaderState.None, true)]
    [InlineData(RowMarkHeaderState.Some, true)]
    [InlineData(RowMarkHeaderState.All, false)]
    public void Pressing_the_header_marks_all_unless_all_are_marked(RowMarkHeaderState state, bool marks)
    {
        Assert.Equal(marks, RowMarkRules.HeaderPress(state));
    }

    [Fact] // ADR-0043: a count that contradicts itself is refused, not painted
    public void Counts_with_more_marked_than_rows_are_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RowMarkCounts(11, 10, 0));
    }
}
