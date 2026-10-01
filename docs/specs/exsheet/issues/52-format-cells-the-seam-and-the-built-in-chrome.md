# 52: Format Cells: the seam, the built-in Chrome, and the Context Menu

Status: done

**What to build:** [ADR-0071](../../../adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md), "Format Cells, a Chrome seam whose frame the Chrome chooses", and
[ADR-0050](../../../adr/0050-what-exsheet-asks-of-exgrids-core.md) item 16.

**Blocked by:** None (can start immediately). Ticket 50 is done; the eleventh Windows run's group 5
is in `verification/2026-10-01-windows-excel-11/cell-format.md` (cases 22–26).

- [x] **Core: a Consumer's popover in the grid's frame** (DC-60).
  - It stays inside the box and is bounded by it.
  - It takes and returns the keyboard.
  - It closes as a Cancel below one row.
- [x] **ExSheet: the seam's contract** (SH-45).
  - **What it offers.**
    - Tabs: Number, Alignment, Font, Border and Fill, in Excel's order.
    - Number Format categories in Excel's order (case 22). Accounting and Fraction are disabled,
      with the reason.
    - Custom takes a code, and a code ExSheet does not read is refused by name.
    - Alignment is horizontal only.
    - The palette is the run's (case 23).
    - The thirteen line styles and the border presets.
  - **How it opens.** It opens on the Focus cell's Cell Format. Parts that differ across the
    Selection are shown as case 24 found.
  - **How it closes.** OK is a `CellFormatChange` of the parts the user touched, as one undo step.
    Cancel and Esc set nothing.
  - **OK with nothing touched** calls nothing. `SetCellFormatAsync` throws on a change that names
    no part (found by ticket 50).
  - The frame is the Chrome's. The seam hands the Chrome the core's focus function, for returning
    the keyboard on closing.
- [x] **The built-in Chrome.**
  - It is a popover through item 16.
  - More Colours takes a hex value.
  - The tabs switch with the arrow keys (ARIA's tabs pattern).
  - No script is added.
- [ ] **What opens it**: Ctrl+1 (with ticket 51), a "Format Cells…" Context Menu command, and
      `OpenFormatCellsAsync()`. Each is refused while an edit is open.
- [x] Layer 2; Layer 3 on both hosts.

What the eleventh run found, for this ticket (ADR-0071, "What the eleventh Windows run settled"):

- [x] Tabs in Excel's order; the first open is Number, and a later Ctrl+1 reopens on the last tab
      shown, per Sheet instance (case 22).
- [x] The Number tab's categories: General, Number, Currency, Accounting, Date, Time, Percentage,
      Fraction, Scientific, Text, Special, Custom. Accounting and Fraction disabled with the reason;
      Special as Excel lists it, or disabled with the reason if its codes are not read.
- [x] The Border tab's line styles in Excel's two columns (None, Hair, Dotted, Dash-dot-dot,
      Dash-dot, Dashed, Thin; Medium Dash-dot-dot, Slanted Dash-dot, Medium Dash-dot, Medium Dashed,
      Medium, Thick, Double), Thin selected on opening; presets None, Outline and Inside, Inside and
      the Horizontal and Vertical buttons disabled for one cell; no diagonal buttons.
- [x] Parts that differ across the Selection as case 24 showed: the Font style empty, a differing
      Fill as No Colour, a differing inside edge as a grey dotted line.
- [x] The palette is case 23's: Automatic, the Office theme's 10 colours with 5 tints each, the 10
      standard colours, More Colours. Each swatch records the RGB value Excel names it with in the
      Fill tab (in `cell-format.jsonl`), not the sampled pixel; the two "Grey" tints are `#7F7F7F`.
      The Fill tab offers No Colour instead of Automatic.


## Comments

*(2026-10-01, built.)* Format Cells, its seam, the built-in Chrome, and the core's item 16.

- **The core: a Consumer's popover (ADR-0050 item 16, DC-60).** It is off until the Consumer
  opens one, so a grid that never does is unchanged (DC-1).
  - `ExGrid.OpenPopoverAsync(RenderFragment<GridPopoverContext> content, string label)` shows the
    contents in the grid's popover frame, a `role="dialog"` named by `label`. It answers false
    while an edit is open and once the grid is disposed. `ClosePopoverAsync()` closes it, and only
    it. `ReturnKeyboardAsync()` is the core's focus function for a Chrome whose frame lies outside
    the grid: the root's hand-back, granted only while DOM focus is inside the root or on nothing.
  - `GridPopoverContext(Action Close, int FocusRequest, int FocusLastRequest, Action<bool> InnerPopupChanged)`.
    The contents take DOM focus themselves on the counts, with Blazor's `FocusAsync` (ADR-0039).
  - The frame stands under the header band, centred across the Viewport, as wide as its contents
    and never wider than the Viewport, bounded below by the Viewport (ADR-0040). It is one popover
    among the others: opening it closes any that stands, and a column's popover, the Context Menu
    or Find closes it. Escape, a press on the rows, the headings or into the Formula Bar, and the
    box shrinking below one row each close it, and the keyboard returns to the root.
  - Tab and Shift+Tab wrap inside it through the core's sentinels. The capture listener's hold
    behind a sentinel now waits for the keyboard to reach `.ex-popover-body`, a class the find
    panel's body wears too (one selector in `ex-grid.js`; no new use of script).
- **The seam (SH-45).** ExSheet decides what is offered and what OK means; the Chrome chooses the
  frame.
  - `ISheetChrome : IGridChrome` adds `RenderFragment? FormatCells(FormatCellsContext context)`.
    A fragment is the Chrome's own frame, rendered beside the grid until OK or Cancel; null is the
    built-in Format Cells in the grid's popover frame. `ExSheet.Chrome` stays an `IGridChrome`.
  - `FormatCellsContext(FormatCellsDraft Draft, FormatCellsTab Tab, Action<FormatCellsTab> TabShown, Func<Task<bool>> Ok, Action Cancel, Func<Task> ReturnKeyboard)`.
  - `FormatCellsDraft` holds what Format Cells opens on and records what is touched; `Change` is
    the `CellFormatChange` of the touched parts, and `Refusal` names a Custom code the engine does
    not read, or More Colours text that is not a colour. `Ok` refuses while a refusal stands,
    sets nothing for an empty change, and otherwise sets the change on the Selection through
    `FormatSelectionAsync`, as one undo step. Cancel sets nothing.
  - `FormatCellsOffer` lists the tabs, the categories (Accounting, Fraction and Special disabled,
    each with its reason), the alignments, the font styles, the line styles in Excel's two
    columns, the presets, the edges, and case 23's palette with the Fill tab's named RGB values.
  - What differs across the Selection is read by a new engine read,
    `Sheet.GetCellFormats(CellRange)`: the distinct Cell Formats a range shows, from what is
    recorded, never cell by cell, so whole columns open as fast as one cell.
- **What opens it.** The Context Menu's "Format Cells…" (`SheetCommandIds.FormatCells`), and
  `public Task<bool> OpenFormatCellsAsync()`, refused by name while an edit is open (SH-43). It
  answers false with nothing selected. A Selection moved under it, by the Name Box or a Consumer,
  ends it. **Ctrl+1 is not claimed here**: it is wired to the same opening after ticket 51 merges.
- **The built-in Chrome** is `FormatCellsPanel`, with ExSheet's stylesheet `ex-sheet.css`
  (`_content/ExSheet/ex-sheet.css`, linked by both hosts). The tabs are ARIA's tabs pattern, each
  list a group of native radio buttons, Enter in a field is OK, and the typed fields are bound.
  No script is added.
- **The DemoHost's `/sheet`** gains a "Format Cells…" button, and `?box=window`, a Sheet whose
  height follows the window, for the shrink test.
- **Tests.** Layer 1 and 2: ExGrid.Tests 826, ExSheet.Engine.Tests 2087 (6 new,
  `CellFormatSpreadTests`), ExGrid.MudBlazor.Tests 88, ExGrid.Components 1081 and one skipped (19
  new, `ConsumerPopoverTests`, DC-60), and ExSheet.Components.Tests 360 (50 new: `FormatCellsTests`
  and `FormatCellsDraftTests`, SH-45), all passing. Layer 3: `format-cells.spec.mjs`, 9 tests,
  passed on both hosts in Chrome, headless on a Mac. It covers opening from the Context Menu and from
  the page's button, the tabs by arrow key, OK as one undo step, Escape, a refused Custom code, a code
  typed at full speed, Tab's wrap, case 24, the shrink below one row, and two Sheets. The specs it
  touches, `sheet.spec.mjs --grep SH-29` and `find.spec.mjs`, passed on both hosts. The full run is
  CI's.
- **Left open.**
  - Ctrl+1 is the orchestrator's to wire once ticket 51 is merged, which is why the "What opens it"
    box stays unticked.
  - Case 24's own setup reads differently after ticket 55. A1 holds a thick bottom and A2 is
    plain, and Excel draws their inside edge grey and dotted. On this branch A2's top records
    nothing, so the edge differs and is drawn so. Under ticket 55's one shared line, A2's top
    records the same thick line, and the edge then reads as one thick line, unlike Excel's
    dialog. The tests here use A1:A3, which differs under either reading.
