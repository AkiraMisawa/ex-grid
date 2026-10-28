// ExPivot's Chrome slots (ADR-0061, ADR-0390 section 3): the parts of the pivot a Wrapper draws in place of the
// built-in markup, as the C# `IPivotChrome` lets a Chrome do. The pivot makes the slot, an element inside the frame
// it places, and hands it to the slot's mount function with what the part shows and the callbacks it answers
// through. What the part means is the pivot's: the commands and their order, which are enabled, what a draft holds
// and what OK makes of it, where the frame stands and what closes it. The contents only draw, and call back. A slot
// left unfilled is drawn by the built-in markup.
//
// A context is handed whole at each mount, as the grid's are (ADR-0390): what the part shows is its `view`, read
// again at each change its `subscribe` tells, and a request for the keyboard mounts the contents again with the next
// `focusRequest`, on the same slot, so a framework that keys its rendering by the slot keeps its component and that
// component's state across the two. Once the part goes, nothing the contents call back reaches the pivot.

import type { GridChrome } from '@ex-grid/core';
import type { PivotAggregation, PivotArea, PivotEntry, PivotItemKey, PivotShowValuesAs } from '@ex-grid/pivot-engine';
import { keyboardIsThePivots } from './parts';

/**
 * A Chrome slot's mount function (ADR-0350 section 9): it draws the part into `slot`, the element the pivot made for
 * it, from `context`, and answers the function that takes what it drew away. The pivot calls that cleanup before the
 * slot goes, and before it mounts again on the same slot with a new context.
 */
export type PivotChromeSlot<Context> = (slot: HTMLElement, context: Context) => () => void;

/** One command of a menu the pivot decides (ADR-0061): what it is called, whether it is available, and what it does. */
export interface PivotMenuCommand {
    /** The command's id, which is also the id of its word, stable for an icon. */
    readonly id: string;
    /** What it is called, in the pivot's words. */
    readonly label: string;
    /** Whether it can be pressed: one that would change nothing is disabled. */
    readonly enabled: boolean;
    /** For a choice among others, the Layout menu's, whether it is the current one, which the menu marks; absent on a
     * plain command. */
    readonly checked?: boolean;
    /** The heading of the group of choices that starts at this command: the Layout menu's Subtotals, Grand Totals and
     * Report Layout; absent where it continues the group above. */
    readonly heading?: string;
    /** Runs it and closes the menu, or opens the panel it names in the menu's place. A disabled command runs nothing,
     * and so does one pressed once the menu has closed or been drawn again without it. */
    readonly invoke: () => void;
}

/** What a menu shows now (ADR-0061): a new object at each change, and the same one otherwise. */
export interface PivotMenuView {
    /** The menu's accessible name, which its frame carries. */
    readonly name: string;
    /** Excel's commands, in its order. */
    readonly commands: readonly PivotMenuCommand[];
}

/** What the slots of a part that takes the keyboard share: their words, and the keyboard's requests. */
export interface PivotPartSlotContext {
    /** One of the pivot's words by its id: the Consumer's `label`, else the built-in English. */
    readonly label: (id: string) => string;
    /** The count of the pivot's requests for the part to take the keyboard; zero asks nothing. Each request mounts the
     * contents again, and whenever the count differs from the one they last answered they give DOM focus, through
     * `takeFocus`, to the control the part's context names. */
    readonly focusRequest: number;
    /** Gives `control` DOM focus: wherever DOM focus stands while a request is owed, until the contents answer it or
     * the user makes an input in the part, and otherwise only while the keyboard is still the pivot's (ADR-0204). */
    readonly takeFocus: (control: HTMLElement) => void;
}

/**
 * What a menu's contents are handed (ADR-0061): a placed field's menu, under its entry in the Field List, or the
 * Layout menu, under Layout ▾. The slot stands inside the pivot's frame (`role="menu"`, named by the pivot), which
 * Escape anywhere in it closes. The contents draw one item per command, in this order, a choice as a
 * `menuitemradio` marked while it is the current one and a group's heading where one starts. A request for the
 * keyboard is for the first enabled command; with none enabled the pivot gives the frame the keyboard itself.
 */
export interface PivotMenuSlotContext extends PivotPartSlotContext {
    /** What the menu shows now. */
    readonly view: () => PivotMenuView;
    /** Hears each change to the view; answers the function that stops it. */
    readonly subscribe: (listener: () => void) => () => void;
    /** Closes the menu without running anything, for a dismissal of the contents' own; the keyboard goes back to what
     * opened it. */
    readonly close: () => void;
}

/** What the slots of a panel share (ADR-0061): its exits, and a popup of the contents' own. A panel holds a draft the
 * pivot keeps; OK says it as the next layout, and Cancel and Escape drop it. */
export interface PivotPanelSlotContext extends PivotPartSlotContext {
    /** Hears each change to the view; answers the function that stops it. */
    readonly subscribe: (listener: () => void) => () => void;
    /** OK: the draft is applied and the panel closes. While the view says it cannot be, or the pivot refuses it, the
     * panel stands and says why. */
    readonly apply: () => void;
    /** Cancel: the draft is dropped, the panel closes, and the keyboard goes back to what opened it. */
    readonly cancel: () => void;
    /** The contents report a popup of their own (a select's list) opening (`true`) and closing (`false`): while one
     * is open, Escape is the popup's, and the next closes the panel (ADR-0039). */
    readonly innerPopupChanged: (open: boolean) => void;
}

/** What Field Settings… shows now (ADR-0061): a new object at each change, and the same one otherwise. */
export interface PivotFieldSettingsView {
    /** The field's caption, the panel's title. */
    readonly caption: string;
    /** The draft's subtotals: Automatic (`true`) or None. */
    readonly subtotals: boolean;
    /** The orders there are, each by its words: A to Z and Z to A by label, then each Value Field's two. */
    readonly sorts: readonly string[];
    /** The draft's order, by its index in `sorts`. */
    readonly sort: number;
}

/** What Field Settings…' contents are handed (ADR-0061): a row or column field's subtotals and order. A request for
 * the keyboard is for the first control, the subtotals' choice. */
export interface PivotFieldSettingsSlotContext extends PivotPanelSlotContext {
    /** What the panel shows now. */
    readonly view: () => PivotFieldSettingsView;
    /** Sets the draft's subtotals. */
    readonly setSubtotals: (automatic: boolean) => void;
    /** Sets the draft's order, by its index in the view's `sorts`. */
    readonly setSort: (index: number) => void;
}

/** A choice a panel offers: its value, its words, and whether it may be chosen. */
export interface PivotChoice<T> {
    /** The choice. */
    readonly value: T;
    /** What it is called. */
    readonly label: string;
    /** Whether it may be chosen: an Aggregation the Pivot Source does not answer is offered, and not available
     * (ADR-0066). */
    readonly enabled: boolean;
    /** Why it may not be chosen, in words; absent while it may. */
    readonly reason?: string;
}

/** What Value Field Settings… shows now (ADR-0061): a new object at each change, and the same one otherwise. */
export interface PivotValueFieldSettingsView {
    /** The Pivot Field's caption, the Source Name. */
    readonly sourceCaption: string;
    /** The draft's caption, as its field shows it: one the user has not written follows the Aggregation. */
    readonly caption: string;
    /** The draft's Aggregation. */
    readonly aggregation: PivotAggregation;
    /** Every Aggregation, with its name and whether the source answers it. */
    readonly aggregations: readonly PivotChoice<PivotAggregation>[];
    /** The draft's Show Values As. */
    readonly showValuesAs: PivotShowValuesAs;
    /** Every Show Values As, with its name. */
    readonly showValuesAsChoices: readonly PivotChoice<PivotShowValuesAs>[];
    /** The draft's number format, as typed; empty for General. */
    readonly numberFormat: string;
    /** What an empty number format means, the field's placeholder: General. */
    readonly numberFormatPlaceholder: string;
    /** A number shown in the draft's format, or the reason it cannot be. */
    readonly sample: string;
    /** Why OK last refused, in words: a caption taken or empty, a format that cannot be used; absent otherwise. */
    readonly refusal?: string;
}

/** What Value Field Settings…' contents are handed (ADR-0061). A request for the keyboard is for the first control,
 * the caption's field. */
export interface PivotValueFieldSettingsSlotContext extends PivotPanelSlotContext {
    /** What the panel shows now. */
    readonly view: () => PivotValueFieldSettingsView;
    /** The caption as typed. */
    readonly setCaption: (text: string) => void;
    /** Sets the draft's Aggregation: one the source does not answer is never chosen. */
    readonly setAggregation: (aggregation: PivotAggregation) => void;
    /** Sets the draft's Show Values As. */
    readonly setShowValuesAs: (showValuesAs: PivotShowValuesAs) => void;
    /** The number format as typed. */
    readonly setNumberFormat: (text: string) => void;
}

/** One Item as Filter… lists it. */
export interface PivotItemChoice {
    /** The Item. */
    readonly key: PivotItemKey;
    /** Its label. */
    readonly label: string;
    /** Whether it is shown in the draft. */
    readonly ticked: boolean;
}

/** What Filter… shows now (ADR-0061, ADR-0066): a new object at each change, and the same one otherwise. */
export interface PivotItemFilterView {
    /** The field's caption, which names the list. */
    readonly caption: string;
    /** The search as typed. */
    readonly search: string;
    /** The Items the search leaves, in the field's order, at most the most Filter… lists. */
    readonly items: readonly PivotItemChoice[];
    /** (Select All)'s state over `items`: `true` for every one, `false` for none (and over no Items), or `undefined` for
     * some. */
    readonly allTicked: boolean | undefined;
    /** Whether the Items, or the source's answer to the search, are on their way, with nothing yet to list. */
    readonly loading: boolean;
    /** Whether newer Items are on their way while what is shown stands: an earlier Source Version's Items, kept in
     * view (ADR-0066 refined), or why they could not be listed, while they are asked again. A Chrome may mark the list
     * as updating; it does not blank it, nor dim it. */
    readonly updating: boolean;
    /** Why the Items cannot be listed, in words, said in place of the list; absent while they can. */
    readonly unavailable?: string;
    /** A note under the Items: that more match than are listed, or that none matches the search; absent otherwise. */
    readonly note?: string;
    /** Whether OK may be pressed: not while every Item is unticked, nor while the Items are on their way. */
    readonly canApply: boolean;
    /** Why OK cannot apply, in words; absent while it can. */
    readonly refusal?: string;
}

/** What Filter…' contents are handed (ADR-0061, ADR-0066): a field's Items, each ticked while shown, a search that
 * narrows them, and OK and Cancel. A request for the keyboard is for the search. */
export interface PivotItemFilterSlotContext extends PivotPanelSlotContext {
    /** What the panel shows now. */
    readonly view: () => PivotItemFilterView;
    /** The search as typed. */
    readonly setSearch: (text: string) => void;
    /** Ticks or unticks one Item. */
    readonly setTicked: (key: PivotItemKey, ticked: boolean) => void;
    /** (Select All): ticks or unticks every Item the search lists. */
    readonly setAll: (ticked: boolean) => void;
}

/** Takes one of a standing part's elements, or `null` once it is gone, as a callback ref does: hand it the element
 * itself (a React `ref`, or a call with the element once it is drawn). */
export type PivotPartRef = (element: HTMLElement | null) => void;

/** What the slots of the pivot's standing parts share (ADR-0390): the Pivot Toolbar, the report filter band and the
 * Stale Report's notice. Their contents are mounted once while the part stands, and draw its view again at each change.
 * They hand in their controls by name, through `control`, so the pivot gives the keyboard to the one it means, as it
 * does with the built-in markup's: after a menu closes, to the button that opened it. They hand in the anchors the
 * pivot places a popup's frame in, through `anchor`: an element around the button that opens it, positioned, so the
 * frame needs no position the pivot would have to measure (ADR-0061). */
export interface PivotStandingSlotContext<View> {
    /** What the part shows now: a new object at each change, and the same one otherwise. */
    readonly view: () => View;
    /** Hears each change to the view; answers the function that stops it. */
    readonly subscribe: (listener: () => void) => () => void;
    /** One of the pivot's words by its id: the Consumer's `label`, else the built-in English. */
    readonly label: (id: string) => string;
}

/** What the Pivot Toolbar shows now (ADR-0061). */
export interface PivotToolbarView {
    /** Layout ▾: its words, and whether its menu is open. */
    readonly layoutMenu: { readonly label: string; readonly open: boolean };
    /** Refresh, while the source can be refreshed (ADR-0066); absent otherwise, and then there is no button. */
    readonly refresh?: { readonly label: string };
    /** The Field List's toggle: its words, and whether the list is shown. */
    readonly fieldList: { readonly label: string; readonly shown: boolean };
    /** Whether the report filter band stands, on the toolbar's left: the contents then hand in its anchor,
     * `anchor('report-filters')`, in which the pivot places the band. */
    readonly reportFilters: boolean;
    /** What the report could not do with the last change, in words — a layout refused by name, or the source's
     * failure — said where the user sees it, as an alert (`role="alert"`); absent otherwise. Data that could not be
     * shown is the Stale Report's notice's to say, not this. */
    readonly refusal?: string;
}

/**
 * What the Pivot Toolbar's contents are handed (ADR-0061): the report filter band on its left, and on its right, in
 * this order, Layout ▾, Refresh where the source can be refreshed, and the Field List's toggle. The Layout menu opens
 * in the anchor around Layout ▾, over the report, with a backdrop that closes it.
 */
export interface PivotToolbarSlotContext extends PivotStandingSlotContext<PivotToolbarView> {
    /** Layout ▾ pressed: opens the Layout menu, or closes it when it is open. */
    readonly toggleLayoutMenu: () => void;
    /** Refresh pressed: asks the source again. */
    readonly refresh: () => void;
    /** The Field List's toggle pressed: shows the list, or hides it. */
    readonly toggleFieldList: () => void;
    /** Hands in a button: `layout`, `refresh` or `field-list`. */
    readonly control: (name: 'layout' | 'refresh' | 'field-list') => PivotPartRef;
    /** Hands in an anchor: `layout`, around Layout ▾, and `report-filters`, where the band stands. */
    readonly anchor: (name: 'layout' | 'report-filters') => PivotPartRef;
}

/** One report filter (ADR-0061). */
export interface PivotReportFilterView {
    /** The field's name. */
    readonly field: string;
    /** Its caption. */
    readonly caption: string;
    /** What it shows: (All), the one Item shown, or (Multiple Items). */
    readonly summary: string;
    /** Whether it hides Items. */
    readonly filtered: boolean;
    /** Whether its Filter… is open. */
    readonly open: boolean;
    /** The button's accessible name: "Filter Region". */
    readonly buttonLabel: string;
}

/** What the report filter band shows now (ADR-0061). */
export interface PivotReportFiltersView {
    /** The band's accessible name, which a `role="group"` around it carries. */
    readonly name: string;
    /** One per field in Filters, in their order. */
    readonly filters: readonly PivotReportFilterView[];
}

/** What the report filter band's contents are handed (ADR-0061): one entry per field in Filters, its caption and a
 * button that says what it shows and opens Filter… in the anchor around it, over the report. */
export interface PivotReportFiltersSlotContext extends PivotStandingSlotContext<PivotReportFiltersView> {
    /** A filter's button pressed: opens its Filter…, or closes it when it is open. */
    readonly toggle: (field: string) => void;
    /** Hands in the button of the filter of `field`. */
    readonly control: (field: string) => PivotPartRef;
    /** Hands in the anchor around the button of the filter of `field`. */
    readonly anchor: (field: string) => PivotPartRef;
}

/** What the Stale Report's notice shows now (ADR-0067). */
export interface PivotStaleReportView {
    /** The whole sentence: "Showing the data as of 14:32:05: the newest data needs more than 200,000 cells." */
    readonly message: string;
    /** When the data on screen arrived, as the sentence writes it. */
    readonly asOfText: string;
    /** What happened, as the sentence ends it. */
    readonly reason: string;
    /** Retry: its words, and whether it can be pressed — not while what it would ask is already out. */
    readonly retry: { readonly label: string; readonly enabled: boolean };
}

/** What the Stale Report's notice's contents are handed (ADR-0067), inside the pivot's polite live region under the
 * Pivot Toolbar, while the report is stale; the contents add no live region of their own. */
export interface PivotStaleReportSlotContext extends PivotStandingSlotContext<PivotStaleReportView> {
    /** Retry pressed: asks again what failed. */
    readonly retry: () => void;
    /** Hands in Retry. */
    readonly control: (name: 'retry') => PivotPartRef;
}

/** A declared field in the Field List (ADR-0061). */
export interface PivotFieldListFieldView {
    /** The field's name. */
    readonly name: string;
    /** What it is called. */
    readonly caption: string;
    /** The name its tick box is handed in by, through `control`. */
    readonly key: string;
    /** Whether it stands in any Area: its tick box. */
    readonly placed: boolean;
    /** Whether it hides Items now. */
    readonly filtered: boolean;
    /** Its count of Items, written in the pivot's locale, where the source counted them; absent otherwise. */
    readonly count?: string;
    /** The count as said: "12 Items"; absent with the count. */
    readonly countLabel?: string;
    /** Whether it is the one being dragged. */
    readonly dragging: boolean;
}

/** A placed entry of an Area (ADR-0061). */
export interface PivotFieldListEntryView {
    /** Which entry it is: its Area and index there, −1 for Σ Values. */
    readonly entry: PivotEntry;
    /** The name its button and the anchor around it are handed in by, through `control` and `anchor`. */
    readonly key: string;
    /** What it is called: the field's caption, a Value Field's caption, Σ Values. */
    readonly caption: string;
    /** The button's accessible name: "Options for Region". */
    readonly menuLabel: string;
    /** Whether its field hides Items now. */
    readonly filtered: boolean;
    /** Whether it is Σ Values. */
    readonly values: boolean;
    /** Whether its menu, or a panel the menu opened, stands under it: the entry is then not draggable, so a press that
     * moves a little on the menu is the menu's. */
    readonly menuOpen: boolean;
    /** Whether it is the one being dragged. */
    readonly dragging: boolean;
    /** Whether a drop now would land before it. */
    readonly dropBefore: boolean;
    /** The index a `dragenter` on it hands `dragEnter`, and a `drop` on it hands `drop`: its own, or for Σ Values,
     * which always stands last, its Area's `end`. */
    readonly dropIndex: number;
}

/** One Area of the Field List (ADR-0061). */
export interface PivotFieldListAreaView {
    /** Which Area. */
    readonly area: PivotArea;
    /** Its name: Filters, Columns, Rows, Values. */
    readonly title: string;
    /** What stands in it, Σ Values last where it stands. */
    readonly entries: readonly PivotFieldListEntryView[];
    /** The index a `dragenter` or a `drop` on the Area's box itself hands on: past its last entry. */
    readonly end: number;
    /** Whether what is dragged is over it, and may be dropped there. */
    readonly dropTarget: boolean;
    /** While a drop there would pass the leaf cap, by the counts, the words that say so, in a `role="status"`: a hint,
     * never a refusal (ADR-0370 section 3). Absent otherwise. */
    readonly overCap?: string;
}

/** What the Field List shows now (ADR-0061). */
export interface PivotFieldListView {
    /** The pane's name: "PivotTable Fields". */
    readonly title: string;
    /** The heading's close button's accessible name: "Hide Field List". */
    readonly hideLabel: string;
    /** The caption above the fields: "Choose fields to add to report:". */
    readonly chooseCaption: string;
    /** The search's accessible name and placeholder. */
    readonly searchLabel: string;
    /** The search as typed. */
    readonly search: string;
    /** The declared fields the search leaves, in declaration order. */
    readonly fields: readonly PivotFieldListFieldView[];
    /** The caption above the Areas: "Drag fields between areas below:". */
    readonly dragCaption: string;
    /** The four Areas, in Excel's order: Filters, Columns, Rows, Values. */
    readonly areas: readonly PivotFieldListAreaView[];
    /** Whether a drop on the list of fields, or on the report, removes what is dragged: a placed field's entry. */
    readonly dropRemoves: boolean;
    /** Defer Layout Update, at the pane's foot: its words, and whether it is ticked. */
    readonly defer: { readonly label: string; readonly ticked: boolean };
    /** Update: its words, and whether it would apply anything. */
    readonly update: { readonly label: string; readonly enabled: boolean };
}

/** What is dragged in the Field List: a declared field from the list of fields, or a placed entry of an Area. */
export type PivotDragSubject = { readonly field: string } | { readonly entry: PivotEntry };

/**
 * What the Field List's contents are handed (ADR-0061): the fields with their tick boxes and search, and the four
 * Areas with their entries, each entry's open menu or panel placed by the pivot in the anchor around its button.
 * Drag and drop is the browser's own: a draggable element calls `startDrag` on `dragstart` and `endDrag` on `dragend`;
 * a drop target calls `dragOver` on `dragover` always — whether it may be dropped on is decided at the drop, never by a
 * render that may still be on its way — `dragEnter` on `dragenter` for the marks, and `drop` (or `dropOnList`) on
 * `drop`. Each Area's box is handed in by `area`, so a drag that leaves every Area is seen to.
 */
export interface PivotFieldListSlotContext extends PivotStandingSlotContext<PivotFieldListView> {
    /** The search as it is typed. */
    readonly setSearch: (text: string) => void;
    /** A field's tick box pressed: ticks it into the layout, or takes it out. */
    readonly toggle: (field: string) => void;
    /** An entry's button pressed: opens its menu, or closes the one standing under it. */
    readonly openMenu: (entry: PivotEntry) => void;
    /** The heading's close button pressed: hides the Field List, keeping the layout and any pending one. */
    readonly close: () => void;
    /** A `dragstart` on a field or an entry: the drag starts, against the layout the pane shows. */
    readonly startDrag: (event: DragEvent, subject: PivotDragSubject) => void;
    /** A `dragover` on any drop target: takes the drop while a drag of the pane's is on. */
    readonly dragOver: (event: DragEvent) => void;
    /** A `dragenter` on an Area (`index` its entries' count) or an entry (`index` its own): where a drop would land. */
    readonly dragEnter: (area: PivotArea, index: number) => void;
    /** A `drop` on an Area or an entry, at the index `dragEnter` takes. */
    readonly drop: (event: DragEvent, area: PivotArea, index: number) => void;
    /** A `drop` on the list of fields: an entry dragged back is taken out of the layout. */
    readonly dropOnList: (event: DragEvent) => void;
    /** A `dragend`, whether or not anything was dropped. */
    readonly endDrag: () => void;
    /** Defer Layout Update ticked or unticked; unticking applies the pending layout, as Excel does. */
    readonly setDeferred: (deferred: boolean) => void;
    /** Update pressed: applies the pending layout in one change. */
    readonly update: () => void;
    /** Hands in a control: `search`, a field's tick box by its `key`, or an entry's button by its `key`. */
    readonly control: (key: string) => PivotPartRef;
    /** Hands in the anchor around an entry's button, or beside it, by the entry's `key`: an element of no position of
     * its own, so the frame the pivot places in it takes the pane's width (ADR-0061). A press or focus on the entry's
     * button, handed in by `control`, is inside its open menu wherever the anchor stands. */
    readonly anchor: (key: string) => PivotPartRef;
    /** Hands in an Area's box. */
    readonly area: (area: PivotArea) => PivotPartRef;
}

/** A sheet's tab at the report's foot (ADR-0059): the report's own, first, or a Show Details sheet's. */
export interface PivotDetailsTabView {
    /** The name the tab's control is handed in by, and `select` and `close` are handed. */
    readonly key: string;
    /** What the tab reads: the report's own words, or the cell the records are behind. */
    readonly title: string;
    /** Whether it is the selected sheet: a details sheet's records then cover the report. */
    readonly selected: boolean;
    /** The id of the panel its records stand in, for the control's `aria-controls`; absent on the report's tab. */
    readonly panelId?: string;
    /** Its close button's words, said as "Close …"; absent on the report's tab, which does not close. */
    readonly closeLabel?: string;
}

/** What the sheet tabs show now (ADR-0059). */
export interface PivotDetailsTabsView {
    /** The tab list's name. */
    readonly name: string;
    /** The report's tab, then each Show Details sheet's, in the order they opened. */
    readonly tabs: readonly PivotDetailsTabView[];
}

/** What the sheet tabs' contents are handed (ADR-0059), at the report's foot while a Show Details sheet stands. The
 * selected sheet's records are the pivot's, in a panel of its own over the report; the tab that opens takes the
 * keyboard, and so does the one selected once a tab holding it closes. */
export interface PivotDetailsTabsSlotContext extends PivotStandingSlotContext<PivotDetailsTabsView> {
    /** A tab pressed, by its `key`: its sheet is selected. */
    readonly select: (key: string) => void;
    /** A details tab's close pressed, by its `key`: Excel's next sheet is selected, or the report. */
    readonly close: (key: string) => void;
    /** Hands in the control of the tab of `key`. */
    readonly control: (key: string) => PivotPartRef;
}

/** What the Show Details dialog shows now (ADR-0059). */
export interface PivotDetailsDialogView {
    /** The cell the records are behind, which names the dialog. */
    readonly title: string;
    /** Close's words. */
    readonly closeLabel: string;
}

/** What the Show Details dialog's contents are handed (ADR-0059): its title, the anchor its records stand in and
 * Close, inside the pivot's modal frame, which keeps the backdrop and Escape. Close takes the keyboard as the dialog
 * opens, and the report's grid takes it back however it closes. */
export interface PivotDetailsDialogSlotContext extends PivotStandingSlotContext<PivotDetailsDialogView> {
    /** Close pressed. */
    readonly close: () => void;
    /** Hands in Close. */
    readonly control: (name: 'close') => PivotPartRef;
    /** Hands in the element the records stand in: their box takes the room the dialog gives it. */
    readonly anchor: (name: 'records') => PivotPartRef;
}

/** A stylesheet a Chrome brings (ADR-0123), as its notice names it: a Wrapper has no root of its own (ADR-0062), so
 * its Chrome declares the stylesheets it brings, and the pivot's root renders a notice for each beside its own. */
export interface PivotChromeStylesheet {
    /** The stylesheet's source file name, as the rules at the end of the shipped stylesheet name it: `mud-ex-pivot.css`. */
    readonly name: string;
    /** The SHA-256 of that source, as lower-case hex: the stamp the stylesheet the Chrome was built with hides its
     * notice for. */
    readonly stamp: string;
}

/**
 * The Chrome slots a pivot takes (ADR-0061, ADR-0390): the parts a Wrapper draws, each in place of the built-in
 * markup's. A slot not given is the built-in markup's. A slot is read when its part opens: a part standing keeps the
 * slot it opened with.
 */
export interface PivotChrome {
    /** The stylesheets this Chrome brings (ADR-0123): the pivot's root renders a notice for each beside its own, which
     * only another version's stylesheet shows. The report grid's Chrome declares its own. None where not given. */
    readonly stylesheets?: readonly PivotChromeStylesheet[];
    /** The report grid's Chrome, and Show Details' grids', so one Chrome dresses the pivot and the grids it draws. The
     * Context Menu's commands are worded by the pivot. */
    readonly grid?: GridChrome<unknown>;
    /** A placed field's menu and the Layout menu. */
    readonly menu?: PivotChromeSlot<PivotMenuSlotContext>;
    /** Filter…, under a field's entry or a report filter's button. */
    readonly itemFilter?: PivotChromeSlot<PivotItemFilterSlotContext>;
    /** Field Settings… of a row or column field. */
    readonly fieldSettings?: PivotChromeSlot<PivotFieldSettingsSlotContext>;
    /** Value Field Settings…. */
    readonly valueFieldSettings?: PivotChromeSlot<PivotValueFieldSettingsSlotContext>;
    /** The Pivot Toolbar above the report. */
    readonly toolbar?: PivotChromeSlot<PivotToolbarSlotContext>;
    /** The report filter band, on the Pivot Toolbar's left. */
    readonly reportFilters?: PivotChromeSlot<PivotReportFiltersSlotContext>;
    /** The Stale Report's notice, under the Pivot Toolbar. */
    readonly staleReport?: PivotChromeSlot<PivotStaleReportSlotContext>;
    /** The Field List: Excel's PivotTable Fields pane. Its slot draws no box of its own, so the contents fill the
     * Field List's height (`height: 100%`). */
    readonly fieldList?: PivotChromeSlot<PivotFieldListSlotContext>;
    /** The sheet tabs at the report's foot, while Show Details' records stand in a tab. */
    readonly detailsTabs?: PivotChromeSlot<PivotDetailsTabsSlotContext>;
    /** What the Show Details dialog draws inside its frame, a flex column; the slot draws no box of its own. */
    readonly detailsDialog?: PivotChromeSlot<PivotDetailsDialogSlotContext>;
}

const slotNames: readonly Exclude<keyof PivotChrome, 'stylesheets'>[] = ['grid', 'menu', 'itemFilter', 'fieldSettings', 'valueFieldSettings', 'toolbar', 'reportFilters', 'staleReport', 'fieldList', 'detailsTabs',
    'detailsDialog'];

/** A pivot's `chrome`, checked as a script may hand it: an object whose slots are each a mount function, and whose
 * `grid` is an object the report grid checks. */
export function checkedPivotChrome(chrome: unknown): PivotChrome | undefined {
    if (chrome === undefined) return undefined;
    if (typeof chrome !== 'object' || chrome === null) {
        throw new TypeError('An ExPivot\'s chrome is an object of Chrome slots, each a mount function (ADR-0390).');
    }
    for (const [name, slot] of Object.entries(chrome)) {
        if (name === 'stylesheets') {
            checkedStylesheets(slot);
            continue;
        }
        if (!(slotNames as readonly string[]).includes(name)) {
            throw new TypeError(`An ExPivot's chrome has no slot '${name}': its slots are ${slotNames.join(', ')} (ADR-0390).`);
        }
        if (name === 'grid') {
            if (slot !== undefined && (typeof slot !== 'object' || slot === null)) {
                throw new TypeError('An ExPivot\'s chrome.grid is a grid\'s chrome: an object of its Chrome slots (ADR-0390).');
            }
        } else if (slot !== undefined && typeof slot !== 'function') {
            throw new TypeError(`An ExPivot's chrome.${name} is a mount function, (slot, context) => cleanup (ADR-0350 section 9).`);
        }
    }
    return chrome;
}

/** A Chrome's `stylesheets`, checked as a script may hand them: each a name and a SHA-256 stamp the notice carries. */
function checkedStylesheets(stylesheets: unknown): void {
    if (stylesheets === undefined) return;
    if (!Array.isArray(stylesheets)) throw new TypeError('An ExPivot\'s chrome.stylesheets is an array of { name, stamp } (ADR-0123).');
    for (const stylesheet of stylesheets as readonly unknown[]) {
        const name: unknown = typeof stylesheet === 'object' && stylesheet !== null ? Reflect.get(stylesheet, 'name') : undefined;
        const stamp: unknown = typeof stylesheet === 'object' && stylesheet !== null ? Reflect.get(stylesheet, 'stamp') : undefined;
        // Any stamp is taken, as C#'s ChromeStylesheet takes it: one no shipped stylesheet carries leaves the notice
        // showing, which says so, not quietly.
        if (typeof name !== 'string' || name === '' || typeof stamp !== 'string' || stamp === '') {
            throw new TypeError('Each of an ExPivot\'s chrome.stylesheets is { name, stamp }: the stylesheet\'s file name, and the stamp its own rule hides the notice for (ADR-0123).');
        }
    }
}

/** Mounts `context` into `slot` by `mount`, and answers the cleanup it returned, checked. */
function mounted<Context>(name: string, mount: PivotChromeSlot<Context>, slot: HTMLElement, context: Context): () => void {
    const cleanup: unknown = mount(slot, context);
    if (typeof cleanup !== 'function') {
        throw new TypeError(`An ExPivot's chrome.${name} answered ${typeof cleanup}: a mount function answers the function that takes its contents away.`);
    }
    return () => { Reflect.apply(cleanup, undefined, []); };
}

/** The listeners a slot's view is told each change through, by the context's `subscribe`. */
export class ViewListeners {
    readonly #listeners = new Set<() => void>();

    /** The context's `subscribe`: adds `listener`, and answers the function that removes it. */
    readonly subscribe = (listener: () => void): (() => void) => {
        if (typeof listener !== 'function') throw new TypeError('subscribe is handed the function that hears each change to the view.');
        this.#listeners.add(listener);
        return () => { this.#listeners.delete(listener); };
    };

    /** Tells every listener the view changed. */
    tell(): void {
        for (const listener of this.#listeners) listener();
    }

    /** The part is gone: nothing is told any more. */
    clear(): void {
        this.#listeners.clear();
    }
}

/**
 * A Chrome slot's contents while their part stands (ADR-0390): mounted with a context, mounted again with the next
 * one for each request for the keyboard, and taken away when the part goes, after which nothing they call back
 * reaches the pivot. The focus a request asks for is owed, the pivot's to give wherever DOM focus stands, until the
 * contents give it or the user makes an input in the part; any other focus they ask for is given only while the
 * keyboard is still the pivot's (ADR-0204), as ExGrid's own slots do.
 */
export class SlotMount<Context> {
    readonly #name: string;
    readonly #mount: PivotChromeSlot<Context>;
    /** The slot, which the pivot stands inside the part's frame. */
    readonly element: HTMLElement;
    readonly #context: (focusRequest: number) => Context;
    readonly #cancel: () => void;
    #cleanup: (() => void) | undefined;
    #focusRequest = 0;
    #owed = false;
    #live = true;
    /** Whether a popup of the contents' own is open: Escape is then the popup's (ADR-0039). */
    innerPopupOpen = false;

    /** `context` makes the context for a focus request's count; `cancel` closes the part. */
    constructor(name: string, mount: PivotChromeSlot<Context>, element: HTMLElement, context: (focusRequest: number) => Context, cancel: () => void) {
        this.#name = name;
        this.#mount = mount;
        this.element = element;
        this.#context = context;
        this.#cancel = cancel;
        element.setAttribute('data-ex-pivot-slot', name);
    }

    /** Whether the part stands. */
    get live(): boolean {
        return this.#live;
    }

    /** Mounts the contents, once. */
    mount(): void {
        if (this.#live && this.#cleanup === undefined) this.#draw();
    }

    /** A request for the keyboard: the contents are mounted again with the next count. */
    request(): void {
        if (!this.#live) return;
        this.#focusRequest += 1;
        this.#owed = true;
        this.#unmount();
        this.#draw();
    }

    /** `change` as an input the user made in the part: no focus is owed any longer, and nothing is made once the part
     * is gone. */
    input<Args extends unknown[]>(change: (...args: Args) => void): (...args: Args) => void {
        return (...args: Args): void => {
            if (!this.#live) return;
            this.#owed = false;
            change(...args);
        };
    }

    /** The part is going: its contents are taken away. */
    dispose(): void {
        if (!this.#live) return;
        this.#live = false;
        this.#unmount();
    }

    readonly innerPopupChanged = (open: boolean): void => {
        if (typeof open !== 'boolean') throw new TypeError('innerPopupChanged is handed whether the popup is open.');
        if (this.#live) this.innerPopupOpen = open;
    };

    readonly takeFocus = (control: HTMLElement): void => {
        if (!(control instanceof HTMLElement)) throw new TypeError('takeFocus is handed the element that takes the keyboard.');
        if (!this.#live || !control.isConnected) return;
        const owed = this.#owed;
        this.#owed = false;
        const pivot = this.element.closest('.ex-pivot');
        if (owed || (pivot !== null && keyboardIsThePivots(pivot))) control.focus();
    };

    // Contents that cannot be drawn close the part, rather than leave it standing empty.
    #draw(): void {
        try {
            this.#cleanup = mounted(this.#name, this.#mount, this.element, this.#context(this.#focusRequest));
        } catch (error) {
            this.#cancel();
            throw error;
        }
    }

    // The contents go, and a popup of theirs with them: they are not asked to say so.
    #unmount(): void {
        const cleanup = this.#cleanup;
        this.#cleanup = undefined;
        this.innerPopupOpen = false;
        cleanup?.();
    }
}

/**
 * A standing part's slot (ADR-0390): its contents are mounted once, with a context of their own, while the part
 * stands and the same mount function is given, and told each change to its view. Another mount function, the
 * Consumer having handed another `chrome`, mounts again; once the part goes, nothing the contents call back reaches
 * the pivot. The slot is an element of its own, which the pivot stands in the part's place. Contents that cannot be
 * drawn are refused by name once, and the part is the built-in markup's while that mount function is given.
 */
export class StandingSlot<View, Context> {
    readonly #name: string;
    /** The slot. */
    readonly element: HTMLElement;
    // Each mount's own, so contents a framework keeps across a mount subscribe again to the next one's.
    #listeners = new ViewListeners();
    #view: View;
    // What the view held says, so a draw that shows the same thing makes nothing new and tells nobody; worked out only
    // while contents stand to be told.
    #said: string | undefined;
    #mount: PivotChromeSlot<Context> | undefined;
    #cleanup: (() => void) | undefined;
    #failed: PivotChromeSlot<Context> | undefined;
    // Counts the mounts, so a callback handed to one reaches nothing once another stands.
    #mounts = 0;

    constructor(name: string, document: Document, view: View) {
        this.#name = name;
        this.element = document.createElement('div');
        this.element.setAttribute('data-ex-pivot-slot', name);
        this.#view = view;
    }

    /** The view the part shows: `next`, or the one held where it shows the same, so it stays the same object while
     * nothing it shows changed. */
    viewOf(next: View): View {
        if (this.#cleanup === undefined) {
            this.#view = next;
            this.#said = undefined;
            return next;
        }
        const said = JSON.stringify(next);
        const held = this.#said === undefined ? JSON.stringify(this.#view) : this.#said;
        this.#said = said;
        if (said === held) return this.#view;
        this.#view = next;
        this.#listeners.tell();
        return next;
    }

    /** Whether the contents `mount` draws stand. */
    stands(mount: PivotChromeSlot<Context>): boolean {
        return this.#cleanup !== undefined && this.#mount === mount;
    }

    /** Whether `mount` could not be drawn: it is not tried again. */
    refuses(mount: PivotChromeSlot<Context>): boolean {
        return this.#failed === mount;
    }

    /** Shows the view held by `mount`: the contents are mounted with the context `context` makes, unless `mount`
     * already stands. `context` is handed the shared parts and an `input` that keeps a callback from reaching the
     * pivot once these contents are gone. Answers whether the contents stand; a mount function that throws, or answers
     * no cleanup, is not tried again, and its error is answered for the pivot to throw once it has drawn. */
    show(mount: PivotChromeSlot<Context>,
        context: (shared: PivotStandingSlotContext<View>, input: <Args extends unknown[]>(change: (...args: Args) => void) => (...args: Args) => void) => Context,
        label: (id: string) => string): { readonly shown: boolean; readonly error?: Error } {
        if (this.refuses(mount)) return { shown: false };
        if (this.stands(mount)) return { shown: true };
        this.hide();
        this.#mounts += 1;
        const made = this.#mounts;
        const input = <Args extends unknown[]>(change: (...args: Args) => void): ((...args: Args) => void) => (...args: Args): void => {
            if (this.#cleanup !== undefined && this.#mounts === made) change(...args);
        };
        this.#listeners = new ViewListeners();
        const shared: PivotStandingSlotContext<View> = { view: () => this.#view, subscribe: this.#listeners.subscribe, label };
        try {
            this.#cleanup = mounted(this.#name, mount, this.element, Object.freeze(context(shared, input)));
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- answered, and thrown by the pivot once it has drawn the built-in markup in its place
            this.#failed = mount;
            return { shown: false, error: error instanceof Error ? error : new Error(`An ExPivot's chrome.${this.#name} could not be drawn (ADR-0390).`, { cause: error }) };
        }
        this.#mount = mount;
        return { shown: true };
    }

    /** What the part shows now. */
    get view(): View {
        return this.#view;
    }

    /** The part shows none of the contents: they are taken away. */
    hide(): void {
        const cleanup = this.#cleanup;
        this.#cleanup = undefined;
        this.#mount = undefined;
        this.#listeners.clear();
        cleanup?.();
    }
}
