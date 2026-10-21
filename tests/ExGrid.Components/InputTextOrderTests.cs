using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

public class InputTextOrderTests : GridTestContext
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ADR0240_a_press_commits_the_text_it_carried_before_the_typing_event_arrives(bool heading)
    {
        var source = new TestSource();
        source.Push(TestRows.Many(50), totalCount: 50);
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, [new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book,
                width: new ColumnWidthSpec(ColumnWidth.Fixed(100)), editable: true)])
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> edit) => edits.Add(edit)));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
            { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10, Detail = 1 });
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("a", false, false, false, false, false, input: 1));

        var viewport = cut.Find(".ex-viewport");
        var text = new GridInputText { Surface = "cell", Text = "all the text", Caret = 12 };
        if (heading)
            await cut.InvokeAsync(() => cut.Instance.InputTakenAt("mousedown", 2, text));
        else
            await cut.InvokeAsync(() => cut.Instance.PressTakenAt("mousedown", 50, 70,
            int.Parse(viewport.GetAttribute("data-ex-first-row")!), 0, 0,
            int.Parse(viewport.GetAttribute("data-ex-sequence")!),
            int.Parse(viewport.GetAttribute("data-ex-layout")!), input: 2,
            fieldText: text));
        await (heading ? cut.Find(".ex-header") : viewport).MouseDownAsync(new MouseEventArgs
            { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 70, Detail = 1 });
        await WhenIdleAsync();

        Assert.Equal("all the text", Assert.Single(edits).Value);
    }
    [Fact]
    public async Task ADR0240_name_box_submission_uses_carried_text_before_older_typing_arrives()
    {
        string? entered = null;
        var source = new TestSource();
        source.Push(TestRows.Many(5), totalCount: 5);
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, [new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book)])
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnNameBoxEntered, (string text) => entered = text));
        await cut.Find("input.ex-name-box").FocusAsync();
        cut.Find("input.ex-name-box").Input("A");
        await cut.InvokeAsync(() => cut.Instance.InputTakenAt("submit", 1,
            new GridInputText { Surface = "name", Text = "A123", Caret = 4 }));
        await cut.Find(".ex-name-box-form").SubmitAsync();
        await WhenIdleAsync();

        Assert.Equal("A123", entered);
        cut.Find("input.ex-name-box").Input("A12");
        cut.Find("input.ex-name-box").Input("A123");
        cut.Find("input.ex-name-box").Input("B456");
        await cut.Find(".ex-name-box-form").SubmitAsync();
        Assert.Equal("B456", entered);
    }

    [Fact]
    public async Task ADR0240_name_box_answer_cannot_restore_the_text_of_a_submission_already_finished()
    {
        var entered = new List<string>();
        var source = new TestSource();
        source.Push(TestRows.Many(5), totalCount: 5);
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, [new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book)])
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.OnNameBoxEntered, (string text) => entered.Add(text)));
        await cut.Find("input.ex-name-box").FocusAsync();
        cut.Find("input.ex-name-box").Input("A123");
        await cut.Find(".ex-name-box-form").SubmitAsync();
        await cut.InvokeAsync(() => cut.Instance.NameBoxAnsweredAsync(1,
            new GridInputText { Surface = "name", Text = "A123", Caret = 4 }));
        await cut.Find("input.ex-name-box").FocusAsync();
        await cut.Find(".ex-name-box-form").SubmitAsync();
        Assert.Equal(["A123"], entered);
    }

}
