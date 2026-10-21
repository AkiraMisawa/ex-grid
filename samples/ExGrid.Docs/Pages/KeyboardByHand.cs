namespace ExGrid.Docs;

/// <summary>
/// The Keyboard reference's rows (ADR-0110): every key of the three products, under the property named for
/// its product, each linking the section of the page that explains it and citing the decision behind it. A
/// key goes here only once a page explains it; the build checks that the section exists and that each cited
/// record does. The selection page's own key table is drawn from the rows that link it, so the two are one
/// list. A row says what its page and its decision say: change those first, and this row with them.
/// <para>Each row names the keys it explains as the products' key tables do (<c>Canonical</c>): ExGrid's
/// <c>GridKeys</c>, and the keys ExSheet and ExPivot declare to their grids. A key one of those tables takes and
/// no row of its product names fails <c>KeyboardCompletenessTests</c>, so a key added to a table is added
/// here, with the page that explains it.</para>
/// </summary>
internal static class KeyboardByHand
{
    private const string Selection = "exgrid/selection#the-keyboard";
    private const string Editing = "exgrid/editing#editable-columns-and-the-edit-intent";
    private const string Bulk = "exgrid/editing#bulk-changes--delete-and-undo";

    /// <summary>ExGrid's keys, which ExSheet's and ExPivot's grids answer too.</summary>
    public static IReadOnlyList<KeyRow> ExGrid { get; } =
    [
        new("Moving and selecting", "[←] [→] [↑] [↓]", "Move the Focus one cell; the selection collapses to it.", Selection, ["ADR-0012"], Canonical: ["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown"]),
        new("Moving and selecting", "[Shift] + arrow", "Extend the range holding the Focus by one cell. The Focus stays.", Selection, ["ADR-0012", "ADR-0052"], Canonical: ["Shift+ArrowLeft", "Shift+ArrowRight", "Shift+ArrowUp", "Shift+ArrowDown"]),
        new("Moving and selecting", "[Ctrl] + arrow", "Jump to the first or last row or column of the result.", Selection, ["ADR-0012"], Canonical: ["Control+ArrowLeft", "Control+ArrowRight", "Control+ArrowUp", "Control+ArrowDown"]),
        new("Moving and selecting", "[Ctrl] + [Shift] + arrow", "Extend to that edge — a whole column in two keys, however many rows it has.", Selection, ["ADR-0012"], Canonical: ["Control+Shift+ArrowLeft", "Control+Shift+ArrowRight", "Control+Shift+ArrowUp", "Control+Shift+ArrowDown"]),
        new("Moving and selecting", "[Home] / [End]", "The row's first or last cell. With [Shift], extend to it.", Selection, ["ADR-0012"], Canonical: ["Home", "End", "Shift+Home", "Shift+End"]),
        new("Moving and selecting", "[Ctrl] + [Home] / [End]", "The first or last cell of the whole result.", Selection, ["ADR-0012"], Canonical: ["Control+Home", "Control+End"]),
        new("Moving and selecting", "[PageUp] / [PageDown]", "Move a Viewport's height. With [Shift], extend.", Selection, ["ADR-0012"], Canonical: ["PageUp", "PageDown", "Shift+PageUp", "Shift+PageDown"]),
        new("Moving and selecting", "[Enter] / [Shift] + [Enter]",
            "With a range selected, move the Focus down each column in turn, wrapping back to the start. With one cell, move down.", Selection,
            ["ADR-0012", "ADR-0052"], Canonical: ["Enter", "Shift+Enter"]),
        new("Moving and selecting", "[Tab] / [Shift] + [Tab]", "The same, along each row. Neither ever leaves the selection.", Selection, ["ADR-0012"], Canonical: ["Tab", "Shift+Tab"]),
        new("Moving and selecting", "[Ctrl] + [A]", "Select everything.", Selection, ["ADR-0012"], Canonical: ["Control+a"]),
        new("Moving and selecting", "[Ctrl] + [Space] / [Shift] + [Space]", "Make the range whole columns / whole rows.", Selection, ["ADR-0012"], Canonical: ["Control+ ", "Shift+ "]),
        new("Moving and selecting", "[Ctrl] + [.]", "Move the Focus to the next corner of its range.", Selection, ["ADR-0052"], Canonical: ["Control+."]),
        new("Moving and selecting", "[Ctrl] + [Backspace]", "Scroll the Focus into view, and change nothing else.", Selection, ["ADR-0052"], Canonical: ["Control+Backspace"]),
        new("Moving and selecting", "[Shift] + [Backspace]", "Collapse the selection to the Focus.", Selection, ["ADR-0052"], Canonical: ["Shift+Backspace"]),
        new("Menus, and leaving the grid", "[Alt] + [↓]", "Open the Focus column's menu: sorting and the filter.", Selection, ["ADR-0039", "ADR-0044"], Canonical: ["Alt+ArrowDown"]),
        new("Menus, and leaving the grid", "[Shift] + [F10] or the menu key", "Open the Context Menu over the selection.", Selection, ["ADR-0036"], Canonical: ["Shift+F10", "ContextMenu"]),
        new("Menus, and leaving the grid", "In a menu: [↑] / [↓], [Home] / [End]",
            "Move among the items that can run, wrapping at the ends; [Home] and [End] go to the first and the last.", Selection, ["ADR-0039"]),
        new("Menus, and leaving the grid", "In a menu: [Enter] / [Space]", "Run the item, and close the menu.", Selection, ["ADR-0039"]),
        new("Menus, and leaving the grid", "In a menu: [Tab] / [Shift] + [Tab]",
            "Close the menu as a Cancel would. Over a column's filter, move to the filter's first or last control instead.", Selection,
            ["ADR-0039", "ADR-0044"]),
        new("Menus, and leaving the grid", "[Escape]",
            "Close what is open. With nothing open, the next [Tab] leaves the grid, typed at once as typed slowly. A [Tab] typed before the grid could tell, straight after a click or a key that might have opened something, stays in the grid, and the grid says so: press it again.",
            Selection, ["ADR-0012", "ADR-0230"], Canonical: ["Escape"]),
        new("Editing", "A character", "On an editable cell, start typing over its value.", Editing, ["ADR-0010"]),
        new("Editing", "[F2]", "Edit the cell's value with a caret. A double-click does the same.", Editing, ["ADR-0010"], Canonical: ["F2"]),
        new("Editing", "[Backspace]", "Empty the cell and start typing in it. Nothing is written until the edit commits.", Editing, ["ADR-0035"], Canonical: ["Backspace"]),
        new("Editing", "[Enter] / [Tab] while editing", "Commit, and move on as [Enter] and [Tab] do, cycling inside the selection.", Editing,
            ["ADR-0010", "ADR-0012"]),
        new("Editing", "Arrow keys, [Shift] + arrow, [Shift] + [Home] or [Ctrl] + arrow while typing over a value",
            "Commit, then move or extend the selection as the same key does with no edit open. After [F2] in the cell they move the caret, or select text.", Editing,
            ["ADR-0010", "ADR-0012"]),
        new("Editing", "[Escape] while editing", "Cancel the edit; nothing is written.", Editing, ["ADR-0010"]),
        new("Editing", "[Ctrl] + [Enter] while editing", "Write what was typed into every selected cell, as one paste.", Bulk, ["ADR-0007"], Canonical: ["Control+Enter"]),
        new("Editing", "[Ctrl] + [D] / [Ctrl] + [R]", "Fill down / fill right across the selection, as one paste.", Bulk, ["ADR-0035"], Canonical: ["Control+d", "Control+r"]),
        new("Editing", "[Delete]", "Clear the selected cells: one OnClear, never a paste of empty text.", Bulk, ["ADR-0054"], Canonical: ["Delete"]),
        new("Editing", "[Ctrl] + [Z]", "Undo, forwarded to your application as OnUndo. While editing, it undoes the typing instead.", Bulk, ["ADR-0007"], Canonical: ["Control+z"]),
        new("Editing", "[Ctrl] + [Y] / [Ctrl] + [Shift] + [Z]", "Redo, forwarded to your application as OnRedo.", Bulk, ["ADR-0007"], Canonical: ["Control+y", "Control+Shift+z"]),
        new("Clipboard", "[Ctrl] + [C] / [Ctrl] + [Insert]",
            "Copy the selection: the text each cell shows, and its full values for Excel. Rows off screen are copied too, or the copy is refused.",
            "exgrid/clipboard#copy", ["ADR-0005"], Canonical: ["Control+c", "Control+Insert"]),
        new("Clipboard", "[Ctrl] + [V] / [Shift] + [Insert]", "Paste into the selection, under the shape rules, as one paste.", "exgrid/clipboard#paste",
            ["ADR-0014"], Canonical: ["Control+v", "Shift+Insert"]),
        new("Clipboard", "[Escape] after a copy", "Take away the dashed outline of what was copied.", "exgrid/clipboard#copy", ["ADR-0170"]),
        new("Find", "[Ctrl] + [F]",
            "Open the find panel. It searches every row of the result, or the selection when several cells are selected.",
            "exgrid/sorting-filtering#with-gridsource-from", ["ADR-0055"], Canonical: ["Control+f"]),
        new("Find", "[Enter] / [Shift] + [Enter] in the find panel", "Go to the next match / the previous one.",
            "exgrid/sorting-filtering#with-gridsource-from", ["ADR-0055"]),
        new("Find", "[Ctrl] + [H]",
            "Open the find panel with Replace: the current match, or every match at once, written by your application. Refused where nothing can be replaced.",
            "exgrid/sorting-filtering#replace", ["ADR-0332"], Canonical: ["Control+h"]),
        new("Action cells", "[Space] on an Action cell",
            "Press its action. On a cell with several, enter it: [←] and [→] choose one, and [Space] presses it.",
            "exgrid/columns#mark--template-and-action-columns", ["ADR-0020", "ADR-0037"], Canonical: [" "]),
    ];

    /// <summary>ExSheet's keys: ExGrid's, and those of a sheet.</summary>
    public static IReadOnlyList<KeyRow> ExSheet { get; } =
    [
        new("The grid's keys", "ExGrid's keys",
            "The keys that move, select and copy are ExGrid's, inside a Sheet as in any grid.", "exsheet#what-exsheet-is", ["ADR-0046"]),
        new("Entries and Formulas", "[F2]", "Edit the cell's Entry, a Formula's text included.", "exsheet/formulas#typing-a-formula", ["ADR-0051"], Canonical: ["F2"]),
        new("Entries and Formulas", "[Enter] / [Escape] while editing",
            "Commit the Entry, or cancel it. A Formula the Sheet cannot read is not committed: the edit stays open.",
            "exsheet/formulas#typing-a-formula", ["ADR-0047", "ADR-0051"]),
        new("Entries and Formulas", "[Tab] in the completion list", "Accept what the list has chosen: a function, a Linked Table, a column, or an argument's value.",
            "exsheet/formulas#completion--hints-and-pointing", ["ADR-0051", "ADR-0058"]),
        new("Entries and Formulas", "Arrow keys while a Formula is typed",
            "Point at a cell and write its Reference where one can go. With [Shift], write a range; [Shift] + [Home] writes one to the row's first column. With [Ctrl], point at the edge of the data, and with [Ctrl] + [Shift], write the range from the cell being edited to it.",
            "exsheet/formulas#completion--hints-and-pointing", ["ADR-0051"]),
        new("Entries and Formulas", "[F4] while a Formula is typed", "Cycle the Reference at the caret through $A$1, A$1, $A1 and A1 — in a range, the end at the caret; a range being pointed at, whole. With the caret inside the text, it is left selected, from after any Sheet qualifier; at the text's end, the caret stays after it.",
            "exsheet/formulas#completion--hints-and-pointing", ["ADR-0051"], Canonical: ["F4"]),
        new("Entries and Formulas", "[Ctrl] + [D] / [Ctrl] + [R]", "Fill down / fill right.",
            "exsheet#a-first-sheet", ["ADR-0035", "ADR-0050"], Canonical: ["Control+d", "Control+r"]),
        new("Entries and Formulas", "[Delete]", "Clear the contents, and keep the formats.", "exsheet#a-first-sheet", ["ADR-0054"], Canonical: ["Delete"]),
        new("Entries and Formulas", "[Ctrl] + [F]", "Find text as it is shown.", "exsheet#a-first-sheet", ["ADR-0055"], Canonical: ["Control+f"]),
        new("Entries and Formulas", "[Ctrl] + [+] / [Ctrl] + [-]",
            "Insert / delete the whole rows or the whole columns the selection spans. Any other selection is refused.",
            "exsheet#a-first-sheet", ["ADR-0050"], Canonical: ["Control++", "Control+-"]),
        new("Entries and Formulas", "[Ctrl] + [Z] / [Ctrl] + [Y]", "Undo / redo, on the Sheet's one undo stack.",
            "exsheet#undo--and-commands-of-your-own", ["ADR-0048"], Canonical: ["Control+z", "Control+y"]),
        new("Formatting", "[Ctrl] + [B] / [I] / [U] / [5]",
            "Toggle bold, italic, underline and strikethrough on the selection, as one undo step.",
            "exsheet/formatting#from-the-keyboard--and-from-your-code", ["ADR-0071"], Canonical: ["Control+b", "Control+i", "Control+u", "Control+5"]),
        new("Formatting", "[Ctrl] + [2] / [3] / [4]", "Bold, italic and underline, as [Ctrl] + [B], [I] and [U] are.",
            "exsheet/formatting#from-the-keyboard--and-from-your-code", ["ADR-0071"], Canonical: ["Control+2", "Control+3", "Control+4"]),
        new("Formatting", "[Ctrl] + [Shift] + [~] [!] [@] [#] [$] [%] [^]",
            "General, #,##0.00, a time, a date, the currency, a percentage and scientific.",
            "exsheet/formatting#from-the-keyboard--and-from-your-code", ["ADR-0071"],
            Canonical: ["Control+Shift+~", "Control+Shift+!", "Control+Shift+@", "Control+Shift+#", "Control+Shift+$", "Control+Shift+%", "Control+Shift+^"]),
        new("Formatting", "[Ctrl] + [Shift] + [&] / [_]", "Draw an outline round each range / remove the borders.",
            "exsheet/formatting#from-the-keyboard--and-from-your-code", ["ADR-0071"], Canonical: ["Control+Shift+&", "Control+Shift+_"]),
        new("Formatting", "[Ctrl] + [1]", "Open Format Cells on the Focus cell's format.", "exsheet/formatting#format-cells", ["ADR-0071"], Canonical: ["Control+1"]),
        new("Formatting", "[Escape] in Format Cells", "Close it and set nothing, as Cancel does.", "exsheet/formatting#format-cells", ["ADR-0071"]),
        new("The Sheet Toolbar", "[Ctrl] + [F1]", "Hide or show the toolbar, where ShowToolbar is bound both ways.",
            "exsheet/formatting#the-sheet-toolbar", ["ADR-0100"], Canonical: ["Control+F1"]),
        new("The Sheet Toolbar", "[Alt] released alone, or [F10]",
            "Show the KeyTips: each row's letter, then each item's, so that [Alt], [H], [1] sets bold and [Alt], [H], [O], [E] opens Format Cells. [Escape] goes back one level.",
            "exsheet/formatting#the-sheet-toolbar", ["ADR-0100"], Canonical: ["Alt+Alt", "F10"]),
        new("Sheet Tabs", "[Ctrl] + [Page Up], [Ctrl] + [Page Down]",
            "Show the Sheet before or after the one shown. Each Sheet keeps its own Selection and scroll position.",
            "exsheet/workbook#sheet-tabs", ["ADR-0331"], Canonical: ["Control+PageUp", "Control+PageDown"]),
        new("Sheet Tabs", "[←], [→], [Home], [End] on a tab",
            "Show the Sheet before or after, the first or the last. [Enter] or [Escape] hands the keyboard back to the grid.",
            "exsheet/workbook#sheet-tabs", ["ADR-0331"]),
        new("Sheet Tabs", "[Shift] + [F11]",
            "Add an empty Sheet before the one shown, named Sheet and the next number, and show it. One undo step.",
            "exsheet/workbook#sheet-tabs", ["ADR-0331"], Canonical: ["Shift+F11"]),
        new("Sheet Tabs", "[Shift] + [F10], the menu key on a tab",
            "Open the tab's menu: Rename, Delete, Move Left, Move Right, Copy. [↑] [↓] choose, [Enter] does it, [Escape] closes it.",
            "exsheet/workbook#sheet-tabs", ["ADR-0331"]),
        new("Sheet Tabs", "[Enter] / [Escape] in a tab's name",
            "Commit the name being edited on its tab, or keep the old one. A name Excel refuses is refused by name.",
            "exsheet/workbook#sheet-tabs", ["ADR-0331"]),
    ];

    /// <summary>ExPivot's keys: ExGrid's on the report, and those of its pane and panels.</summary>
    public static IReadOnlyList<KeyRow> ExPivot { get; } =
    [
        new("The report", "ExGrid's keys", "The report is one ExGrid: moving, selecting and copying are ExGrid's.", "expivot#a-first-pivot",
            ["ADR-0059"]),
        new("The report", "[Shift] + [F10], then Show Details", "Open the records behind the selected value, as a double-click does.",
            "expivot/values#show-details", ["ADR-0059"], Canonical: ["Shift+F10"]),
        new("The report", "[Escape] in Show Details' dialog", "Close the dialog. The report keeps the keyboard.", "expivot/values#show-details",
            ["ADR-0059", "ADR-0070"]),
        new("The report", "[Ctrl] + [F]",
            "Find over the report as shown: row labels, values and totals, by their displayed text. A collapsed group is not searched inside.",
            "expivot/layout#find", ["ADR-0332"]),
        new("The report", "[Ctrl] + [H]", "Opens nothing: a report is not written. The Pivot Toolbar says so, in Excel's words.",
            "expivot/layout#find", ["ADR-0332"]),
        new("The PivotTable Fields pane", "[Space] on a field's checkbox", "Place the field where Excel puts it, or take it out of every Area.",
            "expivot/layout#the-pivottable-fields-pane", ["ADR-0061"]),
        new("The PivotTable Fields pane", "[Enter] on an entry's ▾",
            "Open its menu, which moves it up, down or to another Area: the keyboard's way to drag it.",
            "expivot/layout#the-pivottable-fields-pane", ["ADR-0061"]),
        new("The PivotTable Fields pane", "[Escape] in a menu or a panel",
            "Close it. A panel's changes are discarded unless OK applied them, and the keyboard goes back to what opened it.",
            "expivot/layout#the-pivottable-fields-pane", ["ADR-0061"]),
    ];
}
