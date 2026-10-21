using ExGrid.Chrome;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace ExSheet.Components;

/// <summary>
/// ExSheet's built-in find panel (ADR-0332 §5): the grid's built-in panel, element for element and class for
/// class, so it looks and reads as the grid's does, with Within added — two choices, the Sheet shown or the
/// Workbook. It stands in the grid's popover, which the grid keys by the opening, so each opening is a new
/// instance. The search, the Focus it moves, the writes and what closes the panel are the grid's and ExSheet's;
/// this draws and calls back.
///
/// <para>Written as the grid writes its own fields: each text field's value is one its own typing outranks a
/// render with (<c>SetUpdatesAttributeName</c>, what <c>@bind</c> writes), and every handler is made once, so a
/// render between two typed characters keeps the field's event ids and, on a circuit, the second is not refused
/// (FD-6). Enter in either field is the form's implicit submission — which an IME's candidate Enter is not —
/// through a hidden default button, Shift held on it making it "previous". The field takes DOM focus for each
/// change of the context's focus request, through the core's focus function (ADR-0204).</para>
/// </summary>
internal sealed class SheetFindPanel : ComponentBase
{
    private static int _panels;

    // The radio group's name, one per panel on the page.
    private readonly string _group = "ex-sheet-find-within-" + Interlocked.Increment(ref _panels).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private ElementReference _field;
    private int _focusedRequest;
    private bool _focusOwed;
    private bool _backwardOnSubmit;
    private bool _withinRead;
    private SheetFindWithin _within;

    // The two fields' text as this panel last wrote it or was handed it. The context is the grid's as of its
    // last render, and the grid does not render for a character typed in the Replace field: this panel renders
    // after its own input event, and a value taken from the context then would write the text before the
    // character back over the field.
    private string _text = "";
    private string _replaceText = "";
    private string? _contextText;
    private string? _contextReplaceText;

    private EventCallback? _submit;
    private EventCallback<ChangeEventArgs>? _textInput;
    private EventCallback<ChangeEventArgs>? _replaceInput;
    private EventCallback<KeyboardEventArgs>? _keyDown;
    private EventCallback<ChangeEventArgs>? _matchCase;
    private EventCallback<ChangeEventArgs>? _wholeCell;
    private EventCallback<MouseEventArgs>? _withinSheet;
    private EventCallback<MouseEventArgs>? _withinWorkbook;
    private EventCallback? _previous;
    private EventCallback? _next;
    private EventCallback? _replace;
    private EventCallback? _replaceAll;

    /// <summary>The panel's contract: the grid's, with Within.</summary>
    [Parameter, EditorRequired] public SheetFindContext Context { get; set; } = default!;

    private FindContext Find => Context.Find;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!_withinRead)
        {
            _withinRead = true;
            _within = Context.Within;
        }
        if (Find.FocusRequest != _focusedRequest)
        {
            _focusedRequest = Find.FocusRequest;
            _focusOwed = true;
        }
        // A text the grid holds that differs from the one it held before is the grid's to show.
        if (Find.Text != _contextText)
        {
            _contextText = Find.Text;
            _text = Find.Text;
        }
        if (Find.ReplaceText != _contextReplaceText)
        {
            _contextReplaceText = Find.ReplaceText;
            _replaceText = Find.ReplaceText;
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_focusOwed || _field.Context is null)
            return;
        _focusOwed = false;
        try
        {
            // The core's focus function, which takes the keyboard only while it is still the grid's: on a
            // circuit the opening focus lands a round trip after Ctrl+F, and a grid pressed in the meantime
            // keeps it (ADR-0204).
            if (Find.TakeFocus is { } take)
                await take(_field, true);
            else
                await _field.FocusAsync();
        }
        catch (Exception ex) when (ex is Microsoft.JSInterop.JSException or Microsoft.JSInterop.JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var words = Context.Words;
        builder.OpenElement(0, "form");
        builder.AddAttribute(1, "class", "ex-popover-form ex-sheet-find");
        builder.AddAttribute(2, "data-ex-claims", "submit");
        builder.AddAttribute(3, "onsubmit", _submit ??= EventCallback.Factory.Create(this, SubmitAsync));
        builder.AddEventPreventDefaultAttribute(4, "onsubmit", true);

        builder.OpenElement(10, "button");
        builder.AddAttribute(11, "type", "submit");
        builder.AddAttribute(12, "class", "ex-find-default");
        builder.AddAttribute(13, "hidden", true);
        builder.AddAttribute(14, "tabindex", "-1");
        builder.AddAttribute(15, "aria-hidden", "true");
        builder.CloseElement();

        builder.OpenElement(20, "input");
        builder.AddAttribute(21, "type", "search");
        builder.AddAttribute(22, "class", "ex-find-field");
        builder.AddAttribute(23, "aria-label", words(FindPanelLabelIds.Field));
        builder.AddAttribute(24, "value", _text);
        builder.AddAttribute(25, "oninput", _textInput ??= EventCallback.Factory.Create<ChangeEventArgs>(this, OnTextInput));
        builder.SetUpdatesAttributeName("value");
        builder.AddAttribute(26, "onkeydown", _keyDown ??= EventCallback.Factory.Create<KeyboardEventArgs>(this, OnFieldKeyDown));
        builder.AddElementReferenceCapture(27, field => _field = field);
        builder.CloseElement();

        Option(builder, 30, "ex-find-match-case", Find.MatchCase, _matchCase ??= EventCallback.Factory.Create<ChangeEventArgs>(this, e => Find.MatchCaseChanged(e.Value is true)), words(FindPanelLabelIds.MatchCase));
        Option(builder, 40, "ex-find-whole-cell", Find.WholeCell, _wholeCell ??= EventCallback.Factory.Create<ChangeEventArgs>(this, e => Find.WholeCellChanged(e.Value is true)), words(FindPanelLabelIds.WholeCell));

        // Within (ADR-0332 §5): the Sheet shown, the default, or the Workbook.
        builder.OpenElement(50, "fieldset");
        builder.AddAttribute(51, "class", "ex-sheet-find-within");
        builder.OpenElement(52, "legend");
        builder.AddContent(53, words(SheetFindLabelIds.Within));
        builder.CloseElement();
        Within(builder, 60, "ex-sheet-find-within-sheet", SheetFindWithin.Sheet, _withinSheet ??= EventCallback.Factory.Create<MouseEventArgs>(this, _ => ChooseWithin(SheetFindWithin.Sheet)), words(SheetFindLabelIds.WithinSheet));
        Within(builder, 70, "ex-sheet-find-within-workbook", SheetFindWithin.Workbook, _withinWorkbook ??= EventCallback.Factory.Create<MouseEventArgs>(this, _ => ChooseWithin(SheetFindWithin.Workbook)), words(SheetFindLabelIds.WithinWorkbook));
        builder.CloseElement();

        builder.OpenElement(80, "div");
        builder.AddAttribute(81, "class", "ex-popover-actions");
        Button(builder, 82, "ex-find-previous", _previous ??= EventCallback.Factory.Create(this, () => Find.Previous()), words(FindPanelLabelIds.Previous));
        Button(builder, 86, "ex-find-next", _next ??= EventCallback.Factory.Create(this, () => Find.Next()), words(FindPanelLabelIds.Next));
        builder.CloseElement();

        if (Find.ReplaceShown)
        {
            // Replace (ADR-0332 §2): the replacement and the two writes, each a click taken in its place in the
            // Input Order (ADR-0240). Enter in this field is still "find next", as in Excel's dialog.
            builder.OpenElement(90, "input");
            builder.AddAttribute(91, "type", "text");
            builder.AddAttribute(92, "class", "ex-find-replace-field");
            builder.AddAttribute(93, "aria-label", words(FindPanelLabelIds.ReplaceField));
            builder.AddAttribute(94, "value", _replaceText);
            builder.AddAttribute(95, "oninput", _replaceInput ??= EventCallback.Factory.Create<ChangeEventArgs>(this, OnReplaceInput));
            builder.SetUpdatesAttributeName("value");
            builder.AddAttribute(96, "onkeydown", _keyDown ??= EventCallback.Factory.Create<KeyboardEventArgs>(this, OnFieldKeyDown));
            builder.CloseElement();

            builder.OpenElement(100, "div");
            builder.AddAttribute(101, "class", "ex-popover-actions");
            Button(builder, 102, "ex-find-replace", _replace ??= EventCallback.Factory.Create(this, () => Find.Replace?.Invoke() ?? Task.CompletedTask), words(FindPanelLabelIds.Replace));
            Button(builder, 106, "ex-find-replace-all", _replaceAll ??= EventCallback.Factory.Create(this, () => Find.ReplaceAll?.Invoke() ?? Task.CompletedTask), words(FindPanelLabelIds.ReplaceAll));
            builder.CloseElement();
        }

        // What the last step or Replace came to, announced: a step that moves nothing is otherwise a key that
        // did nothing and said nothing (ADR-0033).
        builder.OpenElement(110, "div");
        builder.AddAttribute(111, "class", "ex-find-outcome");
        builder.AddAttribute(112, "role", "status");
        builder.AddContent(113, Context.OutcomeText());
        builder.CloseElement();

        builder.CloseElement();
    }

    // An option's tick is a click — a label's click and a Space on the checkbox reach the checkbox as one —
    // claimed on the checkbox alone and taken in its place in the Input Order (ADR-0240), so a step numbered
    // before it searches with the option as it stood. Heard as the change that click makes, which says the
    // checkbox's state.
    private static void Option(RenderTreeBuilder builder, int sequence, string cssClass, bool value, EventCallback<ChangeEventArgs> changed, string label)
    {
        builder.OpenElement(sequence, "label");
        builder.OpenElement(sequence + 1, "input");
        builder.AddAttribute(sequence + 2, "type", "checkbox");
        builder.AddAttribute(sequence + 3, "class", cssClass);
        builder.AddAttribute(sequence + 4, "checked", value);
        builder.AddAttribute(sequence + 5, "data-ex-claims", "click");
        builder.AddAttribute(sequence + 6, "onchange", changed);
        builder.CloseElement();
        builder.AddContent(sequence + 7, label);
        builder.CloseElement();
    }

    // A radio's choice is its click — a label's click, an arrow key in the group and a press all reach the
    // radio as one — claimed on the radio alone and taken in its place in the Input Order (ADR-0240), so a
    // step numbered before it searches where the panel searched then.
    private void Within(RenderTreeBuilder builder, int sequence, string cssClass, SheetFindWithin within, EventCallback<MouseEventArgs> chosen, string label)
    {
        builder.OpenElement(sequence, "label");
        builder.OpenElement(sequence + 1, "input");
        builder.AddAttribute(sequence + 2, "type", "radio");
        builder.AddAttribute(sequence + 3, "name", _group);
        builder.AddAttribute(sequence + 4, "class", cssClass);
        builder.AddAttribute(sequence + 5, "checked", _within == within);
        builder.AddAttribute(sequence + 6, "data-ex-claims", "click");
        builder.AddAttribute(sequence + 7, "onclick", chosen);
        builder.CloseElement();
        builder.AddContent(sequence + 8, label);
        builder.CloseElement();
    }

    private static void Button(RenderTreeBuilder builder, int sequence, string cssClass, EventCallback clicked, string label)
    {
        builder.OpenElement(sequence, "button");
        builder.AddAttribute(sequence + 1, "type", "button");
        builder.AddAttribute(sequence + 2, "class", cssClass);
        builder.AddAttribute(sequence + 3, "data-ex-claims", "click");
        builder.AddAttribute(sequence + 4, "onclick", clicked);
        builder.AddContent(sequence + 5, label);
        builder.CloseElement();
    }

    private void OnTextInput(ChangeEventArgs e)
    {
        _text = e.Value as string ?? "";
        Find.TextChanged(_text);
    }

    private void OnReplaceInput(ChangeEventArgs e)
    {
        _replaceText = e.Value as string ?? "";
        Find.ReplaceTextChanged?.Invoke(_replaceText);
    }

    private Task ChooseWithin(SheetFindWithin within)
    {
        _within = within;
        return Context.WithinChanged(within);
    }

    private void OnFieldKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            _backwardOnSubmit = e.ShiftKey;
    }

    private Task SubmitAsync()
    {
        var backward = _backwardOnSubmit;
        _backwardOnSubmit = false;
        return backward ? Find.Previous() : Find.Next();
    }
}
