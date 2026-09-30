using ExGrid.Chrome;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;

namespace ExPivot.Chrome;

/// <summary>
/// ExPivot's Chrome (ADR-0060): what draws the Field List, the report filter band, a placed
/// field's menu and the three panels. Each member returns the content to draw, or null for
/// ExPivot's built-in plain markup. It renders and calls back; the rules, the state and the
/// frames are ExPivot's, so swapping it changes no behaviour.
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

    /// <summary>The report filter band above the report: one entry per field in Filters.</summary>
    RenderFragment? ReportFilters(PivotReportFiltersContext context) => null;

    /// <summary>A placed field's menu, inside the frame ExPivot opens under it.</summary>
    RenderFragment? Menu(PivotMenuContext context) => null;

    /// <summary>Filter…: a field's Items to tick, inside ExPivot's frame.</summary>
    RenderFragment? ItemFilter(PivotItemFilterContext context) => null;

    /// <summary>Field Settings…: subtotals and order, inside ExPivot's frame.</summary>
    RenderFragment? FieldSettings(PivotFieldSettingsContext context) => null;

    /// <summary>Value Field Settings…: caption, Aggregation, Show Values As and number format,
    /// inside ExPivot's frame.</summary>
    RenderFragment? ValueFieldSettings(PivotValueFieldSettingsContext context) => null;
}

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
    Func<string, string> Word);

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
public sealed record PivotCommand(string Id, string Label, bool Enabled, Func<Task> Invoke);

/// <summary>A placed field's menu (ADR-0060).</summary>
/// <param name="Title">The menu's accessible name.</param>
/// <param name="Commands">Excel's commands, in its order.</param>
/// <param name="Close">Closes the menu without running anything.</param>
/// <param name="FocusRequest">Changes when the menu should take DOM focus: its first enabled
/// command.</param>
public sealed record PivotMenuContext(string Title, IReadOnlyList<PivotCommand> Commands, Action Close, int FocusRequest);

/// <summary>A labelled choice in a panel.</summary>
/// <param name="Value">The choice.</param>
/// <param name="Label">What it is called.</param>
public sealed record PivotChoice<T>(T Value, string Label);

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
    Func<string, string> Word);

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
