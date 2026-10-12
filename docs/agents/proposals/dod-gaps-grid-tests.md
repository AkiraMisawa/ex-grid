# Proposals from claude/dod-gaps-grid-tests

The branch closed layer-1 and layer-2 gaps in the Definition of Done for the ExGrid core and
its Wrappers. One row could not be closed as written: **UX-5**. The test that would close it
fails against the product, and the small fix collides with ADR-0160. The choice belongs to the
user, so this file sets out what was found and two ways forward.

## UX-5: the root renders once when the Consumer restyles its wrapping element

**What was found.** In the layer-2 test, a Consumer component wraps `<ExGrid>` in a `div`
whose `style` it owns. Every grid parameter is held in a field, so a render of the wrapper hands
the grid the very same instances. The test changes the wrapper's style to
`--ex-header-background: …; --ex-cell-color: …`. The result:

- every `ExGridRow` keeps its `RenderCount` (rows render nothing);
- the grid root's `RenderCount` goes up by exactly one.

Blazor treats any reference-typed parameter (the Window array, the Columns list) as "may have
changed". So every render of a Consumer calls `SetParametersAsync` on the grid, and the grid
renders, whatever changed. ADR-0027 says what UX-5 is meant to guarantee, in its Consequences:
"[A Wrapper] renders its own element around `<ExGrid>` and sets `--ex-*` on it. Nothing is
passed, nothing is compared, nothing re-renders." And P3 says: "An appearance change
re-renders nothing in .NET." The root renders, so the row fails as written.

**The fix tried, and why it was not committed.** `SetParametersAsync` was made to skip when
every parameter handed is the one already held: the same instance for a reference type, an
equal value for a value type, and never for an empty push. With that change:
- the UX-5 test passed;
- ExGrid.Components, ExGrid.MudBlazor.Tests, ExSheet.Components, ExSheet.MudBlazor.Tests
  and ExPivot.MudBlazor.Tests all passed;
- `ExPivot.Components.Tests.LiveRedrawTests.ADR0160_LV22_the_grid_holds_no_display_row_of_an_earlier_window`
  failed.

That ExPivot test relies on ADR-0160's account of memory: Blazor keeps the render before the
newest as the buffer it renders into next. A grid that renders again when its Consumer
renders lets go of the older Window's rows. A grid that skips that render keeps them alive.
So the skip trades P3 against ADR-0160's retention bound. That is a decision, not a defect fix.

**The two ways forward:**

1. **Keep UX-5 as written, and decide how the grid skips the render without holding an older
   Window.** For example, the skip could apply only when the Window instance is unchanged *and*
   the previous render already painted from it. This is an ADR-0027/ADR-0160 decision. The
   failing test is ready to commit once the product meets it.
2. **Restate UX-5's layer-2 pass condition** to what Blazor allows without the skip. This is
   weaker, so it is the user's call:

   1. old ->
      `| **UX-5** | MUST | Visual Tokens set on an ancestor element reach the grid, with **zero .NET renders** (ADR-0027 P3) | Layer 2: change the wrapping element's `style`; Layer 3: recolour and count | `RenderCount` unchanged; the paint changes |`
      new ->
      `| **UX-5** | MUST | Visual Tokens set on an ancestor element reach the grid, with **no row rendered** and at most the root's one render that the Consumer's own render causes (ADR-0027 P3) | Layer 2: change the wrapping element's `style`; Layer 3: recolour and count | every row's `RenderCount` unchanged and the root's up by at most one; the paint changes |`

**The test.** It is not committed, because it fails today. Its body, for whichever way is
taken:

```csharp
private sealed class TokenWrapper : ComponentBase
{
    private readonly TestRow[] _window = TestRows.Many(200);
    private readonly IReadOnlyList<GridColumn<TestRow>> _columns = TestRows.Columns();
    [Parameter] public string Style { get; set; } = "";
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "consumer-theme");
        builder.AddAttribute(2, "style", Style);
        builder.OpenComponent<ExGrid<TestRow>>(3);
        builder.AddComponentParameter(4, nameof(ExGrid<TestRow>.Window), _window);
        builder.AddComponentParameter(5, nameof(ExGrid<TestRow>.TotalCount), (int?)_window.Length);
        builder.AddComponentParameter(6, nameof(ExGrid<TestRow>.Columns), _columns);
        builder.AddComponentParameter(7, nameof(ExGrid<TestRow>.RowHeight), 20d);
        builder.AddComponentParameter(8, nameof(ExGrid<TestRow>.ViewportHeight), (ViewportSize)120);
        builder.CloseComponent();
        builder.CloseElement();
    }
}

[Fact] // UX-5, ADR-0027 P3
public void UX5_a_visual_token_set_on_the_wrapping_element_renders_nothing_in_dotnet()
{
    var host = Render<TokenWrapper>(ps => ps.Add(w => w.Style, ""));
    var grid = host.FindComponent<ExGrid<TestRow>>();
    var rows = host.FindComponents<ExGridRow<TestRow>>();
    var gridRenders = grid.RenderCount;
    var rowRenders = rows.Select(r => r.RenderCount).ToArray();

    host.Render(ps => ps.Add(w => w.Style, "--ex-header-background: rgb(200, 12, 12); --ex-cell-color: navy"));

    Assert.Equal("consumer-theme", host.Find(".ex-grid").ParentElement!.ClassName);
    Assert.Equal(gridRenders, grid.RenderCount);          // fails today: gridRenders + 1
    Assert.Equal(rowRenders, rows.Select(r => r.RenderCount).ToArray());
}
```
