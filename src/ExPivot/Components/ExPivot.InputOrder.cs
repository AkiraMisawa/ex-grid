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
    /// none before the grid is drawn — an input is then handled at once, with nothing to order it among.</summary>
    private ExGrid<PivotDisplayRow>? OrderGrid => _report is { IsEmpty: false } ? _grid : null;

    /// <summary>
    /// <paramref name="handling"/>, an input of the pivot's own of <paramref name="kind"/> that its control
    /// claims (<c>data-ex-claims</c>), in its place in the report grid's Input Order, and ended with the
    /// keyboard where it leaves it.
    /// </summary>
    internal Task InOrderAsync(GridInputKind kind, Func<Task> handling)
        => OrderGrid is { } grid ? grid.InOrderAsync(kind, () => TurnAsync(handling)) : TurnAsync(handling);

    /// <summary>
    /// <paramref name="handling"/>, an input no control numbered — a key in a draft's field, a drop, a
    /// press that dismisses a menu, a callback of the grid's — after every input received so far, or at
    /// once inside the turn of an input under way.
    /// </summary>
    internal Task InOrderAsync(Func<Task> handling)
        => OrderGrid is { } grid ? grid.InOrderAsync(() => TurnAsync(handling)) : TurnAsync(handling);

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
    /// while the keyboard is still the pivot's (ADR-0070's note of 2026-10-02, ADR-0240 section 6); with
    /// no report grid, the control's own focus.
    /// </summary>
    internal async Task HandKeyboardToAsync(ElementReference control)
    {
        try
        {
            if (OrderGrid is { } grid)
                await grid.HandKeyboardToAsync(control);
            else
                await control.FocusAsync();
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
        {
            // The circuit or the control went away first; there is nothing left to focus.
        }
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
