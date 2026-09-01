using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// CON-7: no unobserved Task exception. A busy scenario — renders, scrolls, keys, a
/// source landing answers, disposal — followed by a forced collection with an
/// UnobservedTaskException handler installed. A discarded task that faulted anywhere
/// in the component would surface here and nowhere else.
/// </summary>
public class UnobservedExceptionTests : GridTestContext
{
    [Fact]
    public async Task A_busy_session_leaves_no_unobserved_task_exception()
    {
        var unobserved = new List<Exception>();
        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, e) => unobserved.Add(e.Exception);
        TaskScheduler.UnobservedTaskException += handler;
        try
        {
            var source = GridSource.From(TestRows.Many(500));
            var cut = Render<ExGrid<TestRow>>(ps => ps
                .Add(g => g.Source, source)
                .Add(g => g.Columns, (GridColumn<TestRow>[])
                    [new("Book", ColumnType.Text, r => r.Book, editable: true),
                     new("Amount", ColumnType.Number, r => r.Amount)])
                .Add(g => g.RowHeight, 20d)
                .Add(g => g.ViewportHeight, 120)
                .Add(g => g.ViewportWidth, 350));

            await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 20, OffsetY = 10 });
            await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("5", false, false, false, false, false));
            await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("Enter", false, false, false, false, false));
            await ScrollToAsync(cut.Find(".ex-scroller"), 4000, 40);
            await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
            source.OnSortChanged([new SortSpec("Amount", SortDirection.Descending)]);
            await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("a", true, false, false, false, false));
            _ = await cut.InvokeAsync(() => cut.Instance.BuildCopyPayload());
            await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("x", null));
            await DisposeComponentsAsync();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.Empty(unobserved);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
        }
    }
}
