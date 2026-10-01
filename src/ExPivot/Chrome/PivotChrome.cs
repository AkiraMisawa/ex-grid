using ExGrid.Chrome;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;

namespace ExPivot.Chrome;

/// <summary>
/// ExPivot's Chrome (ADR-0060): what draws the Field List, the toolbar above the report with its
/// report filter band, a menu — a placed field's, and the Layout menu — the three panels, Show
/// Details' tabs and dialog, and the Stale Report's notice. Each member returns the content to
/// draw, or null for ExPivot's built-in plain markup. It renders and calls back; the rules, the
/// state and the frames are ExPivot's, so swapping it changes no behaviour.
/// </summary>
public interface IPivotChrome
{
    /// <summary>
    /// The report grid's Chrome, so that one Chrome dresses both (ADR-0060); a <c>Chrome</c>
    /// written on ExPivot beats it. Null for ExGrid's built-in. ExPivot asks once per Pivot
    /// Chrome and holds the answer, so a new instance per call is right here.
    /// </summary>
    /// <param name="commandLabel">ExPivot's word for a command of its Context Menu — the
    /// Consumer's through ExPivot's <c>Label</c>, then ExPivot's English — or null for an id that
    /// is not ExPivot's. A grid Chrome that words its menus itself never consults the grid's
    /// <c>CommandLabel</c>, so it asks this first.</param>
    IGridChrome? GridChrome(Func<string, string?> commandLabel) => null;

    /// <summary>The Field List's content: the fields with their checkboxes and search, and the
    /// four Areas with their entries, each entry's open menu or panel placed under it.</summary>
    RenderFragment? FieldList(PivotFieldListContext context) => null;

    /// <summary>The report filter band, on the toolbar's left: one entry per field in Filters.</summary>
    RenderFragment? ReportFilters(PivotReportFiltersContext context) => null;

    /// <summary>The toolbar above the report (ADR-0060): the report filter band on its left — the
    /// context hands it over, drawn by <see cref="ReportFilters"/> — and on its right, in this
    /// order, Layout ▾, Refresh when the source can be refreshed, and the Field List's toggle.</summary>
    RenderFragment? Toolbar(PivotToolbarContext context) => null;

    /// <summary>A menu, inside the frame ExPivot opens under what opened it: a placed field's, and
    /// the toolbar's Layout menu, whose commands come in groups with the current choice marked.</summary>
    RenderFragment? Menu(PivotMenuContext context) => null;

    /// <summary>Filter…: a field's Items to tick, inside ExPivot's frame.</summary>
    RenderFragment? ItemFilter(PivotItemFilterContext context) => null;

    /// <summary>Field Settings…: subtotals and order, inside ExPivot's frame.</summary>
    RenderFragment? FieldSettings(PivotFieldSettingsContext context) => null;

    /// <summary>Value Field Settings…: caption, Aggregation, Show Values As and number format,
    /// inside ExPivot's frame.</summary>
    RenderFragment? ValueFieldSettings(PivotValueFieldSettingsContext context) => null;

    /// <summary>The tabs at the report's foot (ADR-0058): the report's own, then one per Show
    /// Details, each closable. The selected tab's records are ExPivot's to place, over the report.</summary>
    RenderFragment? DetailsTabs(PivotDetailsTabsContext context) => null;

    /// <summary>The content of Show Details' dialog, inside ExPivot's frame — which is a dialog
    /// named by the cell, takes the keyboard and closes on Escape (ADR-0058).</summary>
    RenderFragment? DetailsDialog(PivotDetailsDialogContext context) => null;

    /// <summary>
    /// The Stale Report's notice (ADR-0066), under the toolbar, inside ExPivot's frame — a polite
    /// live region, so the notice is announced without interrupting the reader; the content adds
    /// no live region of its own. Asked only while the report is stale: the newest data could not
    /// be shown, the report stays on the last version it could compute, and the notice says what
    /// happened and as of when, and offers Retry. A refused layout is not a Stale Report: the
    /// toolbar says that one (<see cref="PivotToolbarContext.Refusal"/>).
    /// </summary>
    RenderFragment? StaleReport(PivotStaleReportContext context) => null;
}

/// <summary>
/// The Stale Report's notice (ADR-0066): the newest data cannot be shown — the new answer broke a
/// cap, or the source refused or failed — so the report stays on the last version it could
/// compute, and says what happened and as of when. Retry asks the source again; the notice goes
/// when an answer is laid out. Its words are ExPivot's, by id: <c>stale-report</c> frames the
/// sentence, <c>retry</c> names the command.
/// </summary>
/// <param name="Message">The whole sentence: "Showing the data as of 14:32:05: the newest data
/// needs more than 200,000 cells."</param>
/// <param name="Reason">What happened, as the sentence ends: "the newest data needs more than
/// 200,000 cells."</param>
/// <param name="AsOf">When the answer the report on screen was laid out from arrived, in the
/// clock's local time zone.</param>
/// <param name="AsOfText"><paramref name="AsOf"/> as the sentence writes it, in the report's
/// culture: the time, and the date too when it is not today's.</param>
/// <param name="Retry">Asks again what failed — the source's Refresh, when that is what failed,
/// otherwise the report's layout — and is not available while that is out.</param>
/// <param name="Word">ExPivot's words, by id.</param>
public sealed record PivotStaleReportContext(
    string Message,
    string Reason,
    DateTimeOffset AsOf,
    string AsOfText,
    PivotCommand Retry,
    Func<string, string> Word);

/// <summary>What is being dragged in the Field List (ADR-0060): a declared field from the list
/// of fields, or a placed entry of an Area.</summary>
public sealed record PivotDragSubject
{
    private PivotDragSubject(string? field, PivotEntry? entry)
    {
        Field = field;
        Entry = entry;
    }

    /// <summary>A declared field, dragged from the list of fields.</summary>
    public static PivotDragSubject FromList(string field)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        return new PivotDragSubject(field, null);
    }

    /// <summary>A placed entry, dragged from its Area.</summary>
    public static PivotDragSubject FromArea(PivotEntry entry) => new(null, entry);

    /// <summary>The field's name, when dragged from the list of fields.</summary>
    public string? Field { get; }

    /// <summary>The entry, when dragged from an Area.</summary>
    public PivotEntry? Entry { get; }
}

/// <summary>
/// Everything a Field List substitute needs (ADR-0060). The Chrome paints the fields and the
/// Areas and calls back; ExPivot decides what each gesture means. Drag and drop is Blazor's own
/// events: a draggable element calls <see cref="StartDrag"/> on <c>dragstart</c> and
/// <see cref="EndDrag"/> on <c>dragend</c>; a drop target prevents <c>dragover</c>'s default
/// always — whether it may be dropped on is decided at the drop, never by a render that may
/// still be on its way — calls <see cref="DragOver"/> on <c>dragenter</c> for the indicator, and
/// calls <see cref="Drop"/> (or <see cref="DropOnList"/>) on <c>drop</c>. A drop that changes
/// nothing is ExPivot's to ignore; <see cref="PivotAreaView.AcceptsDrop"/> and
/// <see cref="ListAcceptsDrop"/> are for the look.
/// </summary>
/// <param name="Title">The pane's name: "PivotTable Fields".</param>
/// <param name="Fields">The declared fields the search leaves, in declaration order.</param>
/// <param name="Search">The search as typed.</param>
/// <param name="SearchChanged">Called with the search as it is typed.</param>
/// <param name="Areas">The four Areas, in Excel's order: Filters, Columns, Rows, Values.</param>
/// <param name="Dragging">What is being dragged, or null.</param>
/// <param name="StartDrag">Called on a <c>dragstart</c>.</param>
/// <param name="DragOver">Called on a <c>dragenter</c>: the Area and the index a drop would take.</param>
/// <param name="Drop">Called on a <c>drop</c> on an Area, before the entry at the index (its
/// count for the end).</param>
/// <param name="ListAcceptsDrop">Whether the list of fields takes a drop now — an entry dragged
/// back to it is removed.</param>
/// <param name="DropOnList">Called on a <c>drop</c> on the list of fields.</param>
/// <param name="EndDrag">Called on a <c>dragend</c>, whether or not anything was dropped.</param>
/// <param name="Word">ExPivot's words, by id (<see cref="PivotWords"/>).</param>
public sealed record PivotFieldListContext(
    string Title,
    IReadOnlyList<PivotFieldEntry> Fields,
    string Search,
    Action<string?> SearchChanged,
    IReadOnlyList<PivotAreaView> Areas,
    PivotDragSubject? Dragging,
    Action<PivotDragSubject> StartDrag,
    Action<PivotArea, int> DragOver,
    Func<PivotArea, int, Task> Drop,
    bool ListAcceptsDrop,
    Func<Task> DropOnList,
    Action EndDrag,
    Func<string, string> Word)
{
    /// <summary>Whether Excel's Defer Layout Update is ticked, at the pane's foot (ADR-0060): the
    /// pane's changes then build a pending layout, which the fields and Areas above show, and the
    /// report and the source are left alone until Update.</summary>
    public bool DeferLayoutUpdate { get; init; }

    /// <summary>Ticks or unticks Defer Layout Update. Unticking applies the pending layout, as
    /// Excel does.</summary>
    public Func<bool, Task> DeferLayoutUpdateChanged { get; init; } = static _ => Task.CompletedTask;

    /// <summary>Whether Update would apply anything: Defer Layout Update is ticked and the pending
    /// layout is not the one the report is on.</summary>
    public bool CanUpdate { get; init; }

    /// <summary>Update: applies the pending layout in one change.</summary>
    public Func<Task> Update { get; init; } = static () => Task.CompletedTask;
}

/// <summary>A declared field in the Field List (ADR-0060).</summary>
/// <param name="Name">The field's name.</param>
/// <param name="Caption">What it is called.</param>
/// <param name="Type">Its declared type.</param>
/// <param name="IsPlaced">Whether it stands in any Area — its checkbox.</param>
/// <param name="IsFiltered">Whether it hides Items now.</param>
/// <param name="Toggle">Ticks or unticks it.</param>
public sealed record PivotFieldEntry(
    string Name, string Caption, PivotFieldType Type, bool IsPlaced, bool IsFiltered, Func<Task> Toggle);

/// <summary>One Area in the Field List (ADR-0060).</summary>
/// <param name="Area">Which Area.</param>
/// <param name="Title">Its name: Filters, Columns, Rows, Values.</param>
/// <param name="Entries">What stands in it, Σ Values last where it stands.</param>
/// <param name="AcceptsDrop">Whether what is being dragged may be dropped here.</param>
/// <param name="DropIndex">Where a drop would land, while something is dragged over the Area.</param>
public sealed record PivotAreaView(
    PivotArea Area, string Title, IReadOnlyList<PivotAreaEntryView> Entries, bool AcceptsDrop, int? DropIndex);

/// <summary>A placed entry of an Area (ADR-0060).</summary>
/// <param name="Entry">Which entry it is.</param>
/// <param name="Caption">What it is called: the field's caption, a Value Field's caption, Σ Values.</param>
/// <param name="IsFiltered">Whether its field hides Items now.</param>
/// <param name="IsMenuOpen">Whether its menu or one of its panels is open.</param>
/// <param name="OpenMenu">Opens its menu; closes it when it is open.</param>
/// <param name="Popup">The open menu or panel, in ExPivot's frame, which the Chrome places
/// directly under the entry, with no positioned element of its own around them: the frame takes
/// the pane's width from the Field List (ADR-0060). Null while none is open.</param>
/// <param name="FocusRequest">Changes when the entry's button should take DOM focus — after its
/// menu or panel closed.</param>
public sealed record PivotAreaEntryView(
    PivotEntry Entry,
    string Caption,
    bool IsFiltered,
    bool IsMenuOpen,
    Action OpenMenu,
    RenderFragment? Popup,
    int FocusRequest)
{
    /// <summary>Whether this is Σ Values.</summary>
    public bool IsValuesPseudoField => Entry.IsValuesPseudoField;
}

/// <summary>One command of a menu ExPivot decides (ADR-0060): what it is called — in ExPivot's
/// words, which the Consumer may replace — whether it is available, and what it does.</summary>
/// <param name="Id">Stable, for an icon (<see cref="PivotCommandIds"/>).</param>
/// <param name="Label">What it is called.</param>
/// <param name="Enabled">Whether it is available; a command that would change nothing is not.</param>
/// <param name="Invoke">Runs it and closes the menu.</param>
public sealed record PivotCommand(string Id, string Label, bool Enabled, Func<Task> Invoke)
{
    /// <summary>For a choice among others — the Layout menu's — whether it is the current one,
    /// which the menu marks (ADR-0060); null for a command that is not a choice.</summary>
    public bool? Checked { get; init; }

    /// <summary>The heading of the group of commands this one starts — the Layout menu's
    /// Subtotals, Grand Totals and Report Layout — or null when it continues the group above.</summary>
    public string? GroupHeading { get; init; }
}

/// <summary>A menu (ADR-0060): a placed field's, or the toolbar's Layout menu.</summary>
/// <param name="Title">The menu's accessible name.</param>
/// <param name="Commands">Excel's commands, in its order.</param>
/// <param name="Close">Closes the menu without running anything.</param>
/// <param name="FocusRequest">Changes when the menu should take DOM focus: its first enabled
/// command.</param>
public sealed record PivotMenuContext(string Title, IReadOnlyList<PivotCommand> Commands, Action Close, int FocusRequest);

/// <summary>A labelled choice in a panel.</summary>
/// <param name="Value">The choice.</param>
/// <param name="Label">What it is called.</param>
public sealed record PivotChoice<T>(T Value, string Label)
{
    /// <summary>Whether it may be chosen. An Aggregation the Pivot Source does not answer is
    /// offered, and not available (ADR-0065).</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Why it may not be chosen, in words — "The source does not answer Product." — or
    /// null while it may.</summary>
    public string? Reason { get; init; }
}

/// <summary>One Item as Filter… lists it.</summary>
/// <param name="Key">The Item.</param>
/// <param name="Label">Its label.</param>
/// <param name="Ticked">Whether it is shown in the draft.</param>
public sealed record PivotItemChoice(PivotItemKey Key, string Label, bool Ticked);

/// <summary>
/// Filter… (ADR-0060): a field's Items, every one, in its order, each ticked while shown. The
/// ticks are a draft ExPivot holds; OK applies them, Cancel and Escape drop them.
/// </summary>
/// <param name="FieldCaption">The field's caption.</param>
/// <param name="Items">The Items the search leaves, at most <see cref="ListCap"/> of them.</param>
/// <param name="ItemCount">How many Items the field has.</param>
/// <param name="MatchCount">How many the search leaves — more than <see cref="Items"/> when capped.</param>
/// <param name="ListCap">The most Items listed at once.</param>
/// <param name="Search">The search as typed.</param>
/// <param name="SearchChanged">Called with the search as it is typed.</param>
/// <param name="AllTicked">(Select All)'s state: true, false, or null for some.</param>
/// <param name="SetAll">Ticks or unticks every Item the search leaves.</param>
/// <param name="SetTicked">Ticks or unticks one Item.</param>
/// <param name="CanApply">Whether OK may be pressed: not while every Item is unticked.</param>
/// <param name="Refusal">Why OK cannot apply, in words, or null.</param>
/// <param name="Apply">OK.</param>
/// <param name="Cancel">Cancel.</param>
/// <param name="FocusRequest">Changes when the panel should take DOM focus: its search.</param>
/// <param name="Word">ExPivot's words, by id.</param>
public sealed record PivotItemFilterContext(
    string FieldCaption,
    IReadOnlyList<PivotItemChoice> Items,
    int ItemCount,
    int MatchCount,
    int ListCap,
    string Search,
    Action<string?> SearchChanged,
    bool? AllTicked,
    Action<bool> SetAll,
    Action<PivotItemKey, bool> SetTicked,
    bool CanApply,
    string? Refusal,
    Func<Task> Apply,
    Action Cancel,
    int FocusRequest,
    Func<string, string> Word)
{
    /// <summary>Whether the field's Items are still on their way from the Pivot Source, which
    /// lists them under the report's Source Version (ADR-0065): a first listing, with nothing yet
    /// to list. OK cannot be pressed meanwhile.</summary>
    public bool IsLoading { get; init; }

    /// <summary>Whether the Items listed are an earlier Source Version's, kept in view while the
    /// report's version's are on their way (ADR-0065 refined). They are listed, ticked and applied
    /// as any are — Hidden Items are keys, which name the same Items under any version — and the
    /// report's version's replace them when they land. A Chrome may mark the list as updating; it
    /// does not blank it, and does not dim it: a live report brings a new version several times a
    /// second.</summary>
    public bool IsUpdating { get; init; }

    /// <summary>Why the Items cannot be listed, in words, or null: "The data has changed —
    /// refresh." when the source can no longer answer under the report's Source Version, rather
    /// than Items that are not the report's (ADR-0065). OK cannot be pressed meanwhile.</summary>
    public string? Unavailable { get; init; }
}

/// <summary>Field Settings… of a row or column field (ADR-0060): its subtotals and its order,
/// as a draft until OK.</summary>
/// <param name="FieldCaption">The field's caption.</param>
/// <param name="Subtotals">Automatic (true) or None, in the draft.</param>
/// <param name="SubtotalsChanged">Sets the draft's subtotals.</param>
/// <param name="Sort">The draft's order, by index in <see cref="Sorts"/>.</param>
/// <param name="SortChanged">Sets the draft's order.</param>
/// <param name="Sorts">The orders there are: by label either way, and by each Value Field either way.</param>
/// <param name="Apply">OK.</param>
/// <param name="Cancel">Cancel.</param>
/// <param name="FocusRequest">Changes when the panel should take DOM focus: its first control.</param>
/// <param name="InnerPopupChanged">Reports a popup of the content's own opening and closing —
/// while one is open, Escape is the popup's (ADR-0039).</param>
/// <param name="Word">ExPivot's words, by id.</param>
public sealed record PivotFieldSettingsContext(
    string FieldCaption,
    bool Subtotals,
    Action<bool> SubtotalsChanged,
    int Sort,
    Action<int> SortChanged,
    IReadOnlyList<PivotChoice<PivotSort>> Sorts,
    Func<Task> Apply,
    Action Cancel,
    int FocusRequest,
    Action<bool> InnerPopupChanged,
    Func<string, string> Word);

/// <summary>Value Field Settings… (ADR-0060): the caption, the Aggregation, Show Values As and
/// the number format, as a draft until OK, which a taken caption or an unusable format refuses.</summary>
/// <param name="FieldCaption">The Pivot Field's caption — the Source Name.</param>
/// <param name="Caption">The draft's caption, as shown in its field.</param>
/// <param name="CaptionChanged">Called with the caption as it is typed.</param>
/// <param name="Aggregation">The draft's Aggregation.</param>
/// <param name="AggregationChanged">Sets it.</param>
/// <param name="Aggregations">Every Aggregation, with its name.</param>
/// <param name="ShowValuesAs">The draft's Show Values As.</param>
/// <param name="ShowValuesAsChanged">Sets it.</param>
/// <param name="ShowValuesAsChoices">Every Show Values As, with its name.</param>
/// <param name="NumberFormat">The draft's number format, as typed; empty for General.</param>
/// <param name="NumberFormatChanged">Called with the format as it is typed.</param>
/// <param name="Sample">A sample number shown in the draft's format, or the reason it cannot be.</param>
/// <param name="Refusal">Why OK last refused, in words, or null.</param>
/// <param name="Apply">OK.</param>
/// <param name="Cancel">Cancel.</param>
/// <param name="FocusRequest">Changes when the panel should take DOM focus: its first control.</param>
/// <param name="InnerPopupChanged">Reports a popup of the content's own opening and closing.</param>
/// <param name="Word">ExPivot's words, by id.</param>
public sealed record PivotValueFieldSettingsContext(
    string FieldCaption,
    string Caption,
    Action<string?> CaptionChanged,
    PivotAggregation Aggregation,
    Action<PivotAggregation> AggregationChanged,
    IReadOnlyList<PivotChoice<PivotAggregation>> Aggregations,
    PivotShowValuesAs ShowValuesAs,
    Action<PivotShowValuesAs> ShowValuesAsChanged,
    IReadOnlyList<PivotChoice<PivotShowValuesAs>> ShowValuesAsChoices,
    string NumberFormat,
    Action<string?> NumberFormatChanged,
    string Sample,
    string? Refusal,
    Func<Task> Apply,
    Action Cancel,
    int FocusRequest,
    Action<bool> InnerPopupChanged,
    Func<string, string> Word);

/// <summary>The report filter band (ADR-0060): each field in Filters, as Excel shows its page
/// fields above the report.</summary>
/// <param name="Title">The band's accessible name.</param>
/// <param name="Filters">One entry per field in Filters, in order.</param>
/// <param name="Word">ExPivot's words, by id.</param>
public sealed record PivotReportFiltersContext(string Title, IReadOnlyList<PivotReportFilterView> Filters, Func<string, string> Word);

/// <summary>One report filter (ADR-0060).</summary>
/// <param name="Field">The field's name.</param>
/// <param name="Caption">Its caption.</param>
/// <param name="Summary">What it shows: <c>(All)</c>, the one Item shown, or <c>(Multiple Items)</c>.</param>
/// <param name="IsFiltered">Whether it hides Items.</param>
/// <param name="IsOpen">Whether its Filter… is open.</param>
/// <param name="Open">Opens its Filter…; closes it when it is open.</param>
/// <param name="Popup">The open Filter…, in ExPivot's frame, which the Chrome places inside a
/// positioned wrapper around the button; null while closed.</param>
/// <param name="FocusRequest">Changes when the button should take DOM focus — after Filter… closed.</param>
public sealed record PivotReportFilterView(
    string Field,
    string Caption,
    string Summary,
    bool IsFiltered,
    bool IsOpen,
    Action Open,
    RenderFragment? Popup,
    int FocusRequest);

/// <summary>
/// The toolbar above the report (ADR-0060). On its left stands the report filter band, which it is
/// handed drawn; on its right, in this order, Layout ▾, Refresh when the source can be refreshed,
/// and the Field List's toggle. Its popups — the band's Filter… and the Layout menu — open under it,
/// over the report, in ExPivot's frame, with a backdrop that closes them; the Chrome places each
/// frame inside a positioned wrapper around the button that opened it.
/// </summary>
/// <param name="ReportFilters">The report filter band, drawn by <see cref="IPivotChrome.ReportFilters"/>
/// or ExPivot's markup; null while no field stands in Filters.</param>
/// <param name="LayoutMenu">Layout ▾, which opens Excel's Design tab choices.</param>
/// <param name="Refresh">Refresh: asks the source again. Null when the source cannot be
/// refreshed, and then there is no button (ADR-0065).</param>
/// <param name="FieldList">The Field List's toggle: <see cref="PivotCommand.Checked"/> says
/// whether the pane is shown, and invoking it shows or hides it.</param>
/// <param name="Refusal">What the report could not do with the last change, in words — a layout
/// refused by name ("This layout needs more than 200,000 cells."), or the source's failure — or
/// null. The report stays as it was, and this says why where the user sees it, as an alert: it
/// answers what the user just did. Data that could not be shown is not said here but in the Stale
/// Report's notice under the toolbar (<see cref="IPivotChrome.StaleReport"/>).</param>
/// <param name="Word">ExPivot's words, by id.</param>
public sealed record PivotToolbarContext(
    RenderFragment? ReportFilters,
    PivotToolbarMenuView LayoutMenu,
    PivotCommand? Refresh,
    PivotCommand FieldList,
    string? Refusal,
    Func<string, string> Word);

/// <summary>A toolbar button that opens a menu under the toolbar (ADR-0060): Layout ▾.</summary>
/// <param name="Id">Stable, for an icon (<see cref="PivotCommandIds.LayoutMenu"/>).</param>
/// <param name="Label">What it is called: "Layout".</param>
/// <param name="IsOpen">Whether its menu is open.</param>
/// <param name="Open">Opens its menu; closes it when it is open.</param>
/// <param name="Popup">The open menu, in ExPivot's frame, which the Chrome places inside a
/// positioned wrapper around the button; null while closed.</param>
/// <param name="FocusRequest">Changes when the button should take DOM focus — after its menu
/// closed.</param>
public sealed record PivotToolbarMenuView(string Id, string Label, bool IsOpen, Action Open, RenderFragment? Popup, int FocusRequest);

/// <summary>
/// The tabs at the report's foot, where Excel's sheet tabs are (ADR-0058): the report's own tab,
/// then one per Show Details, each titled by its cell and closable. Selecting a details tab shows
/// its records over the report, and the report's tab brings the report back. The tabs are not part
/// of the Pivot Layout.
/// </summary>
/// <param name="Title">The tab list's accessible name.</param>
/// <param name="Report">The report's own tab, which cannot be closed.</param>
/// <param name="Tabs">One tab per Show Details, oldest first.</param>
/// <param name="Word">ExPivot's words, by id.</param>
public sealed record PivotDetailsTabsContext(
    string Title, PivotDetailsTab Report, IReadOnlyList<PivotDetailsTab> Tabs, Func<string, string> Word);

/// <summary>One tab at the report's foot.</summary>
/// <param name="Id">Unique among the tabs of this ExPivot, and stable while the tab stands. The
/// panel ExPivot places for it has the id <c>{Id}-panel</c> and is labelled by the element with this
/// id (<c>aria-labelledby</c>): the Chrome gives it to the tab itself, or — where a design system's
/// tab carries an id of its own — to the tab's title inside it.</param>
/// <param name="Title">What it is called: the report's tab "PivotTable", a details tab its cell.</param>
/// <param name="IsSelected">Whether it is the one shown.</param>
/// <param name="Select">Shows it.</param>
/// <param name="Close">Closes it; null for the report's tab.</param>
/// <param name="CloseLabel">The close button's accessible name, "Close Details: …"; null for the
/// report's tab.</param>
public sealed record PivotDetailsTab(string Id, string Title, bool IsSelected, Action Select, Action? Close, string? CloseLabel)
{
    /// <summary>Changes when the tab's button should take DOM focus: a tab Show Details has just
    /// opened — the report under it is covered, and keeps no keyboard — and the tab selected when
    /// the one holding the keyboard closed. Zero asks nothing.</summary>
    public int FocusRequest { get; init; }
}

/// <summary>
/// Show Details' dialog content (ADR-0058), inside ExPivot's frame: the cell's title, the records'
/// grid — ExPivot's, an ExGrid of the source's fields paged under the report's Source Version — and
/// a Close button.
/// </summary>
/// <param name="Title">The cell's title, which also names the dialog.</param>
/// <param name="Records">The records' grid, or the sentence that says why they cannot be shown.</param>
/// <param name="Close">Closes the dialog.</param>
/// <param name="FocusRequest">Changes when the content should take DOM focus: its Close button.</param>
/// <param name="Word">ExPivot's words, by id.</param>
public sealed record PivotDetailsDialogContext(
    string Title, RenderFragment Records, Action Close, int FocusRequest, Func<string, string> Word);
