using System.Globalization;
using Bunit;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using ExSheet.Xlsx;
using Xunit;
using SheetComponent = ExSheet.Components.ExSheet;

namespace ExSheet.Components.Tests;

/// <summary>
/// Opening and saving a file at its place in the Input Order (ADR-0330 §10, exsheet-xlsx ticket 06). The
/// application calls <c>OpenAsync</c> with a read still to do: the inputs behind it wait for the document
/// and are taken against the Workbook it opened, the Sheet says the opening is under way, an Escape typed
/// against a stamp that names it cancels it (ADR-0240 §12) and a refused read leaves the Workbook as it was.
/// Landing clears the undo stack; an opening while an edit is open is refused. <c>SaveAsync</c> lends the Workbook as the inputs before it left it, and is
/// refused while an edit is open. Here the reads are held by hand, and the Escape's call is made as the
/// script makes it; what the script reads off the root and numbers is layer 3's.
/// </summary>
public class OpenAndSaveInOrderTests : SheetTestContext
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    // A Workbook of one Sheet, the one shown before any file is opened.
    private static WorkbookDocument Before()
    {
        var workbook = new Workbook(Culture, ["Before"]);
        workbook.Do("Before", SheetEdit.Enter(CellAddress.Parse("A1"), "old"));
        return workbook.ToDocument();
    }

    // The Workbook a file holds: two Sheets, so a key that shows the next one acts only on it.
    private static WorkbookDocument Opened()
    {
        var workbook = new Workbook(Culture, ["One", "Two"]);
        workbook.Do("One", SheetEdit.Enter(CellAddress.Parse("A1"), "21"));
        workbook.Do("Two", SheetEdit.Enter(CellAddress.Parse("A1"), "=One!A1*2"));
        return workbook.ToDocument();
    }

    private IRenderedComponent<SheetComponent> RenderBefore(Action<ComponentParameterCollectionBuilder<SheetComponent>>? more = null) =>
        RenderSheet(ps =>
        {
            ps.Add(s => s.Workbook, Before());
            more?.Invoke(ps);
        });

    private static string Notice(IRenderedComponent<SheetComponent> cut) => cut.Find(".ex-sheet-notice").TextContent;

    private static string[] Tabs(IRenderedComponent<SheetComponent> cut) => [.. cut.FindAll(".ex-sheet-tab").Select(t => t.TextContent)];

    private static string SelectedTab(IRenderedComponent<SheetComponent> cut) =>
        cut.FindAll(".ex-sheet-tab").Single(tab => tab.GetAttribute("aria-selected") == "true").TextContent;

    private static AngleSharp.Dom.IElement GridRoot(IRenderedComponent<SheetComponent> cut) => cut.Find(".ex-grid");

    // The application's Open button: the call made and not awaited, its read held until `read` answers.
    private static async Task<Task<bool>> OpenAsync(IRenderedComponent<SheetComponent> cut, Task<WorkbookDocument> read, List<CancellationToken>? tokens = null)
    {
        Task<bool> opening = Task.FromResult(false);
        await cut.InvokeAsync(() =>
        {
            opening = cut.Instance.OpenAsync(async token =>
            {
                tokens?.Add(token);
                var document = await read.WaitAsync(token);
                return document;
            });
        });
        return opening;
    }

    private static Task KeyAsync(IRenderedComponent<SheetComponent> cut, string key, int input, bool ctrl = false)
    {
        var grid = Grid(cut);
        return grid.InvokeAsync(() => grid.Instance.OnKeyAsync(key, ctrl, false, false, false, false, input: input));
    }

    // The work the grid's Settled Stamp names as placed, which the script reads at an Escape's keydown.
    private static string? Placed(IRenderedComponent<SheetComponent> cut)
        => cut.Find(".ex-grid > [data-ex-answered]").GetAttribute("data-ex-work-placed");

    // The script's Escape, typed against a stamp that names the opening: a cancel naming it, numbered `input`.
    private static async Task<Task> EscapeAsync(IRenderedComponent<SheetComponent> cut, int input)
    {
        var grid = Grid(cut);
        var work = int.Parse(Placed(cut) ?? "0", CultureInfo.InvariantCulture);
        Task sent = Task.CompletedTask;
        await grid.InvokeAsync(() => { sent = grid.Instance.OnWorkCancelAsync(work, input); });
        return sent;
    }

    [Fact] // ADR-0330 §10: a key typed while the file is read waits for it, and is taken against the Workbook it opened
    public async Task ADR0330_a_key_typed_while_a_file_opens_lands_on_the_opened_workbook()
    {
        var cut = RenderBefore();
        var read = new TaskCompletionSource<WorkbookDocument>();

        var opening = await OpenAsync(cut, read.Task);
        Task next = Task.CompletedTask;
        await cut.InvokeAsync(() => { next = KeyAsync(cut, "PageDown", 1, ctrl: true); });

        // Still reading: the key behind the opening has done nothing to the Workbook before it.
        Assert.Equal(["Before"], Tabs(cut));
        Assert.False(next.IsCompleted);

        read.SetResult(Opened());
        Assert.True(await opening);
        await next;

        Assert.Equal(["One", "Two"], Tabs(cut));
        Assert.Equal("Two", SelectedTab(cut));
    }

    [Fact] // ADR-0330 §10, ADR-0240 §12: while the file is read the Sheet says so and its grid is busy; landed, it says so and is not
    public async Task ADR0330_an_opening_is_said_while_under_way_and_when_it_lands()
    {
        var cut = RenderBefore();
        var read = new TaskCompletionSource<WorkbookDocument>();

        var opening = await OpenAsync(cut, read.Task);

        Assert.Equal("Opening a file. Press Escape in the Sheet to cancel.", Notice(cut));
        Assert.Equal("true", GridRoot(cut).GetAttribute("aria-busy"));
        Assert.NotNull(GridRoot(cut).GetAttribute("data-ex-work"));

        read.SetResult(Opened());
        await opening;

        Assert.Equal("The file was opened.", Notice(cut));
        Assert.Null(GridRoot(cut).GetAttribute("aria-busy"));
        Assert.Null(GridRoot(cut).GetAttribute("data-ex-work"));
    }

    [Fact] // ADR-0330 §10, ADR-0240 §12: an Escape while the file is read cancels it; the Workbook is as it was, and the Sheet says so
    public async Task ADR0330_escape_cancels_an_opening_and_leaves_the_workbook_as_it_was()
    {
        var cut = RenderBefore();
        var read = new TaskCompletionSource<WorkbookDocument>();
        List<CancellationToken> tokens = [];
        var opening = await OpenAsync(cut, read.Task, tokens);

        var escape = await EscapeAsync(cut, 1);

        Assert.False(await opening);
        await escape;
        Assert.True(tokens.Single().IsCancellationRequested);
        Assert.Equal(["Before"], Tabs(cut));
        Assert.Equal(Before().ToJson(), cut.Instance.ToWorkbookDocument().ToJson());
        Assert.Equal("Nothing was opened: the opening was cancelled. The Workbook is as it was.", Notice(cut));
        Assert.Null(GridRoot(cut).GetAttribute("aria-busy"));
    }

    [Fact] // ADR-0240 §12: a read that lands whatever the token says was past the point a cancel could stop it, and the Escape's turn says so
    public async Task ADR0330_an_escape_that_finds_the_file_opened_says_so()
    {
        var cut = RenderBefore();
        var read = new TaskCompletionSource<WorkbookDocument>();
        Task<bool> opening = Task.FromResult(false);
        // A read that does not look at its token: it answers the document whatever happens.
        await cut.InvokeAsync(() => { opening = cut.Instance.OpenAsync(_ => read.Task); });

        var escape = await EscapeAsync(cut, 1);
        read.SetResult(Opened());

        // The read answered, and the opening looks once more before it lands: cancelled by then, nothing opens.
        Assert.False(await opening);
        await escape;
        Assert.Equal(["Before"], Tabs(cut));

        // Landed, then the Escape: the cancel finds the file opened, and its turn says so.
        var again = new TaskCompletionSource<WorkbookDocument>();
        await cut.InvokeAsync(() => { opening = cut.Instance.OpenAsync(_ => again.Task); });
        var work = GridRoot(cut).GetAttribute("data-ex-work");
        again.SetResult(Opened());
        Assert.True(await opening);
        var grid = Grid(cut);
        Task late = Task.CompletedTask;
        await grid.InvokeAsync(() => { late = grid.Instance.OnWorkCancelAsync(int.Parse(work!, CultureInfo.InvariantCulture), 2); });
        await late;

        Assert.Equal(["One", "Two"], Tabs(cut));
        Assert.Equal("The file had already opened: Escape cancelled nothing.", Notice(cut));
    }

    [Fact] // ADR-0330 §10: a refused read leaves the Workbook as it was, says so by name, and is the caller's
    public async Task ADR0330_a_refused_read_leaves_the_workbook_and_says_why()
    {
        var cut = RenderBefore();
        Task<bool> opening = Task.FromResult(false);
        await cut.InvokeAsync(() =>
        {
            opening = cut.Instance.OpenAsync(_ => Task.FromException<WorkbookDocument>(
                new XlsxOpenException(XlsxOpenRefusal.UnreadableFormula, "The Formula in Sheet1!B2 cannot be read.", "Sheet1", "B2")));
        });

        var refused = await Assert.ThrowsAsync<XlsxOpenException>(() => opening);

        Assert.Equal("B2", refused.Cell);
        Assert.Equal(["Before"], Tabs(cut));
        Assert.Equal("Nothing was opened, and the Workbook is as it was. " + refused.Message, Notice(cut));
        Assert.Null(GridRoot(cut).GetAttribute("aria-busy"));
    }

    [Fact] // ADR-0330 §10, ADR-0048: the opened Workbook clears the undo stack, and the application hears it
    public async Task ADR0330_an_opened_workbook_clears_the_undo_stack_and_is_raised()
    {
        WorkbookDocument? raised = null;
        var cut = RenderBefore(ps => ps.Add(s => s.WorkbookChanged, (WorkbookDocument d) => raised = d));
        await cut.InvokeAsync(() => cut.Instance.DoAsync("Before", SheetEdit.Enter(CellAddress.Parse("B1"), "5")));
        Assert.True(cut.Instance.CanUndo);

        var opening = await OpenAsync(cut, Task.FromResult(Opened()));
        Assert.True(await opening);

        Assert.False(cut.Instance.CanUndo);
        Assert.NotNull(raised);
        Assert.Equal(["One", "Two"], raised!.Sheets.Select(s => s.Name));
    }

    [Fact] // ADR-0330 §10, ADR-0048: an opening while an edit is open is refused by name and said, nothing is read, and the typing goes on into the editor
    public async Task ADR0330_an_opening_while_an_edit_is_open_is_refused_by_name()
    {
        var cut = RenderBefore();
        await GoToAsync(cut, "A1");
        await PressAsync(cut, "9");
        await TypeAsync(cut, "99");
        var read = false;

        var refused = await Assert.ThrowsAsync<SheetRefusedException>(() => cut.InvokeAsync(() => cut.Instance.OpenAsync(_ =>
        {
            read = true;
            return Task.FromResult(Opened());
        })));

        Assert.Equal(SheetRefusalReason.EditIsOpen, refused.Refusal.Reason);
        Assert.Equal("Nothing was opened: a cell is being edited. Press Enter to commit the edit or Escape to cancel it, then open the file again.", Notice(cut));
        Assert.False(read);
        Assert.True(cut.Instance.IsEditing);
        Assert.Null(GridRoot(cut).GetAttribute("aria-busy"));

        await TypeAsync(cut, "997");
        await PressAsync(cut, "Enter");
        Assert.Equal(["Before"], Tabs(cut));
        Assert.Equal(997, Workbook.Open(cut.Instance.ToWorkbookDocument()).Sheets[0].GetValue(CellAddress.Parse("A1"))!.Value.Number);
    }

    [Fact] // ADR-0240 §12, ADR-0330 §10: from the call on the stamp names the opening, though it still waits for its turn and the root names nothing, and an Escape typed against it cancels it
    public async Task ADR0330_a_plain_escape_cancels_an_opening_still_waiting_for_its_turn()
    {
        var cut = RenderBefore();
        var holds = new TaskCompletionSource();
        var grid = Grid(cut);
        Task held = Task.CompletedTask;
        await grid.InvokeAsync(() => { held = grid.Instance.InOrderAsync(() => holds.Task); });
        var read = new TaskCompletionSource<WorkbookDocument>();
        List<CancellationToken> tokens = [];
        var opening = await OpenAsync(cut, read.Task, tokens);
        Assert.Null(GridRoot(cut).GetAttribute("data-ex-work"));
        Assert.NotNull(Placed(cut));

        var escape = await EscapeAsync(cut, 1);
        holds.SetResult();
        await held;

        Assert.False(await opening);
        await escape;
        Assert.Equal(["Before"], Tabs(cut));
        Assert.Equal("Nothing was opened: the opening was cancelled. The Workbook is as it was.", Notice(cut));
    }

    [Fact] // ADR-0240 §12, ADR-0330 §10: an opening an Escape cancelled while it waited ends cancelled in its turn, though an edit was opened before it: never refused as an opening while editing
    public async Task ADR0330_an_opening_cancelled_while_it_waits_is_cancelled_though_an_edit_opened_before_its_turn()
    {
        var cut = RenderBefore();
        await GoToAsync(cut, "A1");
        var holds = new TaskCompletionSource();
        var grid = Grid(cut);
        Task held = Task.CompletedTask;
        await grid.InvokeAsync(() => { held = grid.Instance.InOrderAsync(() => holds.Task); });
        // A key typed before the opening, waiting behind the hold: it opens an edit before the opening's turn.
        Task typed = Task.CompletedTask;
        await cut.InvokeAsync(() => { typed = KeyAsync(cut, "9", 1); });
        var read = false;
        Task<bool> opening = Task.FromResult(true);
        await cut.InvokeAsync(() =>
        {
            opening = cut.Instance.OpenAsync(_ =>
            {
                read = true;
                return Task.FromResult(Opened());
            });
        });

        var escape = await EscapeAsync(cut, 2);
        holds.SetResult();
        await held;
        await typed;

        Assert.False(await opening);
        await escape;
        Assert.False(read);
        Assert.True(cut.Instance.IsEditing);
        Assert.Equal(["Before"], Tabs(cut));
        Assert.Equal("Nothing was opened: the opening was cancelled. The Workbook is as it was.", Notice(cut));
    }

    [Fact] // ADR-0240 §12: an Escape naming an opening that a refused read ended finds nothing to cancel, and says so
    public async Task ADR0330_an_escape_after_a_refused_read_says_there_was_nothing_to_cancel()
    {
        var cut = RenderBefore();
        var read = new TaskCompletionSource<WorkbookDocument>();
        var opening = await OpenAsync(cut, read.Task);
        var work = int.Parse(Placed(cut)!, CultureInfo.InvariantCulture);
        read.SetException(new IOException("The file is larger than the 10,000,000 bytes allowed."));
        await Assert.ThrowsAsync<IOException>(() => opening);

        var grid = Grid(cut);
        Task late = Task.CompletedTask;
        await grid.InvokeAsync(() => { late = grid.Instance.OnWorkCancelAsync(work, 1); });
        await late;

        Assert.Equal("There was nothing to cancel: the opening had already ended.", Notice(cut));
    }

    [Fact] // ADR-0330 §10: what a handler of the application's throws once the file has landed is the handler's, never read as a cancel
    public async Task ADR0330_a_handler_failing_after_the_file_landed_is_not_a_cancel()
    {
        var cut = RenderBefore(ps => ps.Add(s => s.WorkbookChanged, (WorkbookDocument _) => throw new OperationCanceledException("the handler's own")));

        var opening = await OpenAsync(cut, Task.FromResult(Opened()));

        var failed = await Assert.ThrowsAsync<OperationCanceledException>(() => opening);
        Assert.Equal("the handler's own", failed.Message);
        Assert.Equal(["One", "Two"], Tabs(cut));
        Assert.Equal("The file was opened.", Notice(cut));
    }

    [Fact] // ADR-0330 §10: a Sheet disposed while a file is read cancels the read, and nothing lands or is raised
    public async Task ADR0330_a_sheet_disposed_while_a_file_is_read_lands_nothing()
    {
        var raised = 0;
        var cut = RenderBefore(ps => ps.Add(s => s.WorkbookChanged, (WorkbookDocument _) => raised++));
        var read = new TaskCompletionSource<WorkbookDocument>();
        Task<bool> opening = Task.FromResult(true);
        List<CancellationToken> tokens = [];
        // A read that does not look at its token: it answers after the Sheet has gone.
        await cut.InvokeAsync(() =>
        {
            opening = cut.Instance.OpenAsync(token =>
            {
                tokens.Add(token);
                return read.Task;
            });
        });

        await DisposeComponentsAsync();
        read.SetResult(Opened());

        Assert.False(await opening);
        Assert.True(tokens.Single().IsCancellationRequested);
        Assert.Equal(0, raised);
    }

    [Fact] // ADR-0330 §10: a save is taken at its place: the Workbook as the input before it left it, not as the one after it leaves it
    public async Task ADR0330_a_save_is_taken_against_the_workbook_as_the_inputs_before_it_left_it()
    {
        var cut = RenderBefore();
        var read = new TaskCompletionSource<WorkbookDocument>();
        var opening = await OpenAsync(cut, read.Task);
        List<string[]> seen = [];
        Task saving = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            saving = cut.Instance.SaveAsync(workbook =>
            {
                seen.Add([.. workbook.Sheets.Select(s => s.Name)]);
                return Task.CompletedTask;
            });
        });

        // Behind the opening, the save has not looked at the Workbook yet.
        Assert.Empty(seen);
        read.SetResult(Opened());
        await opening;
        await saving;

        Assert.Equal(["One", "Two"], seen.Single());
    }

    [Fact] // ADR-0330 §10, ADR-0048: a save while an edit is open is refused by name, and nothing is handed to the writer
    public async Task ADR0330_a_save_while_an_edit_is_open_is_refused_by_name()
    {
        var cut = RenderBefore();
        await GoToAsync(cut, "A1");
        await PressAsync(cut, "9");
        var written = false;

        var refused = await Assert.ThrowsAsync<SheetRefusedException>(() => cut.InvokeAsync(() => cut.Instance.SaveAsync(_ =>
        {
            written = true;
            return Task.CompletedTask;
        })));

        Assert.Equal(SheetRefusalReason.EditIsOpen, refused.Refusal.Reason);
        Assert.Contains("Nothing was saved: a cell is being edited.", refused.Message);
        Assert.False(written);
        Assert.True(cut.Instance.IsEditing);
    }

    [Fact] // ADR-0330 §6, §10: a Workbook saved through ExSheet.Xlsx and opened again through it is the same Workbook
    public async Task ADR0330_a_workbook_saved_and_opened_through_the_xlsx_package_comes_back()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Workbook, Opened()));
        using var file = new MemoryStream();

        await cut.InvokeAsync(() => cut.Instance.SaveAsync(workbook => XlsxWorkbookWriter.WriteAsync(workbook, file).AsTask()));
        var bytes = file.ToArray();
        var before = cut.Instance.ToWorkbookDocument().ToJson();

        var other = RenderBefore();
        XlsxOpenResult? result = null;
        var opened = await other.InvokeAsync(() => other.Instance.OpenAsync(async token =>
        {
            result = await XlsxWorkbookReader.ReadAsync(new MemoryStream(bytes), Culture, null, token);
            return result.Document;
        }));

        Assert.True(opened);
        Assert.True(result!.Report.IsEmpty);
        Assert.Equal(before, other.Instance.ToWorkbookDocument().ToJson());
    }
}
