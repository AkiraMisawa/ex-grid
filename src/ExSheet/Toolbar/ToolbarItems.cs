using ExSheet.Engine;
using Microsoft.AspNetCore.Components;

namespace ExSheet;

// ExSheet's Toolbar Items (ADR-0100). Each acts through the Sheet's public commands only —
// SetCellFormatAsync, CellFormatAt through the toolbar's Focus format, OpenFormatCellsAsync — and
// carries Excel's Home tab KeyTip where Excel has one (a reading until a Windows run reads them all).

/// <summary>A toggle that sets one Font emphasis, on or off as the Focus cell gives it, as the key does (ADR-0071).</summary>
public abstract class FontToggleItem : ToolbarItemBase
{
    private readonly Func<CellFormat, CellFormatChange> _toggle;

    private protected FontToggleItem(Func<CellFormat, CellFormatChange> toggle) => _toggle = toggle;

    /// <summary>Whether the Focus cell has the emphasis.</summary>
    protected abstract bool Has(CellFont font);

    /// <inheritdoc />
    protected override Task PressAsync() => Context.Sheet.SetCellFormatFromFocusAsync(_toggle);

    private protected ToolbarItemFace Toggle(string name, ToolbarIcon icon, string shortcut) =>
        new(ToolbarItemKind.Toggle, name, icon) { Shortcut = shortcut, Pressed = Has(Context.FocusFormat.Font) };
}

/// <summary>Bold (ADR-0100): Ctrl+B's toggle.</summary>
public sealed class BoldItem : FontToggleItem
{
    /// <summary>Bold, on or off as the Focus cell gives it.</summary>
    public BoldItem() : base(focus => new() { Bold = !focus.Font.Bold }) { }

    /// <inheritdoc />
    protected override bool Has(CellFont font) => font.Bold;

    /// <inheritdoc />
    protected override ToolbarItemFace Face() => Toggle("Bold", ToolbarIcon.Bold, "Ctrl+B");

    /// <inheritdoc />
    protected override string? DefaultKeyTip => "1";
}

/// <summary>Italic (ADR-0100): Ctrl+I's toggle.</summary>
public sealed class ItalicItem : FontToggleItem
{
    /// <summary>Italic, on or off as the Focus cell gives it.</summary>
    public ItalicItem() : base(focus => new() { Italic = !focus.Font.Italic }) { }

    /// <inheritdoc />
    protected override bool Has(CellFont font) => font.Italic;

    /// <inheritdoc />
    protected override ToolbarItemFace Face() => Toggle("Italic", ToolbarIcon.Italic, "Ctrl+I");

    /// <inheritdoc />
    protected override string? DefaultKeyTip => "2";
}

/// <summary>A single underline (ADR-0100): Ctrl+U's toggle.</summary>
public sealed class UnderlineItem : FontToggleItem
{
    /// <summary>A single underline, on or off as the Focus cell gives it.</summary>
    public UnderlineItem() : base(focus => new() { Underline = !focus.Font.Underline }) { }

    /// <inheritdoc />
    protected override bool Has(CellFont font) => font.Underline;

    /// <inheritdoc />
    protected override ToolbarItemFace Face() => Toggle("Underline", ToolbarIcon.Underline, "Ctrl+U");

    /// <inheritdoc />
    protected override string? DefaultKeyTip => "3";
}

/// <summary>
/// Strikethrough (ADR-0100): Ctrl+5's toggle. It sets the Font as Format Cells' Font tab does, which
/// widens no column. Excel's Home tab has no KeyTip for it; ExSheet's own is 4, a letter Excel's Home
/// tab does not use (a reading until a Windows run reads them all).
/// </summary>
public sealed class StrikethroughItem : FontToggleItem
{
    /// <summary>Strikethrough, on or off as the Focus cell gives it.</summary>
    public StrikethroughItem() : base(focus => new() { Strikethrough = !focus.Font.Strikethrough }) { }

    /// <inheritdoc />
    protected override bool Has(CellFont font) => font.Strikethrough;

    /// <inheritdoc />
    protected override ToolbarItemFace Face() => Toggle("Strikethrough", ToolbarIcon.Strikethrough, "Ctrl+5");

    /// <inheritdoc />
    protected override string? DefaultKeyTip => "4";
}

/// <summary>
/// A split control over a colour, as Excel's Font Color and Fill Color are (ADR-0100): the face sets
/// the colour last chosen, and the arrow opens the palette — the colour that takes it away, then the
/// Office theme's colours and the ten standard ones, as Format Cells offers them.
/// </summary>
public abstract class ColourItem : ToolbarItemBase
{
    private CellColour _last;
    private IReadOnlyList<ToolbarChoice>? _choices;
    private CellColour? _choicesFor;

    private protected ColourItem(CellColour first) => _last = first;

    /// <summary>The change that sets <paramref name="colour"/>.</summary>
    private protected abstract CellFormatChange Setting(CellColour colour);

    /// <summary>What the first choice, which takes the colour away, is called: Automatic, No Fill.</summary>
    private protected abstract string TakenAway { get; }

    /// <summary>The colour the Focus cell has; null where it has none of its own (Automatic, No Fill).</summary>
    private protected abstract CellColour? FocusColour { get; }

    /// <inheritdoc />
    protected override Task PressAsync() => Context.Sheet.SetCellFormatAsync(Setting(_last));

    private protected ToolbarItemFace Split(string name, ToolbarIcon icon) =>
        new(ToolbarItemKind.Split, name, icon) { Colour = _last, Choices = Choices() };

    private IReadOnlyList<ToolbarChoice> Choices()
    {
        // The colour the Focus cell has is the one marked; null where it has none.
        var focus = FocusColour;
        if (_choices is not null && _choicesFor == focus) return _choices;
        _choicesFor = focus;
        var swatches = FormatCellsOffer.ThemeColours.SelectMany(row => row).Concat(FormatCellsOffer.StandardColours);
        _choices =
        [
            new(TakenAway, () => Choose(CellColour.Automatic), Selected: focus is null),
            .. swatches.Select(swatch => new ToolbarChoice(swatch.Name, () => Choose(swatch.Colour), Colour: swatch.Colour, Selected: swatch.Colour == focus)),
        ];
        return _choices;
    }

    private Task Choose(CellColour colour) => Context.RunAsync(() =>
    {
        // The face sets what was chosen last, as Excel's does; taking the colour away is not kept.
        if (!colour.IsAutomatic) _last = colour;
        return Context.Sheet.SetCellFormatAsync(Setting(colour));
    });
}

/// <summary>The Font colour (ADR-0100): Excel's Font Color, red on its face until another is chosen.</summary>
public sealed class FontColourItem : ColourItem
{
    /// <summary>The Font colour, red on the face as Excel's starts.</summary>
    public FontColourItem() : base(CellColour.FromRgb(0xFF0000)) { }

    private protected override CellFormatChange Setting(CellColour colour) => new() { FontColour = colour };

    private protected override string TakenAway => "Automatic";

    private protected override CellColour? FocusColour => Context.FocusFormat.Font.Colour is { IsAutomatic: false } colour ? colour : null;

    /// <inheritdoc />
    protected override ToolbarItemFace Face() => Split("Font Colour", ToolbarIcon.FontColour);

    /// <inheritdoc />
    protected override string? DefaultKeyTip => "FC";
}

/// <summary>The Fill (ADR-0100): Excel's Fill Color, yellow on its face until another is chosen.</summary>
public sealed class FillItem : ColourItem
{
    /// <summary>The Fill, yellow on the face as Excel's starts.</summary>
    public FillItem() : base(CellColour.FromRgb(0xFFFF00)) { }

    private protected override CellFormatChange Setting(CellColour colour) =>
        new() { Fill = colour.IsAutomatic ? CellFill.None : CellFill.Solid(colour) };

    private protected override string TakenAway => "No Fill";

    private protected override CellColour? FocusColour => Context.FocusFormat.Fill.Colour;

    /// <inheritdoc />
    protected override ToolbarItemFace Face() => Split("Fill Colour", ToolbarIcon.Fill);

    /// <inheritdoc />
    protected override string? DefaultKeyTip => "H";
}

/// <summary>
/// Borders (ADR-0100): Excel's Borders split control. The face sets the preset chosen last — Bottom
/// Border until another is chosen — and the arrow lists Excel's presets, each relative to every
/// selected range, as Format Cells' are.
/// </summary>
public sealed class BordersItem : ToolbarItemBase
{
    private static readonly BorderLine Thin = new(BorderLineStyle.Thin);
    private static readonly BorderLine Thick = new(BorderLineStyle.Thick);

    private static readonly (string Name, BorderChange Change)[] Presets =
    [
        ("Bottom Border", new BorderChange { Bottom = Thin }),
        ("Top Border", new BorderChange { Top = Thin }),
        ("Left Border", new BorderChange { Left = Thin }),
        ("Right Border", new BorderChange { Right = Thin }),
        ("No Border", BorderChange.None),
        ("All Borders", BorderChange.Outline(Thin) with { InsideHorizontal = Thin, InsideVertical = Thin }),
        ("Outside Borders", BorderChange.Outline(Thin)),
        ("Thick Outside Borders", BorderChange.Outline(Thick)),
    ];

    private int _last;
    private IReadOnlyList<ToolbarChoice>? _choices;

    /// <inheritdoc />
    protected override Task PressAsync() => Context.Sheet.SetCellFormatAsync(new CellFormatChange { Borders = Presets[_last].Change });

    /// <inheritdoc />
    protected override ToolbarItemFace Face()
    {
        _choices ??= [.. Presets.Select((preset, at) => new ToolbarChoice(preset.Name, () => Choose(at)))];
        return new(ToolbarItemKind.Split, "Borders", ToolbarIcon.Borders) { Choices = _choices };
    }

    /// <inheritdoc />
    protected override string? DefaultKeyTip => "B";

    private Task Choose(int at) => Context.RunAsync(() =>
    {
        _last = at;
        return Context.Sheet.SetCellFormatAsync(new CellFormatChange { Borders = Presets[at].Change });
    });
}

/// <summary>
/// One horizontal alignment (ADR-0100), pressed while the Focus cell has it. Pressing it sets it, and
/// pressing it again where the Focus cell has it sets General, as Excel's alignment buttons do.
/// </summary>
public abstract class AlignmentItem : ToolbarItemBase
{
    private readonly HorizontalAlignment _alignment;

    private protected AlignmentItem(HorizontalAlignment alignment) => _alignment = alignment;

    /// <inheritdoc />
    protected override Task PressAsync() => Context.Sheet.SetCellFormatFromFocusAsync(focus =>
        new CellFormatChange { Alignment = focus.Alignment == _alignment ? HorizontalAlignment.General : _alignment });

    private protected ToolbarItemFace Toggle(string name, ToolbarIcon icon) =>
        new(ToolbarItemKind.Toggle, name, icon) { Pressed = Context.FocusFormat.Alignment == _alignment };
}

/// <summary>Left alignment (ADR-0100).</summary>
public sealed class AlignLeftItem : AlignmentItem
{
    /// <summary>Left alignment.</summary>
    public AlignLeftItem() : base(HorizontalAlignment.Left) { }

    /// <inheritdoc />
    protected override ToolbarItemFace Face() => Toggle("Align Left", ToolbarIcon.AlignLeft);

    /// <inheritdoc />
    protected override string? DefaultKeyTip => "AL";
}

/// <summary>Centre alignment (ADR-0100).</summary>
public sealed class AlignCenterItem : AlignmentItem
{
    /// <summary>Centre alignment.</summary>
    public AlignCenterItem() : base(HorizontalAlignment.Center) { }

    /// <inheritdoc />
    protected override ToolbarItemFace Face() => Toggle("Centre", ToolbarIcon.AlignCenter);

    /// <inheritdoc />
    protected override string? DefaultKeyTip => "AC";
}

/// <summary>Right alignment (ADR-0100).</summary>
public sealed class AlignRightItem : AlignmentItem
{
    /// <summary>Right alignment.</summary>
    public AlignRightItem() : base(HorizontalAlignment.Right) { }

    /// <inheritdoc />
    protected override ToolbarItemFace Face() => Toggle("Align Right", ToolbarIcon.AlignRight);

    /// <inheritdoc />
    protected override string? DefaultKeyTip => "AR";
}

/// <summary>
/// The Number Format (ADR-0100): Excel's Number Format box, showing the Focus cell's category and
/// listing Excel's categories in Excel's order. Each sets what Format Cells' Number tab sets when the
/// category is chosen; Accounting and Fraction are shown disabled with Format Cells' reasons, and
/// More Number Formats… opens Format Cells.
/// </summary>
public sealed class NumberFormatItem : ToolbarItemBase
{
    private IReadOnlyList<ToolbarChoice>? _choices;
    private (NumberFormatCategory Category, System.Globalization.CultureInfo Culture)? _for;

    /// <inheritdoc />
    protected override Task PressAsync() => Task.CompletedTask;

    /// <inheritdoc />
    protected override ToolbarItemFace Face()
    {
        var culture = Context.Culture;
        var shown = NumberFormatCodes.Recognise(Context.FocusFormat.NumberFormat, culture).Category;
        if (_choices is null || _for != (shown, culture))
        {
            _for = (shown, culture);
            _choices = Choices(shown, culture);
        }
        return new(ToolbarItemKind.Menu, "Number Format", ToolbarIcon.None) { Text = FormatCellsOffer.NameOf(shown), Choices = _choices };
    }

    /// <inheritdoc />
    protected override string? DefaultKeyTip => "N";

    private IReadOnlyList<ToolbarChoice> Choices(NumberFormatCategory shown, System.Globalization.CultureInfo culture)
    {
        var choices = new List<ToolbarChoice>();
        foreach (var offer in FormatCellsOffer.Categories)
        {
            if (offer.Category is NumberFormatCategory.Special) continue; // not on Excel's ribbon list
            if (offer.Category is NumberFormatCategory.Custom)
            {
                choices.Add(new("More Number Formats…", () => Context.RunAsync(() => Context.Sheet.OpenFormatCellsAsync())));
                continue;
            }
            var label = FormatCellsOffer.NameOf(offer.Category);
            if (offer.DisabledReason is { } reason)
            {
                choices.Add(new(label, () => Task.CompletedTask, reason));
                continue;
            }
            var format = CodeFor(offer.Category, culture);
            choices.Add(new(label, () => Context.RunAsync(() => Context.Sheet.SetNumberFormatAsync(format)), Selected: offer.Category == shown));
        }
        return choices;
    }

    // What Format Cells' Number tab writes when the category is chosen with its defaults.
    private static NumberFormat CodeFor(NumberFormatCategory category, System.Globalization.CultureInfo culture) => category switch
    {
        NumberFormatCategory.General => NumberFormat.General,
        NumberFormatCategory.Number => NumberFormat.Parse(NumberFormatCodes.Number(2, false, 0)),
        NumberFormatCategory.Currency => NumberFormat.Parse(NumberFormatCodes.Currency(NumberFormatCodes.DefaultPlaces(category, culture), 0, culture)),
        NumberFormatCategory.Date => NumberFormat.Parse(NumberFormatCodes.DateTypes[0]),
        NumberFormatCategory.Time => NumberFormat.Parse(NumberFormatCodes.TimeTypes[0]),
        NumberFormatCategory.Percentage => NumberFormat.Parse(NumberFormatCodes.Percentage(2)),
        NumberFormatCategory.Scientific => NumberFormat.Parse(NumberFormatCodes.Scientific(2)),
        NumberFormatCategory.Text => NumberFormat.Parse("@"),
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Not a category the toolbar sets."),
    };
}

/// <summary>The percent style (ADR-0100): Excel's Percent Style, <c>0%</c>, as Ctrl+Shift+% sets it.</summary>
public sealed class PercentItem : ToolbarItemBase
{
    private static readonly NumberFormat Percent = NumberFormat.Parse("0%");

    /// <inheritdoc />
    protected override Task PressAsync() => Context.Sheet.SetNumberFormatAsync(Percent);

    /// <inheritdoc />
    protected override ToolbarItemFace Face() => new(ToolbarItemKind.Button, "Percent Style", ToolbarIcon.Percent) { Shortcut = "Ctrl+Shift+%" };

    /// <inheritdoc />
    protected override string? DefaultKeyTip => "P";
}

/// <summary>
/// The comma style (ADR-0100). Excel's Comma Style sets the built-in Comma style, whose code pads each
/// value with <c>*</c> as Accounting does, which ExSheet does not read. So it is shown and disabled,
/// with the reason, as Format Cells shows Accounting, rather than set as something else under
/// Excel's name.
/// </summary>
public sealed class CommaItem : ToolbarItemBase
{
    /// <inheritdoc />
    protected override Task PressAsync() => Task.CompletedTask;

    /// <inheritdoc />
    protected override ToolbarItemFace Face() => new(ToolbarItemKind.Button, "Comma Style", ToolbarIcon.Comma)
    {
        DisabledReason = SheetWords.CommaStyleUnread,
    };

    /// <inheritdoc />
    protected override string? DefaultKeyTip => "K";
}

/// <summary>Format Cells… (ADR-0100, ADR-0071): opens Format Cells, which takes the keyboard.</summary>
public sealed class FormatCellsItem : ToolbarItemBase
{
    /// <inheritdoc />
    protected override Task PressAsync() => Context.Sheet.OpenFormatCellsAsync();

    /// <inheritdoc />
    protected override bool ReturnsKeyboard => false;

    /// <inheritdoc />
    protected override ToolbarItemFace Face() => new(ToolbarItemKind.Button, "Format Cells", ToolbarIcon.FormatCells) { Shortcut = "Ctrl+1" };

    /// <inheritdoc />
    protected override string? DefaultKeyTip => "O";
}

/// <summary>A gap between groups of Toolbar Items. It is never a tab stop and does nothing.</summary>
public sealed class ToolbarSeparator : ToolbarItemBase
{
    /// <inheritdoc />
    protected override Task PressAsync() => Task.CompletedTask;

    /// <inheritdoc />
    protected override ToolbarItemFace Face() => new(ToolbarItemKind.Separator, "", ToolbarIcon.None);
}

/// <summary>
/// A Consumer's own action on the Sheet Toolbar (ADR-0100), a Toolbar Item like ExSheet's: it stands
/// in a row of its own or among the formatting items, the Chrome draws it, and while an edit is open
/// it cannot be pressed. It carries a KeyTip only when one is declared.
/// </summary>
public sealed class ToolbarButton : ToolbarItemBase
{
    /// <summary>What pressing it does.</summary>
    [Parameter] public EventCallback OnClick { get; set; }

    /// <summary>Its name: the text it shows, its accessible name and its tooltip.</summary>
    [Parameter, EditorRequired] public string Text { get; set; } = "";

    /// <summary>Content of the Consumer's own drawn in place of the text, such as an icon. The name still names it.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Why it cannot be pressed now; null when it can.</summary>
    [Parameter] public string? DisabledReason { get; set; }

    /// <summary>
    /// Whether a press from the toolbar's keys gives the keyboard back to the Sheet afterwards, as
    /// every item's does by default. Set it false for an action that opens something of its own
    /// that takes the keyboard.
    /// </summary>
    [Parameter] public bool GivesKeyboardBack { get; set; } = true;

    /// <inheritdoc />
    protected override bool ReturnsKeyboard => GivesKeyboardBack;

    /// <inheritdoc />
    protected override Task PressAsync() => OnClick.InvokeAsync();

    /// <inheritdoc />
    protected override ToolbarItemFace Face() => new(ToolbarItemKind.Button, Text, ToolbarIcon.None)
    {
        Text = ChildContent is null ? Text : null,
        Content = ChildContent,
        DisabledReason = DisabledReason,
    };
}
