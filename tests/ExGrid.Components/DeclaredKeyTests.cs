using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Keys;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Declared keys (ADR-0050, item 14; DC-57), the C# half: which keys the gate is handed, in which
/// state, and what a forwarded declared key raises. That the real browser hands them to the grid
/// before its own meaning — Ctrl+U's page source — is layer 3's, on ExSheet's /sheet.
/// </summary>
public class DeclaredKeyTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));
    private static readonly string[] Bold = ["Control+b", "Control+B"];

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        IReadOnlyCollection<string>? declared, List<GridDeclaredKeyPress>? raised, List<GridSelection>? selections = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(20))
              .Add(g => g.TotalCount, 20)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350);
            if (declared is not null)
                ps.Add(g => g.DeclaredKeys, declared);
            if (raised is not null)
                ps.Add(g => g.OnDeclaredKey, raised.Add);
            if (selections is not null)
                ps.Add(g => g.SelectionChanged, selections.Add);
        });

    private static Task PressAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false,
        bool meta = false, bool metaIsPrimary = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, meta, metaIsPrimary));

    // A key as the listener forwards it while an edit is open: with the surface's text and caret.
    private static Task PressInEditorAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, string text, bool ctrl = false, bool shift = false, bool fromBar = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            key, ctrl, shift, false, false, false, fromDescendant: fromBar, editorText: text, editorCaret: text.Length,
            editorSelectionEnd: text.Length));

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    [Fact] // ADR-0050 item 14 / DC-57 / DC-1: undeclared, a key stays the browser's, in either state, and nothing is raised
    public async Task Undeclared_a_key_stays_the_browsers()
    {
        var raised = new List<GridDeclaredKeyPress>();
        var cut = RenderGrid(declared: null, raised: null);

        Assert.DoesNotContain("Control+b", Js.TakenAtAttach);
        Assert.Empty(Js.DeclaredAtAttach);
        Assert.Equal(GridKeys.TakenFor(new GridKeyClaims(CanEdit: true, CanUndo: false, CanRedo: false)), Js.TakenAtAttach);

        await PressAsync(cut, "b", ctrl: true);

        Assert.Empty(raised);
    }

    [Fact] // ADR-0050 item 14 / DC-57: declared, the gate takes the keys with no edit open and is handed them for its editing branch
    public void Declared_the_gate_claims_the_keys_in_both_states()
    {
        RenderGrid(Bold, []);

        Assert.Contains("Control+b", Js.TakenAtAttach);
        Assert.Contains("Control+B", Js.TakenAtAttach);
        Assert.Equal(Bold.Order(StringComparer.Ordinal), Js.DeclaredAtAttach.Order(StringComparer.Ordinal));
        // Everything the grid claimed before is still claimed.
        Assert.All(GridKeys.TakenFor(new GridKeyClaims(CanEdit: true, CanUndo: false, CanRedo: false)),
            key => Assert.Contains(key, Js.TakenAtAttach));
    }

    [Fact] // ADR-0050 item 14 / DC-57: with no edit open, a declared key is raised as declared, with no edit open, and moves nothing
    public async Task A_declared_key_is_raised_and_moves_nothing()
    {
        var raised = new List<GridDeclaredKeyPress>();
        var selections = new List<GridSelection>();
        var cut = RenderGrid(Bold, raised, selections);
        await ClickCellAsync(cut, 50, 30);
        var told = selections.Count;
        var counts = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();

        await PressAsync(cut, "b", ctrl: true);
        await PressAsync(cut, "B", ctrl: true); // CapsLock

        Assert.Equal([new GridDeclaredKeyPress("Control+b", false), new GridDeclaredKeyPress("Control+B", false)], raised);
        Assert.Equal(told, selections.Count);
        Assert.Empty(cut.FindAll(".ex-editor"));
        Assert.Equal(counts, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList());
    }

    [Fact] // ADR-0050 item 14 / DC-57: a key matched exactly — Shift is part of the form, and an undeclared form is not raised
    public async Task Only_the_declared_form_is_raised()
    {
        var raised = new List<GridDeclaredKeyPress>();
        var cut = RenderGrid(["Control+#", "Control+Shift+#"], raised);
        await ClickCellAsync(cut, 50, 30);

        await PressAsync(cut, "#", ctrl: true);              // a UK layout: no Shift
        await PressAsync(cut, "#", ctrl: true, shift: true); // a US layout: Shift
        await PressAsync(cut, "b", ctrl: true);              // not declared

        Assert.Equal(["Control+#", "Control+Shift+#"], raised.Select(r => r.Key));
    }

    [Fact] // ADR-0050 item 14 / ADR-0012: Command is the Primary Modifier on an Apple keyboard, and the OS's elsewhere
    public async Task Command_raises_a_declared_key_on_an_apple_platform_only()
    {
        var raised = new List<GridDeclaredKeyPress>();
        var cut = RenderGrid(Bold, raised);

        await PressAsync(cut, "b", meta: true, metaIsPrimary: true);
        await PressAsync(cut, "b", meta: true, metaIsPrimary: false);

        Assert.Equal([new GridDeclaredKeyPress("Control+b", false)], raised);
    }

    [Fact] // ADR-0050 item 14 / DC-57: while an edit is open, a declared key is raised with the edit open, and the edit stays as it was
    public async Task While_an_edit_is_open_a_declared_key_is_raised_with_it_open()
    {
        var raised = new List<GridDeclaredKeyPress>();
        var cut = RenderGrid(Bold, raised);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "F2");
        await cut.Find(".ex-editor").InputAsync(new ChangeEventArgs { Value = "Row 000000 x" });

        await PressInEditorAsync(cut, "b", "Row 000000 x", ctrl: true);

        Assert.Equal([new GridDeclaredKeyPress("Control+b", true)], raised);
        Assert.Equal("Row 000000 x", cut.Find(".ex-editor").GetAttribute("value"));
        // Still open: Escape cancels it, and the cell keeps its value.
        await PressInEditorAsync(cut, "Escape", "Row 000000 x");
        Assert.Empty(cut.FindAll(".ex-editor"));
        Assert.Equal("Row 000000", cut.FindAll("[role=gridcell]")[0].TextContent);
    }

    [Fact] // ADR-0050 item 14 / ADR-0051 / DC-57: in Overwrite too, where the core takes the arrows, a declared key commits nothing
    public async Task In_overwrite_a_declared_key_commits_nothing()
    {
        var raised = new List<GridDeclaredKeyPress>();
        var cut = RenderGrid(Bold, raised);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "q");

        await PressInEditorAsync(cut, "b", "q", ctrl: true);

        Assert.Equal([new GridDeclaredKeyPress("Control+b", true)], raised);
        Assert.Equal("q", cut.Find(".ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0050 item 14 / ADR-0010 / DC-57: a declaration naming a key the core answers itself is refused by name
    public void A_declaration_naming_a_core_key_is_refused_by_name()
    {
        var refused = Assert.Throws<ArgumentException>(() => RenderGrid(["Control+b", "Control+a"], []));

        Assert.Contains("'Control+a'", refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0050 item 14: keys declared for nobody would be taken from the page for nothing, so the declaration is refused by name
    public void Declared_keys_without_a_listener_are_refused()
    {
        var refused = Assert.Throws<ArgumentException>(() => RenderGrid(Bold, raised: null));

        Assert.Equal(nameof(ExGrid<TestRow>.OnDeclaredKey), refused.ParamName);
    }

    [Fact] // ADR-0050 item 14: a declaration changed after attach re-tells the gate; the same keys again do not
    public void A_declaration_changed_later_re_tells_the_gate()
    {
        var rows = TestRows.Many(20);
        var columns = Columns();
        var raised = EventCallback.Factory.Create<GridDeclaredKeyPress>(this, _ => { });
        void Base(ComponentParameterCollectionBuilder<ExGrid<TestRow>> ps) => ps
            .Add(g => g.Window, rows).Add(g => g.TotalCount, 20).Add(g => g.Columns, columns)
            .Add(g => g.RowHeight, 20d).Add(g => g.ViewportHeight, 120).Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnDeclaredKey, raised);
        var cut = Render<ExGrid<TestRow>>(ps => { Base(ps); ps.Add(g => g.DeclaredKeys, Bold); });

        // A new collection naming the same keys: nothing to tell.
        cut.Render(ps => { Base(ps); ps.Add(g => g.DeclaredKeys, (string[])["Control+B", "Control+b"]); });
        Assert.Empty(Js.ClaimsTold.Invocations);

        cut.Render(ps => { Base(ps); ps.Add(g => g.DeclaredKeys, (string[])["Control+i"]); });

        var told = Assert.Single(Js.ClaimsTold.Invocations);
        Assert.Contains("Control+i", (IReadOnlyList<string>)told.Arguments[0]!);
        Assert.DoesNotContain("Control+b", (IReadOnlyList<string>)told.Arguments[0]!);
        Assert.Equal(["Control+i"], (IReadOnlyList<string>)told.Arguments[3]!);
    }
}
