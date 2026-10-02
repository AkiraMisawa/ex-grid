using ExSheet.Components;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace ExSheet;

/// <summary>
/// What a Toolbar Item says about itself, before the Sheet Toolbar adds what is the toolbar's — its
/// id, whether an edit is open, where the keyboard is (ADR-0100).
/// </summary>
/// <param name="Kind">What kind of control it is.</param>
/// <param name="Name">Its name, as Excel names it.</param>
/// <param name="Icon">The picture it shows.</param>
public sealed record ToolbarItemFace(ToolbarItemKind Kind, string Name, ToolbarIcon Icon)
{
    /// <summary>The key that does the same, such as <c>Ctrl+B</c>; null for none.</summary>
    public string? Shortcut { get; init; }

    /// <summary>The text it shows; null for none.</summary>
    public string? Text { get; init; }

    /// <summary>For a toggle, whether the Focus cell has it.</summary>
    public bool Pressed { get; init; }

    /// <summary>Why it cannot be pressed even with no edit open; null when it can.</summary>
    public string? DisabledReason { get; init; }

    /// <summary>The choices of a menu or a split control.</summary>
    public IReadOnlyList<ToolbarChoice> Choices { get; init; } = [];

    /// <summary>For a colour control, the colour its face applies.</summary>
    public CellColour? Colour { get; init; }

    /// <summary>A Consumer item's own content.</summary>
    public RenderFragment? Content { get; init; }
}

/// <summary>
/// A Toolbar Item (ADR-0100): one element of a Toolbar Row, which declares what it means and leaves
/// how it looks to the Chrome. It stands inside an ExSheet's <c>ToolbarContent</c>, reads the Sheet
/// through the cascaded <see cref="SheetToolbarContext"/>, and acts through the Sheet's public
/// commands only. While an edit is open it cannot be pressed.
/// </summary>
public abstract class ToolbarItemBase : ComponentBase, IDisposable
{
    private SheetToolbarContext? _joined;
    private ToolbarItemContext? _drawn;
    private int _openRequest;
    private Func<Task>? _invoke;
    private Func<Task>? _choicesClosed;
    private string? _id;

    /// <summary>The Sheet Toolbar this item stands in.</summary>
    [CascadingParameter] public SheetToolbarContext? Toolbar { get; set; }

    /// <summary>The Toolbar Row this item stands in; null for an item of a toolbar that declares no rows.</summary>
    [CascadingParameter] public ToolbarRow? Row { get; set; }

    /// <summary>The scope this item takes its place on the toolbar from.</summary>
    [CascadingParameter] public ToolbarOrder? Order { get; set; }

    /// <summary>Where the item stands on the toolbar, in markup order.</summary>
    internal ToolbarOrder Place { get; private set; } = new();

    /// <summary>
    /// The item's KeyTip letters (ADR-0100). ExSheet's own items carry Excel's; a Consumer's item
    /// carries only what the Consumer declares here, and none otherwise.
    /// </summary>
    [Parameter] public string? KeyTip { get; set; }

    /// <summary>The toolbar, which every item has: an item outside an ExSheet's <c>ToolbarContent</c> is refused.</summary>
    protected SheetToolbarContext Context => Toolbar
        ?? throw new InvalidOperationException($"{GetType().Name} is a Toolbar Item: it stands inside an ExSheet's ToolbarContent (ADR-0100).");

    /// <summary>What the item is and shows now.</summary>
    protected abstract ToolbarItemFace Face();

    /// <summary>What pressing the item does: for a split control, its face. A menu opens its choices instead.</summary>
    protected abstract Task PressAsync();

    /// <summary>The item's KeyTip when the Consumer declared none: Excel's, for ExSheet's own items.</summary>
    protected virtual string? DefaultKeyTip => null;

    /// <summary>Whether a press from the toolbar's keys gives the keyboard back to the Sheet afterwards (ADR-0100).</summary>
    protected virtual bool ReturnsKeyboard => true;

    /// <summary>The KeyTip the item answers to.</summary>
    internal string? KeyTipLetters => KeyTip ?? DefaultKeyTip;

    internal bool IsSeparator => Face().Kind == ToolbarItemKind.Separator;

    internal bool CanPress => !IsSeparator && Describe().Enabled;

    /// <summary>The item as the Chrome is told it.</summary>
    internal ToolbarItemContext Describe()
    {
        var context = Context;
        var face = Face();
        var disabledReason = context.IsEditing ? SheetWords.ToolbarWhileEditing : face.DisabledReason;
        _id ??= context.NextId();
        _invoke ??= () => Face().Kind == ToolbarItemKind.Menu ? OpenChoicesAsync() : Context.RunAsync(PressAsync);
        _choicesClosed ??= () => Context.ReturnKeyboardAsync();
        return new ToolbarItemContext(
            _id, face.Kind, face.Name, face.Shortcut, face.Icon, face.Text, face.Pressed,
            Enabled: face.Kind != ToolbarItemKind.Separator && disabledReason is null,
            disabledReason, _invoke, face.Choices, face.Colour,
            Active: ReferenceEquals(context.Active, this), _openRequest, _choicesClosed, face.Content,
            KeyTipLetters, ShowKeyTip: false);
    }

    /// <summary>A press from the toolbar's keys (ADR-0100): a menu or a split control's arrow opens its
    /// choices; anything else runs, and the keyboard goes back to the Sheet.</summary>
    internal async Task PressFromKeyboardAsync(bool openChoices)
    {
        var face = Face();
        if (face.Kind == ToolbarItemKind.Separator || !Describe().Enabled) return;
        if (openChoices && face.Kind is not (ToolbarItemKind.Menu or ToolbarItemKind.Split)) return;
        if (face.Kind == ToolbarItemKind.Menu || (openChoices && face.Kind == ToolbarItemKind.Split))
        {
            _openRequest++;
            StateHasChanged();
            return;
        }
        await Context.RunAsync(PressAsync);
        if (ReturnsKeyboard) await Context.ReturnKeyboardAsync();
    }

    /// <summary>Opens the item's choices in the Sheet's built-in frame.</summary>
    internal Task OpenChoicesAsync() => Context.OpenChoicesAsync(Face().Name, Face().Choices);

    /// <inheritdoc />
    protected override void OnInitialized() => Place = Order?.Next() ?? Place;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        var context = Context;
        if (ReferenceEquals(context, _joined)) return;
        if (_joined is { } previous)
        {
            previous.Changed -= OnToolbarChanged;
            previous.Leave(this);
        }
        _joined = context;
        context.Join(this);
        context.Changed += OnToolbarChanged;
    }

    /// <inheritdoc />
    protected override bool ShouldRender() => !Describe().Equals(_drawn);

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var drawn = Describe();
        _drawn = drawn;
        builder.AddContent(0, Context.Chrome?.ToolbarItem(drawn) ?? BuiltInToolbarItem.Draw(drawn, this));
    }

    private void OnToolbarChanged()
    {
        if (!Describe().Equals(_drawn)) _ = InvokeAsync(StateHasChanged);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_joined is { } joined)
        {
            joined.Changed -= OnToolbarChanged;
            joined.Leave(this);
        }
        GC.SuppressFinalize(this);
    }
}
