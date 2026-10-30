using Microsoft.AspNetCore.Components;

namespace ExSheet;

// ExSheet's Toolbar Items (ADR-0100). Each declares itself to the Sheet's toolbar; what it does, what it shows and
// Excel's Home tab KeyTip it carries are the Script Core's (a reading until a Windows run reads them all).

/// <summary>Bold (ADR-0100): Ctrl+B's toggle, KeyTip 1. It widens no column, as Ctrl+B does not.</summary>
public sealed class BoldItem : ToolbarItemBase;

/// <summary>Italic (ADR-0100): Ctrl+I's toggle, KeyTip 2. It widens no column, as Ctrl+I does not.</summary>
public sealed class ItalicItem : ToolbarItemBase;

/// <summary>
/// A single underline (ADR-0100): Ctrl+U's toggle, KeyTip 3. It widens a column whose numbers it no longer fits, as
/// Ctrl+U does (ADR-0071; the seventeenth Windows run's h7).
/// </summary>
public sealed class UnderlineItem : ToolbarItemBase;

/// <summary>
/// Strikethrough (ADR-0100): Ctrl+5's toggle. It widens a column whose numbers it no longer fits, as Ctrl+5 does
/// (ADR-0071; the fourteenth Windows run's case 7). Excel's Home tab has no KeyTip for it; ExSheet's own is 4, a letter
/// Excel's Home tab does not use.
/// </summary>
public sealed class StrikethroughItem : ToolbarItemBase;

/// <summary>
/// The Font colour (ADR-0100): Excel's Font Color split control, KeyTip FC, red on its face until another is chosen.
/// The face sets the colour last chosen and the arrow opens the palette: Automatic, then the Office theme's colours and
/// the ten standard ones, as Format Cells offers them. Over numbers showing ####, its face and a colour from its list
/// widen a column the user has not sized, as Excel's Home tab Font Color button did (the readings of 2026-10-14, run
/// 18a, widening case 5).
/// </summary>
public sealed class FontColourItem : ToolbarItemBase;

/// <summary>
/// The Fill (ADR-0100): Excel's Fill Color split control, KeyTip H, yellow on its face until another is chosen, with
/// No Fill first in its palette. It widens no column, as Excel's Fill Color button did not over #### (the fourteenth
/// Windows run, case 7).
/// </summary>
public sealed class FillItem : ToolbarItemBase;

/// <summary>
/// Borders (ADR-0100): Excel's Borders split control, KeyTip B. The face sets the preset chosen last — Bottom Border
/// until another is chosen — and the arrow lists Excel's presets, each relative to every selected range, as Format
/// Cells' are.
/// </summary>
public sealed class BordersItem : ToolbarItemBase;

/// <summary>
/// Left alignment (ADR-0100), KeyTip AL, pressed while the Focus cell has it. Pressing it sets it, and pressing it
/// again where the Focus cell has it sets General, as Excel's alignment buttons do.
/// </summary>
public sealed class AlignLeftItem : ToolbarItemBase;

/// <summary>Centre alignment (ADR-0100), KeyTip AC, as <see cref="AlignLeftItem"/> is.</summary>
public sealed class AlignCenterItem : ToolbarItemBase;

/// <summary>Right alignment (ADR-0100), KeyTip AR, as <see cref="AlignLeftItem"/> is.</summary>
public sealed class AlignRightItem : ToolbarItemBase;

/// <summary>
/// The Number Format (ADR-0100): Excel's Number Format box, KeyTip N, showing the Focus cell's category and listing
/// Excel's categories in Excel's order. Each sets what Format Cells' Number tab sets when the category is chosen;
/// Accounting and Fraction are shown disabled with Format Cells' reasons, and More Number Formats… opens Format Cells.
/// </summary>
public sealed class NumberFormatItem : ToolbarItemBase;

/// <summary>The percent style (ADR-0100): Excel's Percent Style, KeyTip P, <c>0%</c>, as Ctrl+Shift+% sets it.</summary>
public sealed class PercentItem : ToolbarItemBase;

/// <summary>
/// The comma style (ADR-0100), KeyTip K. Excel's Comma Style sets the built-in Comma style, whose code pads each value
/// with <c>*</c> as Accounting does, which ExSheet does not read. So it is shown and disabled, with the reason, as
/// Format Cells shows Accounting, rather than set as something else under Excel's name.
/// </summary>
public sealed class CommaItem : ToolbarItemBase;

/// <summary>
/// Format Cells… (ADR-0100, ADR-0071): Ctrl+1, which opens Format Cells and gives it the keyboard. Excel's Home tab
/// reaches it through the Cells group's Format menu, Alt, H, O, E (the Windows readings of 2026-10-12, R2 (a)); ExSheet
/// has no such menu, so its KeyTip is both letters: O narrows the KeyTips to this one and E runs it.
/// </summary>
public sealed class FormatCellsItem : ToolbarItemBase;

/// <summary>A gap between groups of Toolbar Items. It is never a tab stop and does nothing.</summary>
public sealed class ToolbarSeparator : ToolbarItemBase;

/// <summary>
/// A Consumer's own action on the Sheet Toolbar (ADR-0100), a Toolbar Item like ExSheet's: it stands in a row of its
/// own or among the formatting items, the Chrome draws it, and while an edit is open it cannot be pressed. It carries a
/// KeyTip only when one is declared. A press gives the keyboard back to the Sheet before <see cref="OnClick"/> runs, so
/// what it opens takes the keyboard from there.
/// </summary>
public sealed class ToolbarButton : ToolbarItemBase
{
    /// <summary>What pressing it does: run in the press's place in the Sheet's Input Order (ADR-0240 section 6).</summary>
    [Parameter] public EventCallback OnClick { get; set; }

    /// <summary>Its name: the text it shows, its accessible name and its tooltip.</summary>
    [Parameter, EditorRequired] public string Text { get; set; } = "";

    /// <summary>Content of the Consumer's own drawn in place of the text, such as an icon. The name still names it.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Why it cannot be pressed now; null when it can.</summary>
    [Parameter] public string? DisabledReason { get; set; }
}
