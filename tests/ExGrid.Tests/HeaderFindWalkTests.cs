using ExGrid.Finding;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The order a Find step walks once the headers are searched (ADR-0333 §3): the header rows first,
/// then the data rows by rows, wrapping; backward the reverse. The grid searches the headers itself and
/// reads the Consumer's answer, asked as ADR-0055 asks it, for whether the walk passed the header rows
/// before reaching it.
/// </summary>
public class HeaderFindWalkTests
{
    // Headers 1 and 3 of five match.
    private static readonly int[] Matches = [1, 3];
    private static readonly CellPosition Focus = new(5, 1);

    [Fact] // ADR-0333 §3: no header matches — the Consumer is always asked, and its answer always stands
    public void ADR0333_with_no_header_matching_the_consumer_decides()
    {
        Assert.Null(HeaderFindWalk.BeforeAsking([], fromHeader: null, fromCell: false, backward: false));
        Assert.Null(HeaderFindWalk.BeforeAsking([], fromHeader: 2, fromCell: false, backward: false));
        Assert.Null(HeaderFindWalk.AfterAnswer([], Focus, answer: null, backward: false));
        Assert.Null(HeaderFindWalk.AfterAnswer([], Focus, new CellPosition(0, 0), backward: false));
    }

    [Theory] // ADR-0333 §3: from a header, the next matching header after it (before it, backward) comes before any row
    [InlineData(0, false, 1)]
    [InlineData(1, false, 3)]
    [InlineData(2, false, 3)]
    [InlineData(4, true, 3)]
    [InlineData(3, true, 1)]
    [InlineData(2, true, 1)]
    public void ADR0333_from_a_header_the_next_header_is_reached_first(int from, bool backward, int expected)
        => Assert.Equal(expected, HeaderFindWalk.BeforeAsking(Matches, from, fromCell: false, backward));

    [Theory] // ADR-0333 §3: from the last matching header (the first, backward) the rows are next, so the Consumer is asked
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(1, true)]
    [InlineData(0, true)]
    public void ADR0333_past_the_last_header_the_consumer_is_asked(int from, bool backward)
        => Assert.Null(HeaderFindWalk.BeforeAsking(Matches, from, fromCell: false, backward));

    [Fact] // ADR-0333 §3: from nothing, forward starts at the top, where the header rows are; backward starts at the end of the rows
    public void ADR0333_from_nothing_forward_starts_at_the_headers()
    {
        Assert.Equal(1, HeaderFindWalk.BeforeAsking(Matches, fromHeader: null, fromCell: false, backward: false));
        Assert.Null(HeaderFindWalk.BeforeAsking(Matches, fromHeader: null, fromCell: false, backward: true));
    }

    [Theory] // ADR-0333 §3: from a cell the Consumer is asked first, whichever way
    [InlineData(false)]
    [InlineData(true)]
    public void ADR0333_from_a_cell_the_consumer_is_asked_first(bool backward)
        => Assert.Null(HeaderFindWalk.BeforeAsking(Matches, fromHeader: null, fromCell: true, backward));

    [Theory] // ADR-0333 §3: forward from a cell, an answer after it stands; one at or before it wrapped past the headers, so the first matching header is reached first
    [InlineData(5, 2, false)]
    [InlineData(6, 0, false)]
    [InlineData(5, 1, true)]
    [InlineData(5, 0, true)]
    [InlineData(0, 0, true)]
    public void ADR0333_forward_a_wrapped_answer_gives_way_to_the_first_header(int row, int column, bool wrapped)
        => Assert.Equal(wrapped ? 1 : null, HeaderFindWalk.AfterAnswer(Matches, Focus, new CellPosition(row, column), backward: false));

    [Theory] // ADR-0333 §3: backward from a cell, an answer before it stands; one at or after it wrapped past the headers, so the last matching header is reached first
    [InlineData(5, 0, false)]
    [InlineData(4, 9, false)]
    [InlineData(5, 1, true)]
    [InlineData(5, 2, true)]
    [InlineData(99, 0, true)]
    public void ADR0333_backward_a_wrapped_answer_gives_way_to_the_last_header(int row, int column, bool wrapped)
        => Assert.Equal(wrapped ? 3 : null, HeaderFindWalk.AfterAnswer(Matches, Focus, new CellPosition(row, column), backward: true));

    [Theory] // ADR-0333 §3: no match among the rows — the walk reaches a header, and a lone header match finds itself
    [InlineData(false, 1)]
    [InlineData(true, 3)]
    public void ADR0333_no_row_matching_lands_on_a_header(bool backward, int expected)
    {
        Assert.Equal(expected, HeaderFindWalk.AfterAnswer(Matches, Focus, answer: null, backward));
        Assert.Equal(expected, HeaderFindWalk.AfterAnswer(Matches, from: null, answer: null, backward));
        Assert.Equal(2, HeaderFindWalk.AfterAnswer([2], from: null, answer: null, backward));
    }

    [Theory] // ADR-0333 §3: asked from the start of the rows (the end, backward), a found answer wraps past nothing and stands
    [InlineData(false)]
    [InlineData(true)]
    public void ADR0333_asked_from_the_start_a_found_answer_stands(bool backward)
        => Assert.Null(HeaderFindWalk.AfterAnswer(Matches, from: null, new CellPosition(0, 0), backward));
}
