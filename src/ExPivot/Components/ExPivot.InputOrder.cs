using ExGrid.Chrome;
using ExGrid.Components;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExPivot.Components;

/// <summary>
/// The pivot's surfaces in its report grid's Input Order (ADR-0240 section 6, ticket 05): the pivot's
/// box is the Input Root, and the Field List, the Pivot Toolbar, their menus and panels, the report's
/// toggles and the details' tabs and dialog put their input in the report grid's order through its
/// seam (<c>ExGrid.InOrderAsync</c>). Each details grid keeps an order of its own, inside an Input Root
/// of its own (decided with the user, 2026-10-22).
///
/// <para>An input's turn ends with the keyboard where it leaves it. A menu or a panel that opens takes
/// the keyboard, and one that closes gives it back to its opener, inside the turn of the input that
/// opened or closed it: the render that draws what takes the keyboard is waited for, and the keyboard
/// is handed on through the grid's guard (<c>HandKeyboardToAsync</c>) before the turn ends. The keys
/// typed meanwhile are held by the grid behind that input and handed on where it left the keyboard, so
/// the local queues of 2026-10-13 are gone. A menu or a panel closed in a turn stands, doing nothing,
/// until its opener has the keyboard, so DOM focus never falls to the page's body in between, where no
/// listener of the grid's hears a key.</para>
/// </summary>
public partial class ExPivot
{
    // The grid's script, imported on its own while the report is empty (ADR-0360).
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly PivotInput _input;

    // The seam the Chrome contexts carry (InOrder, TakeKeyboard), made once so a context's delegates
    // stay the same instances render after render.
    private Func<GridInputKind, Func<Task>, Task>? _inOrderSeam;
    private Func<GridInputKind, Func<Task>, Task> InOrderSeam => _inOrderSeam ??= InOrderAsync;
    private Func<ElementReference, Task> TakeKeyboardSeam => _takeKeyboard ??= TakeKeyboardAsync;

    // How deep the turns under way run: a handling in the order, and the inputs it runs inline.
    private int _turnDepth;

    // The focus request the last turn's end waited for, against _focusSequence: a turn whose handling
    // asked for the keyboard renders before its end, so the control asked for is drawn and has asked.
    private int _focusSettled;

    // The keyboard's move a render asked for during a turn: performed at the turn's end, the last asked.
    private Func<Task>? _keyboardAsked;

    // A render asked for at a turn's end, completed after the render (PivotRendered), and its mark.
    private TaskCompletionSource? _rendered;
    private int _renderMark;
    private Action? _noteRendered;

    /// <summary>The report grid that owns the pivot's Input Order: none while the report is empty, and
    /// none before the grid is drawn — an input is then taken in the order it arrived, behind the turn
    /// under way (ADR-0360).</summary>
    private ExGrid<PivotDisplayRow>? OrderGrid => _report is { IsEmpty: false } ? _grid : null;

    /// <summary>
    /// <paramref name="handling"/>, an input of the pivot's own of <paramref name="kind"/> that its control
    /// claims (<c>data-ex-claims</c>), in its place in the report grid's Input Order, and ended with the
    /// keyboard where it leaves it.
    /// </summary>
    internal Task InOrderAsync(GridInputKind kind, Func<Task> handling)
        => InTurnUnordered ? TurnAsync(handling)
            : OrderGrid is { } grid ? grid.InOrderAsync(kind, () => AfterTurnsAsync(handling, unordered: false))
            : AfterTurnsAsync(handling, unordered: true);

    /// <summary>
    /// <paramref name="handling"/>, an input no control numbered — a key in a draft's field, a drop, a
    /// press that dismisses a menu, a callback of the grid's — after every input received so far, or at
    /// once inside the turn of an input under way.
    /// </summary>
    internal Task InOrderAsync(Func<Task> handling)
        => InTurnUnordered ? TurnAsync(handling)
            : OrderGrid is { } grid ? grid.InOrderAsync(() => AfterTurnsAsync(handling, unordered: false))
            : AfterTurnsAsync(handling, unordered: true);

    // With no report grid nothing numbers the pivot's inputs, but a turn still ends before the next one
    // begins (ADR-0360). A turn's hand-off of the keyboard can wait on the browser — the grid's script
    // loading, a focus a round trip away — and a turn begun meanwhile had its own request parked, then
    // dropped as the first turn ended: Field Settings… pressed while the menu's first command was taking
    // the keyboard opened with the keyboard nowhere. A turn the report grid orders joins the same line, so
    // the report drawn or emptied between two inputs does not let the second overlap the first. An input
    // handled inside a turn is part of it and runs at once, as in the grid's own order: through the grid
    // inside a turn the grid ordered (its flow), and directly inside one it did not.
    private readonly AsyncLocal<object?> _turnFlow = new();
    private object? _turnRunning;
    private bool _turnRunningUnordered;
    private Task _turnsInLine = Task.CompletedTask;

    private bool InTurnFlow => _turnRunning is { } running && ReferenceEquals(_turnFlow.Value, running);

    private bool InTurnUnordered => InTurnFlow && _turnRunningUnordered;

    private Task AfterTurnsAsync(Func<Task> handling, bool unordered)
    {
        if (InTurnFlow)
            return TurnAsync(handling);
        var turn = AfterAsync(_turnsInLine, handling, unordered);
        _turnsInLine = turn;
        return turn;
    }

    private async Task AfterAsync(Task previous, Func<Task> handling, bool unordered)
    {
        try
        {
            await previous;
        }
        catch (Exception)
        {
            // The turn before failed on its own; it has said so to its caller, and this one still runs.
        }
        // A pivot disposed while the input waited has nothing to handle it on.
        if (_disposed)
            return;
        var running = new object();
        _turnRunning = running;
        _turnRunningUnordered = unordered;
        _turnFlow.Value = running;
        try
        {
            await TurnAsync(handling);
        }
        finally
        {
            _turnRunning = null;
        }
    }

    /// <summary><see cref="InOrderAsync(GridInputKind, Func{Task})"/> for work that waits on nothing.</summary>
    internal Task InOrderAsync(GridInputKind kind, Action handling) => InOrderAsync(kind, () =>
    {
        handling();
        return Task.CompletedTask;
    });

    /// <summary>The report grid is not drawn (an empty report): the one it was is gone, and orders nothing.</summary>
    private void ForgetGrid() => _grid = null;

    /// <summary><see cref="InOrderAsync(Func{Task})"/> for work that waits on nothing.</summary>
    internal Task InOrderAsync(Action handling) => InOrderAsync(() =>
    {
        handling();
        return Task.CompletedTask;
    });

    private async Task TurnAsync(Func<Task> handling)
    {
        _turnDepth++;
        try
        {
            await handling();
            if (_turnDepth == 1)
                await KeyboardInTurnAsync();
        }
        finally
        {
            // A move asked once the turn had made its own, or by a turn that threw, is the turn's and
            // goes with it: left parked, it would move the keyboard at the end of a later, unrelated turn.
            if (--_turnDepth == 0)
                _keyboardAsked = null;
        }
    }

    /// <summary>
    /// The end of a turn: when the handling asked for the keyboard, the render that draws the control
    /// asked for, then the keyboard handed to it, then a menu or a panel closed in the turn taken away.
    /// </summary>
    private async Task KeyboardInTurnAsync()
    {
        if (_disposed)
            return;
        if (_focusSequence != _focusSettled || _closingSurface is not null)
        {
            _focusSettled = _focusSequence;
            await RenderedAsync();
        }
        if (_keyboardAsked is { } move)
        {
            _keyboardAsked = null;
            await move();
        }
        if (_closingSurface is not null && !_disposed)
        {
            EndClosing();
            StateHasChanged();
        }
    }

    /// <summary>Renders, and completes once the render's after-render calls have run: every control it
    /// asked to take the keyboard has asked by then.</summary>
    private Task RenderedAsync()
    {
        var rendered = _rendered ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _renderMark++;
        StateHasChanged();
        return rendered.Task;
    }

    private void NoteRendered()
    {
        if (_rendered is { } rendered)
        {
            _rendered = null;
            rendered.TrySetResult();
        }
    }

    private RenderFragment RenderedMark() => builder =>
    {
        builder.OpenComponent<PivotRendered>(0);
        builder.AddComponentParameter(1, nameof(PivotRendered.Mark), _renderMark);
        builder.AddComponentParameter(2, nameof(PivotRendered.Rendered), _noteRendered ??= NoteRendered);
        builder.CloseComponent();
    };

    /// <summary>
    /// A move of the keyboard a render asked for — a control taking it as a menu or a panel opens, an
    /// opener taking it back, the report's grid taking it back: inside a turn, made at the turn's end,
    /// and otherwise at once.
    /// </summary>
    private Task MoveKeyboardAsync(Func<Task> move)
    {
        if (_turnDepth > 0 && !_disposed)
        {
            _keyboardAsked = move;
            return Task.CompletedTask;
        }
        return move();
    }

    /// <summary>A control of the pivot's taking the keyboard when a render asks: in its turn, through the
    /// report grid's guard (<see cref="HandKeyboardToAsync"/>).</summary>
    internal Task TakeKeyboardAsync(ElementReference control) => MoveKeyboardAsync(() => HandKeyboardToAsync(control));

    /// <summary>
    /// Hands the keyboard to <paramref name="control"/> through the report grid's guard, granted only
    /// while the keyboard is still the pivot's (ADR-0070's note of 2026-10-02, ADR-0240 section 6). With
    /// no report grid — the report empty, its first answer on its way, or failed — the grid's script,
    /// imported on its own, focuses the control, or the first control inside the box a Chrome named, as
    /// the grid would have (ADR-0360): a box drawn with <c>display: contents</c> around a design system's
    /// select cannot take focus itself. The control's own focus is still asked where the script is
    /// refused, a script of another version, or where no control took the keyboard.
    /// </summary>
    internal async Task HandKeyboardToAsync(ElementReference control)
    {
        try
        {
            var script = OrderGrid is null ? await LoadedScriptFocusAsync() : null;
            // Checked again once the script is loaded: a report drawn meanwhile hands the keyboard on.
            if (OrderGrid is { } grid)
                await grid.HandKeyboardToAsync(control);
            else if (script is null || !await FocusBoxAsync(script, control))
                await control.FocusAsync();
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
        {
            // The circuit or the control went away first; there is nothing left to focus.
        }
    }

    // The grid's script, loaded the first time the keyboard is handed on with no report grid, and kept
    // for the pivot's life, so each later hand-off is one call, as the control's own focus was (ADR-0360).
    private GridScriptFocus? _scriptFocus;

    /// <summary>The grid's script, loaded and this build's; or none, where it would not load or answered
    /// with another version's stamp, which leaves the control's own focus to be asked.</summary>
    private async Task<GridScriptFocus?> LoadedScriptFocusAsync()
    {
        if (_disposed)
            return null;
        var script = _scriptFocus ??= new GridScriptFocus(JS, Assets);
        try
        {
            return await script.LoadAsync() ? script : null;
        }
        catch (JSException)
        {
            return null;
        }
    }

    /// <summary>Whether a control inside the box took the keyboard from the script; a call the script
    /// threw on leaves the control's own focus to be asked.</summary>
    private static async Task<bool> FocusBoxAsync(GridScriptFocus script, ElementReference control)
    {
        try
        {
            return await script.FocusBoxAsync(control);
        }
        catch (JSException)
        {
            return false;
        }
    }

    private void DisposeScriptFocus()
    {
        if (_scriptFocus is { } focus)
            _ = focus.DisposeAsync().AsTask();
        _scriptFocus = null;
    }

    private void EndTurns()
    {
        _keyboardAsked = null;
        NoteRendered();
    }
}

/// <summary>
/// The pivot's seam into its Input Order and its keyboard guard, cascaded to its built-in parts
/// (ADR-0240 section 6): what a part's control claims goes through <see cref="InOrderAsync(GridInputKind, Func{Task})"/>,
/// and a control takes the keyboard through <see cref="TakeKeyboardAsync"/>.
/// </summary>
internal sealed class PivotInput(ExPivot pivot)
{
    /// <summary>A claimed input in its turn.</summary>
    public Task InOrderAsync(GridInputKind kind, Func<Task> handling) => pivot.InOrderAsync(kind, handling);

    /// <summary>An unnumbered input after every input received so far.</summary>
    public Task InOrderAsync(Func<Task> handling) => pivot.InOrderAsync(handling);

    /// <summary>A control taking the keyboard when a render asks.</summary>
    public Task TakeKeyboardAsync(ElementReference control) => pivot.TakeKeyboardAsync(control);

    /// <summary>A control inside the report's grid taking the keyboard now, through the grid's guard.</summary>
    public Task HandKeyboardToAsync(ElementReference control) => pivot.HandKeyboardToAsync(control);
}

/// <summary>Renders nothing; tells ExPivot after each render of it (<see cref="ExPivot"/>'s turns wait on
/// it, so the controls a render drew have asked for the keyboard before the turn hands it on).
///
/// <para>It renders only when <see cref="Mark"/> changes. On Blazor Server a render's after-render calls run
/// once the browser has acknowledged its batch, and a batch sent before the turn asked can be acknowledged
/// while the turn waits: rendered with every render of ExPivot's, this told of that earlier batch, and the
/// turn handed the keyboard on before the menu it had opened was drawn (a click on Layout ▾ and Enter at
/// once, PV-11's neighbour under the Server host).</para></summary>
internal sealed class PivotRendered : ComponentBase
{
    private int _drawn = -1;

    /// <summary>Changes when ExPivot asks for a render to wait on.</summary>
    [Parameter] public int Mark { get; set; }

    /// <summary>Told after each render.</summary>
    [Parameter] public Action? Rendered { get; set; }

    protected override bool ShouldRender() => Mark != _drawn;

    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) => _drawn = Mark;

    protected override void OnAfterRender(bool firstRender) => Rendered?.Invoke();
}
