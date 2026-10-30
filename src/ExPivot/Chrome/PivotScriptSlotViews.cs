using ExPivot.Engine;

namespace ExPivot.Chrome;

/// <summary>
/// The Chrome slots an <see cref="IPivotChrome"/> fills with Razor when the Script Core draws the pivot
/// (<c>ExPivot.Components.ExPivot</c>, ADR-0410): each named here is asked for through its view overload, and the
/// Script Core draws its own part for every slot not named. The report grid's slots are the grid Chrome's
/// (<see cref="IPivotChrome.GridChrome"/>), named by its own <c>ScriptSlots</c>.
/// </summary>
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

    /// <summary>The report filter band, through <see cref="IPivotChrome.ReportFilters(PivotReportFiltersView)"/>.</summary>
    ReportFilters = 8,

    /// <summary>Filter…, through <see cref="IPivotChrome.ItemFilter(PivotItemFilterView)"/>.</summary>
    ItemFilter = 16,

    /// <summary>Field Settings…, through <see cref="IPivotChrome.FieldSettings(PivotFieldSettingsView)"/>.</summary>
    FieldSettings = 32,

    /// <summary>Value Field Settings…, through
    /// <see cref="IPivotChrome.ValueFieldSettings(PivotValueFieldSettingsView)"/>.</summary>
    ValueFieldSettings = 64,

    /// <summary>The PivotTable Fields pane, through <see cref="IPivotChrome.FieldList(PivotFieldListView)"/>.</summary>
    FieldList = 128,

    /// <summary>Show Details' sheet tabs, through <see cref="IPivotChrome.DetailsTabs(PivotDetailsTabsView)"/>.</summary>
    DetailsTabs = 256,

    /// <summary>Show Details' dialog, through <see cref="IPivotChrome.DetailsDialog(PivotDetailsDialogView)"/>.</summary>
    DetailsDialog = 512,
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
    /// <c>refresh</c> and <c>field-list</c> (the Field List's toggle); the Stale Report's <c>retry</c>; a panel's
    /// <c>ok</c> and <c>cancel</c>; on the report filter band, the field whose Filter… it opens. The script
    /// runs it in the pivot's Input Order (ADR-0240).</summary>
    public const string Press = "data-ex-pivot-press";

    /// <summary>On a control the pivot gives the keyboard to, by its name: the Pivot Toolbar's <c>layout</c>,
    /// <c>refresh</c> and <c>field-list</c>; the Stale Report's <c>retry</c>; a report filter's button, by its
    /// field. After a menu closes, the keyboard
    /// goes back to the button that opened it.</summary>
    public const string Control = "data-ex-pivot-control";

    /// <summary>On an element the pivot places a popup's frame in, by its name: the Pivot Toolbar's
    /// <c>layout</c>, inside an element positioned around Layout ▾; <c>report-filters</c>, where the report
    /// filter band stands; and a report filter's field, inside an element positioned around its button. Draw it empty: the pivot's frame stands in it alone, and a Razor child beside the frame
    /// would be moved around it by Blazor's diff.</summary>
    public const string Anchor = "data-ex-pivot-anchor";

    /// <summary>On a panel's text field, or an element holding it: Filter…'s <c>search</c>, and Value Field
    /// Settings…' <c>caption</c> and <c>number-format</c>. The script reads its text as it is typed, and writes the
    /// draft into it where the two differ, so draw it with the text its view opens with and no value bound after: a
    /// render arrives a round trip behind the typing.</summary>
    public const string Text = "data-ex-pivot-text";

    /// <summary>On a panel's choice, or an element holding it: Field Settings…' <c>subtotals</c> on each radio
    /// button, beside <see cref="Value"/>, and <c>sort</c>; Value Field Settings…' <c>aggregation</c> and
    /// <c>show-values-as</c>. A select is a native <c>select</c> whose options' values are the view's (an order's
    /// index, a choice's <see cref="PivotChoiceView.Value"/>): a component that answers its choice in .NET would
    /// reach the pivot a round trip after the OK that applies it.</summary>
    public const string Choice = "data-ex-pivot-choice";

    /// <summary>Beside <see cref="Choice"/> on a radio button: the value it chooses, <c>automatic</c> or
    /// <c>none</c>.</summary>
    public const string Value = "data-ex-pivot-value";

    /// <summary>On Filter…'s checkbox for one Item: the Item's index in <see cref="PivotItemFilterView.Items"/>. The
    /// script ticks the Item the checkbox was drawn for.</summary>
    public const string Item = "data-ex-pivot-item";

    /// <summary>On Filter…'s (Select All): a press ticks every Item listed, or unticks them while all are.</summary>
    public const string All = "data-ex-pivot-all";

    /// <summary>On a panel's first control, or an element holding it: the one each of the pivot's requests for the
    /// keyboard is for.</summary>
    public const string First = "data-ex-pivot-first";

    /// <summary>On a field of the Field List, the element the browser drags: the field's
    /// <see cref="PivotFieldListFieldView.Name"/>. Its checkbox inside ticks the field into the layout, or takes it
    /// out. Draw it <c>draggable</c>.</summary>
    public const string Field = "data-ex-pivot-field";

    /// <summary>On an entry of an Area, the element the browser drags and drops on: the entry's
    /// <see cref="PivotFieldListEntryView.Key"/>. The script keeps it draggable while no menu or panel of its stands
    /// under it.</summary>
    public const string Entry = "data-ex-pivot-entry";

    /// <summary>On an Area's box, which takes a drop past its entries: the Area's
    /// <see cref="PivotFieldListAreaView.Name"/>.</summary>
    public const string Area = "data-ex-pivot-area";

    /// <summary>On the Field List's list of fields: an entry dropped there is taken out of the layout.</summary>
    public const string Fields = "data-ex-pivot-fields";

    /// <summary>On Defer Layout Update's checkbox, or an element holding it.</summary>
    public const string Defer = "data-ex-pivot-defer";

    /// <summary>On a sheet tab: its <see cref="PivotDetailsTabView.Key"/>; a press selects its sheet.</summary>
    public const string Tab = "data-ex-pivot-tab";

    /// <summary>On a sheet tab's close button, beside the tab and not inside it: the tab's
    /// <see cref="PivotDetailsTabView.Key"/>; a press closes its sheet.</summary>
    public const string CloseTab = "data-ex-pivot-close-tab";
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

/// <summary>One field of the report filter band (ADR-0061).</summary>
/// <param name="Field">The field's name, which its button and its anchor are marked with.</param>
/// <param name="Caption">Its caption.</param>
/// <param name="Summary">What it shows: (All), the one Item shown, or (Multiple Items).</param>
/// <param name="Filtered">Whether it hides Items.</param>
/// <param name="Open">Whether its Filter… is open.</param>
/// <param name="ButtonLabel">The button's accessible name: "Filter Region".</param>
public sealed record PivotReportFiltersEntryView(string Field, string Caption, string Summary, bool Filtered, bool Open, string ButtonLabel);

/// <summary>
/// The report filter band when the Script Core draws the pivot (ADR-0061, ADR-0410): data only. The Razor draws, per
/// field, its caption and a button marked <see cref="PivotScriptSlotAttributes.Press"/> and
/// <see cref="PivotScriptSlotAttributes.Control"/> with the field, beside an empty element marked
/// <see cref="PivotScriptSlotAttributes.Anchor"/> with the field, both in a positioned element, where Filter… opens.
/// </summary>
/// <param name="Name">The band's accessible name, which a <c>role="group"</c> around it carries.</param>
/// <param name="Filters">One per field in Filters, in their order.</param>
public sealed record PivotReportFiltersView(string Name, IReadOnlyList<PivotReportFiltersEntryView> Filters);

/// <summary>A choice a panel offers (ADR-0061).</summary>
/// <param name="Value">The choice, as the option's value carries it.</param>
/// <param name="Label">What it is called.</param>
/// <param name="Enabled">Whether it may be chosen: an Aggregation the source does not answer is offered, and not
/// available (ADR-0066).</param>
/// <param name="Reason">Why it may not be chosen, in words; null while it may.</param>
public sealed record PivotChoiceView(string Value, string Label, bool Enabled, string? Reason);

/// <summary>
/// Field Settings… when the Script Core draws the pivot (ADR-0061, ADR-0410): the draft as data, drawn again at each
/// change. The Razor draws the subtotals' two radio buttons, each marked <see cref="PivotScriptSlotAttributes.Choice"/>
/// <c>subtotals</c> and <see cref="PivotScriptSlotAttributes.Value"/> <c>automatic</c> or <c>none</c>, the first also
/// <see cref="PivotScriptSlotAttributes.First"/>; the order as a native select marked <c>sort</c>, its options'
/// values the orders' indexes; and OK and Cancel, marked <see cref="PivotScriptSlotAttributes.Press"/>.
/// </summary>
/// <param name="Caption">The field's caption, the panel's title.</param>
/// <param name="Subtotals">The draft's subtotals: Automatic (<c>true</c>) or None.</param>
/// <param name="Sorts">The orders there are, each by its words.</param>
/// <param name="Sort">The draft's order, by its index in <paramref name="Sorts"/>.</param>
/// <param name="SubtotalsLabel">The subtotals' group's words.</param>
/// <param name="AutomaticLabel">Automatic's words.</param>
/// <param name="NoneLabel">None's words.</param>
/// <param name="SortOrderLabel">The order's words.</param>
/// <param name="OkLabel">OK's words.</param>
/// <param name="CancelLabel">Cancel's words.</param>
public sealed record PivotFieldSettingsView(
    string Caption,
    bool Subtotals,
    IReadOnlyList<string> Sorts,
    int Sort,
    string SubtotalsLabel,
    string AutomaticLabel,
    string NoneLabel,
    string SortOrderLabel,
    string OkLabel,
    string CancelLabel);

/// <summary>
/// Value Field Settings… when the Script Core draws the pivot (ADR-0061, ADR-0410): the draft as data, drawn again at
/// each change. The Razor draws the caption's text field, marked <see cref="PivotScriptSlotAttributes.Text"/>
/// <c>caption</c> and <see cref="PivotScriptSlotAttributes.First"/>; the Aggregation and Show Values As as native
/// selects marked <see cref="PivotScriptSlotAttributes.Choice"/>; the number format's text field marked
/// <c>number-format</c>; the sample and the refusal; and OK and Cancel, marked
/// <see cref="PivotScriptSlotAttributes.Press"/>.
/// </summary>
/// <param name="Title">The panel's title.</param>
/// <param name="SourceCaption">The Pivot Field's caption, the Source Name.</param>
/// <param name="Caption">The caption its field opens with; the script writes the draft's after.</param>
/// <param name="Aggregation">The draft's Aggregation, as its choice's value.</param>
/// <param name="Aggregations">Every Aggregation, with whether the source answers it.</param>
/// <param name="ShowValuesAs">The draft's Show Values As, as its choice's value.</param>
/// <param name="ShowValuesAsChoices">Every Show Values As.</param>
/// <param name="NumberFormat">The number format its field opens with; the script writes the draft's after.</param>
/// <param name="NumberFormatPlaceholder">What an empty number format means: General.</param>
/// <param name="Sample">A number shown in the draft's format, or the reason it cannot be.</param>
/// <param name="Refusal">Why OK last refused, in words; null otherwise.</param>
/// <param name="CustomNameLabel">The caption's words.</param>
/// <param name="SummarizeByLabel">The Aggregation's words.</param>
/// <param name="ShowValuesAsLabel">Show Values As' words.</param>
/// <param name="NumberFormatLabel">The number format's words.</param>
/// <param name="OkLabel">OK's words.</param>
/// <param name="CancelLabel">Cancel's words.</param>
public sealed record PivotValueFieldSettingsView(
    string Title,
    string SourceCaption,
    string Caption,
    string Aggregation,
    IReadOnlyList<PivotChoiceView> Aggregations,
    string ShowValuesAs,
    IReadOnlyList<PivotChoiceView> ShowValuesAsChoices,
    string NumberFormat,
    string NumberFormatPlaceholder,
    string Sample,
    string? Refusal,
    string CustomNameLabel,
    string SummarizeByLabel,
    string ShowValuesAsLabel,
    string NumberFormatLabel,
    string OkLabel,
    string CancelLabel);

/// <summary>One Item as Filter… lists it (ADR-0061).</summary>
/// <param name="Key">A text naming the Item, the same while the panel stands wherever the Item comes in the list: a
/// Chrome keys the Item's checkbox by it, so a box keeps its Item as the list moves.</param>
/// <param name="Label">Its label.</param>
/// <param name="Ticked">Whether it is shown in the draft.</param>
public sealed record PivotItemChoiceView(string Key, string Label, bool Ticked);

/// <summary>
/// Filter… when the Script Core draws the pivot (ADR-0061, ADR-0066, ADR-0410): the draft as data, drawn again at
/// each change. The Razor draws the search, marked <see cref="PivotScriptSlotAttributes.Text"/> <c>search</c> and
/// <see cref="PivotScriptSlotAttributes.First"/>; (Select All), marked <see cref="PivotScriptSlotAttributes.All"/>,
/// and a checkbox per Item marked <see cref="PivotScriptSlotAttributes.Item"/> with its index; the note, the
/// refusal; and OK and Cancel, marked <see cref="PivotScriptSlotAttributes.Press"/>.
/// </summary>
/// <param name="Caption">The field's caption, which names the list.</param>
/// <param name="Search">The search its field opens with; the script writes the draft's after.</param>
/// <param name="Items">The Items the search leaves, in the field's order.</param>
/// <param name="AllTicked">(Select All)'s state: <c>true</c> for every Item, <c>false</c> for none, null for some.</param>
/// <param name="Loading">Whether the Items are on their way, with nothing yet to list.</param>
/// <param name="Updating">Whether newer Items are on their way while those shown stand: mark the list busy, and
/// neither blank nor dim it (ADR-0066).</param>
/// <param name="Unavailable">Why the Items cannot be listed, said in place of the list; null while they can.</param>
/// <param name="Note">A note under the Items; null otherwise.</param>
/// <param name="CanApply">Whether OK may be pressed.</param>
/// <param name="Refusal">Why OK cannot apply, in words; null while it can.</param>
/// <param name="SearchLabel">The search's words.</param>
/// <param name="SelectAllLabel">(Select All)'s words.</param>
/// <param name="LoadingLabel">What is said while the Items are on their way.</param>
/// <param name="OkLabel">OK's words.</param>
/// <param name="CancelLabel">Cancel's words.</param>
public sealed record PivotItemFilterView(
    string Caption,
    string Search,
    IReadOnlyList<PivotItemChoiceView> Items,
    bool? AllTicked,
    bool Loading,
    bool Updating,
    string? Unavailable,
    string? Note,
    bool CanApply,
    string? Refusal,
    string SearchLabel,
    string SelectAllLabel,
    string LoadingLabel,
    string OkLabel,
    string CancelLabel);

/// <summary>A declared field as the Field List lists it (ADR-0061).</summary>
/// <param name="Name">The field's name, which marks the element dragged
/// (<see cref="PivotScriptSlotAttributes.Field"/>).</param>
/// <param name="Key">The name its checkbox is handed in by (<see cref="PivotScriptSlotAttributes.Control"/>).</param>
/// <param name="Caption">What it is called.</param>
/// <param name="Placed">Whether it stands in an Area: its checkbox is ticked.</param>
/// <param name="Filtered">Whether it hides Items now.</param>
/// <param name="Count">Its count of Items, written in the pivot's culture, where the source counted them; null
/// otherwise.</param>
/// <param name="CountLabel">The count as said, "12 Items"; null with the count.</param>
/// <param name="Dragging">Whether it is the one being dragged.</param>
public sealed record PivotFieldListFieldView(string Name, string Key, string Caption, bool Placed, bool Filtered, string? Count, string? CountLabel, bool Dragging);

/// <summary>A placed entry of an Area (ADR-0061).</summary>
/// <param name="Key">The name its element (<see cref="PivotScriptSlotAttributes.Entry"/>), its button
/// (<see cref="PivotScriptSlotAttributes.Press"/>, <see cref="PivotScriptSlotAttributes.Control"/>) and the anchor
/// under it (<see cref="PivotScriptSlotAttributes.Anchor"/>) are marked with.</param>
/// <param name="Caption">What it is called: the field's caption, a Value Field's, Σ Values.</param>
/// <param name="MenuLabel">The button's accessible name: "Options for Region".</param>
/// <param name="Filtered">Whether its field hides Items now.</param>
/// <param name="Values">Whether it is Σ Values.</param>
/// <param name="MenuOpen">Whether its menu, or a panel the menu opened, stands under it.</param>
/// <param name="Dragging">Whether it is the one being dragged.</param>
/// <param name="DropBefore">Whether a drop now would land before it.</param>
public sealed record PivotFieldListEntryView(string Key, string Caption, string MenuLabel, bool Filtered, bool Values, bool MenuOpen, bool Dragging, bool DropBefore);

/// <summary>One Area of the Field List (ADR-0061).</summary>
/// <param name="Area">Which Area.</param>
/// <param name="Name">The Area's name as the script reads it, which marks its box
/// (<see cref="PivotScriptSlotAttributes.Area"/>).</param>
/// <param name="Title">Its words: Filters, Columns, Rows, Values.</param>
/// <param name="Entries">What stands in it, Σ Values last where it stands.</param>
/// <param name="DropTarget">Whether what is dragged is over it, and may be dropped there.</param>
/// <param name="OverCap">While a drop there would pass the leaf cap, the words that say so, as a status: a hint,
/// never a refusal (ADR-0370 section 3); null otherwise.</param>
public sealed record PivotFieldListAreaView(PivotArea Area, string Name, string Title, IReadOnlyList<PivotFieldListEntryView> Entries, bool DropTarget, string? OverCap);

/// <summary>
/// The PivotTable Fields pane when the Script Core draws the pivot (ADR-0061, ADR-0410): data only, drawn again at
/// each change. The Razor draws the heading with Hide (<see cref="PivotScriptSlotAttributes.Press"/> <c>close</c>);
/// the search, marked <see cref="PivotScriptSlotAttributes.Text"/> and <see cref="PivotScriptSlotAttributes.Control"/>
/// <c>search</c>, drawn with the text it opens with and no value bound after; the list of fields, marked
/// <see cref="PivotScriptSlotAttributes.Fields"/>, one draggable element per field marked
/// <see cref="PivotScriptSlotAttributes.Field"/> holding its checkbox; the four Areas' boxes and their entries, each
/// entry's button pressed and handed in by its key and an empty anchor under it, where its menu and panels open;
/// and Defer Layout Update (<see cref="PivotScriptSlotAttributes.Defer"/>) with Update
/// (<see cref="PivotScriptSlotAttributes.Press"/> <c>update</c>). Every drag and drop is the browser's, read by the
/// script; key the fields and entries, so the element the browser drags stays through a render.
/// </summary>
/// <param name="Title">The pane's name: "PivotTable Fields".</param>
/// <param name="HideLabel">Hide's accessible name.</param>
/// <param name="ChooseCaption">The caption above the fields.</param>
/// <param name="SearchLabel">The search's accessible name and placeholder.</param>
/// <param name="Search">The search its field opens with; the script writes the pane's after.</param>
/// <param name="Fields">The declared fields the search leaves, in declaration order.</param>
/// <param name="DragCaption">The caption above the Areas.</param>
/// <param name="Areas">The four Areas, in Excel's order.</param>
/// <param name="DropRemoves">Whether a drop on the list of fields takes what is dragged out of the layout.</param>
/// <param name="DeferLabel">Defer Layout Update's words.</param>
/// <param name="Deferred">Whether Defer Layout Update is ticked.</param>
/// <param name="UpdateLabel">Update's words.</param>
/// <param name="CanUpdate">Whether Update would apply anything.</param>
public sealed record PivotFieldListView(
    string Title,
    string HideLabel,
    string ChooseCaption,
    string SearchLabel,
    string Search,
    IReadOnlyList<PivotFieldListFieldView> Fields,
    string DragCaption,
    IReadOnlyList<PivotFieldListAreaView> Areas,
    bool DropRemoves,
    string DeferLabel,
    bool Deferred,
    string UpdateLabel,
    bool CanUpdate);

/// <summary>A sheet tab at the report's foot (ADR-0059): the report's own, first, or a Show Details sheet's.</summary>
/// <param name="Key">The name the tab is marked and handed in by (<see cref="PivotScriptSlotAttributes.Tab"/>,
/// <see cref="PivotScriptSlotAttributes.Control"/>), and its close by
/// (<see cref="PivotScriptSlotAttributes.CloseTab"/>).</param>
/// <param name="Title">What the tab reads.</param>
/// <param name="Selected">Whether it is the selected sheet.</param>
/// <param name="PanelId">The id of the panel its records stand in, for the tab's <c>aria-controls</c>; null on the
/// report's tab.</param>
/// <param name="CloseLabel">Its close button's accessible name; null on the report's tab, which does not close.</param>
public sealed record PivotDetailsTabView(string Key, string Title, bool Selected, string? PanelId, string? CloseLabel);

/// <summary>
/// Show Details' sheet tabs when the Script Core draws the pivot (ADR-0059, ADR-0410): data only, drawn again at
/// each change. The Razor draws a tab list named <see cref="Name"/>, one tab per <see cref="Tabs"/> marked
/// <see cref="PivotScriptSlotAttributes.Tab"/> and <see cref="PivotScriptSlotAttributes.Control"/>, and a close
/// button beside each tab that closes, marked <see cref="PivotScriptSlotAttributes.CloseTab"/>. Which sheet is
/// selected, and which tab takes the keyboard, are the pivot's.
/// </summary>
/// <param name="Name">The tab list's accessible name.</param>
/// <param name="Tabs">The report's tab, then each Show Details sheet's, in the order they opened.</param>
public sealed record PivotDetailsTabsView(string Name, IReadOnlyList<PivotDetailsTabView> Tabs);

/// <summary>
/// What Show Details' dialog draws inside the pivot's modal frame when the Script Core draws the pivot (ADR-0059,
/// ADR-0410): its title, an empty element marked <see cref="PivotScriptSlotAttributes.Anchor"/> <c>records</c> where
/// the records stand, and Close, marked <see cref="PivotScriptSlotAttributes.Press"/> and
/// <see cref="PivotScriptSlotAttributes.Control"/> <c>close</c>, which takes the keyboard as the dialog opens. The
/// frame, its backdrop and Escape are the pivot's.
/// </summary>
/// <param name="Title">The cell the records are behind, which names the dialog.</param>
/// <param name="CloseLabel">Close's words.</param>
public sealed record PivotDetailsDialogView(string Title, string CloseLabel);
