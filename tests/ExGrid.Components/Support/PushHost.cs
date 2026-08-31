using ExGrid.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace ExGrid.Components.Tests.Support;

/// <summary>
/// A Consumer that answers a Range Request immediately, in the callback itself — the
/// shape <c>GridSource.From</c> produces, where everything is in hand (ADR-0001). It
/// exists to prove the request loop terminates: the grid asks, this answers, the answer
/// covers the Viewport, and nothing asks again.
/// </summary>
internal sealed class PushHost : ComponentBase
{
    [Parameter, EditorRequired] public TestRow[] AllRows { get; set; } = [];

    [Parameter] public double RowHeight { get; set; } = 20;

    [Parameter] public double ViewportHeight { get; set; } = 100;

    /// <summary>How much more than the Viewport to answer with — read-ahead is the
    /// Consumer's job, not the grid's (ADR-0001).</summary>
    [Parameter] public int ReadAhead { get; set; }

    public List<RowRange> Requests { get; } = [];

    private IReadOnlyList<TestRow> _window = [];
    private int _windowStart;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<ExGrid<TestRow>>(0);
        builder.AddComponentParameter(1, nameof(ExGrid<TestRow>.Window), _window);
        builder.AddComponentParameter(2, nameof(ExGrid<TestRow>.WindowStart), _windowStart);
        builder.AddComponentParameter(3, nameof(ExGrid<TestRow>.TotalCount), (int?)AllRows.Length);
        builder.AddComponentParameter(4, nameof(ExGrid<TestRow>.Columns), (IReadOnlyList<GridColumn<TestRow>>)TestRows.Columns());
        builder.AddComponentParameter(5, nameof(ExGrid<TestRow>.RowHeight), RowHeight);
        builder.AddComponentParameter(6, nameof(ExGrid<TestRow>.ViewportHeight), ViewportHeight);
        builder.AddComponentParameter(7, nameof(ExGrid<TestRow>.OnRangeNeeded),
            EventCallback.Factory.Create<RowRange>(this, Answer));
        builder.CloseComponent();
    }

    private void Answer(RowRange range)
    {
        Requests.Add(range);
        var start = Math.Max(0, range.Start - ReadAhead);
        var end = Math.Min(AllRows.Length, range.Start + range.Count + ReadAhead);
        _windowStart = start;
        _window = AllRows[start..end];
        StateHasChanged();
    }
}
