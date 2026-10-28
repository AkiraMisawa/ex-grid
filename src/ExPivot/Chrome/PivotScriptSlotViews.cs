namespace ExPivot.Chrome;

/// <summary>
/// The Chrome slots an <see cref="IPivotChrome"/> fills with Razor when the Script Core draws the pivot
/// (<c>ExPivot.Script.ExPivot</c>, ADR-0410): each named here is asked for through its view overload, and the
/// Script Core draws its own part for every slot not named. The report grid's slots are the grid Chrome's
/// (<see cref="IPivotChrome.GridChrome"/>), named by its own <c>ScriptSlots</c>.
/// </summary>
/// <remarks>
/// It stands beside the C# core's contexts until the C# core is removed (ADR-0350 section 7, ADR-0410
/// section 4's note of 2026-10-28): a Chrome answers both while both cores draw.
/// </remarks>
[Flags]
public enum PivotScriptSlots
{
    /// <summary>No slot: the Script Core draws every part.</summary>
    None = 0,

    /// <summary>A placed field's menu and the Layout menu, through <see cref="IPivotChrome.Menu(PivotMenuView)"/>.</summary>
    Menu = 1,

    /// <summary>The Pivot Toolbar, through <see cref="IPivotChrome.Toolbar(PivotToolbarView)"/>.</summary>
    Toolbar = 2,

    /// <summary>The Stale Report's notice, through <see cref="IPivotChrome.StaleReport(PivotStaleReportView)"/>.</summary>
    StaleReport = 4,
}

/// <summary>
/// The attributes a pivot slot's Razor writes on its controls when the Script Core draws the pivot (ADR-0410
/// section 2): the adapter's script wires each control it finds by them, in the browser, so nothing a key means
/// waits on the circuit. The contents carry no handler of their own for what an attribute says.
/// </summary>
public static class PivotScriptSlotAttributes
{
    /// <summary>On a menu's control for one command, the command's index in <see cref="PivotMenuView.Items"/>, once
    /// for every item. The script gives it the command's role, <c>menuitemradio</c> for a choice and marked while
    /// it is the current one, marks it disabled where its command is, runs its command on a press, and gives the
    /// first enabled one the keyboard as the menu opens.</summary>
    public const string MenuItem = "data-ex-pivot-menu-item";

    /// <summary>On a button: what a press on it does. The Pivot Toolbar's <c>layout</c> (Layout ▾),
    /// <c>refresh</c> and <c>field-list</c> (the Field List's toggle); the Stale Report's <c>retry</c>. The script
    /// runs it in the pivot's Input Order (ADR-0240).</summary>
    public const string Press = "data-ex-pivot-press";

    /// <summary>On a control the pivot gives the keyboard to, by its name: the Pivot Toolbar's <c>layout</c>,
    /// <c>refresh</c> and <c>field-list</c>; the Stale Report's <c>retry</c>. After a menu closes, the keyboard
    /// goes back to the button that opened it.</summary>
    public const string Control = "data-ex-pivot-control";

    /// <summary>On an element the pivot places a popup's frame in, by its name: the Pivot Toolbar's
    /// <c>layout</c>, inside an element positioned around Layout ▾, and <c>report-filters</c>, where the report
    /// filter band stands. Draw it empty: the pivot's frame stands in it alone, and a Razor child beside the frame
    /// would be moved around it by Blazor's diff.</summary>
    public const string Anchor = "data-ex-pivot-anchor";
}

/// <summary>One command of a pivot menu, as the Script Core hands it (ADR-0061).</summary>
/// <param name="Id">The command's id, which is also the id of its word, stable for an icon.</param>
/// <param name="Label">What it is called, in the pivot's words.</param>
/// <param name="Enabled">Whether it can be pressed: one that would change nothing is disabled.</param>
/// <param name="Checked">For a choice among others, the Layout menu's, whether it is the current one; null on a
/// plain command.</param>
/// <param name="Heading">The heading of the group of choices that starts at this command; null where it
/// continues the group above.</param>
public sealed record PivotMenuItemView(string Id, string Label, bool Enabled, bool? Checked, string? Heading);

/// <summary>
/// A pivot menu's contents when the Script Core draws the pivot (ADR-0410 section 3): a placed field's menu, or
/// the Layout menu, data only. The Razor draws one control per item, in order, each marked with
/// <see cref="PivotScriptSlotAttributes.MenuItem"/>, and a group's heading where one starts; it handles none of
/// their presses or focus. The frame, Escape and what closes the menu are the pivot's.
/// </summary>
/// <param name="Name">The menu's accessible name, which its frame carries.</param>
/// <param name="Items">The commands, in Excel's order.</param>
public sealed record PivotMenuView(string Name, IReadOnlyList<PivotMenuItemView> Items);

/// <summary>
/// The Pivot Toolbar when the Script Core draws the pivot (ADR-0061, ADR-0410): data only, drawn again at each
/// change. The Razor draws, on the left, an element marked <see cref="PivotScriptSlotAttributes.Anchor"/>
/// <c>report-filters</c> where the report filter band stands while <see cref="ReportFilters"/>, and on the right,
/// in this order, Layout ▾, Refresh where <see cref="RefreshLabel"/> is given, and the Field List's toggle, each
/// marked with <see cref="PivotScriptSlotAttributes.Press"/> and <see cref="PivotScriptSlotAttributes.Control"/>;
/// Layout ▾ stands in a positioned element holding the anchor <c>layout</c>, where the Layout menu opens.
/// </summary>
/// <param name="LayoutLabel">Layout ▾'s words.</param>
/// <param name="LayoutMenuOpen">Whether the Layout menu is open.</param>
/// <param name="RefreshLabel">Refresh's words, while the source can be refreshed (ADR-0066); null otherwise, and
/// then there is no button.</param>
/// <param name="FieldListLabel">The Field List's toggle's words.</param>
/// <param name="FieldListShown">Whether the Field List is shown.</param>
/// <param name="ReportFilters">Whether the report filter band stands.</param>
/// <param name="Refusal">What the report could not do with the last change, in words, said as an alert; null
/// otherwise.</param>
public sealed record PivotToolbarView(
    string LayoutLabel,
    bool LayoutMenuOpen,
    string? RefreshLabel,
    string FieldListLabel,
    bool FieldListShown,
    bool ReportFilters,
    string? Refusal);

/// <summary>
/// The Stale Report's notice when the Script Core draws the pivot (ADR-0067, ADR-0410): data only. The Razor draws
/// the sentence and Retry, marked <see cref="PivotScriptSlotAttributes.Press"/> and
/// <see cref="PivotScriptSlotAttributes.Control"/> <c>retry</c>, inside the pivot's polite live region; it adds no
/// live region of its own.
/// </summary>
/// <param name="Message">The whole sentence.</param>
/// <param name="AsOfText">When the data on screen arrived, as the sentence writes it.</param>
/// <param name="Reason">What happened, as the sentence ends it.</param>
/// <param name="RetryLabel">Retry's words.</param>
/// <param name="RetryEnabled">Whether Retry can be pressed: not while what it would ask is already out.</param>
public sealed record PivotStaleReportView(string Message, string AsOfText, string Reason, string RetryLabel, bool RetryEnabled);
