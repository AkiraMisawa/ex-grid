# 54: The Sheet Toolbar, shipped in ExSheet and ExSheet.MudBlazor

Status: ready-for-agent

**What to build:** the Sheet Toolbar of [ADR-0100](../../../adr/0100-the-sheet-toolbar-ships-as-an-opt-in-part-of-exsheet.md),
which replaces ADR-0071's "A toolbar is a sample, not a product". It is an opt-in part of `ExSheet`,
drawn by the Chrome, and built only on public commands. *(Until 2026-10-02 this ticket built the
toolbar as a DemoHost sample. The file keeps its name so links to it hold.)*

**Blocked by:** None (48, 50 and 52 are done)

- [ ] **`ShowToolbar`** (default `false`) and **`ToolbarContent`** on `ExSheet` (SH-48).
  - It stands above the Formula Bar, inside the Sheet's box and outside ExGrid's instance root.
  - The Sheet's `Height` includes it. Every Toolbar Row is one height, resolved in C# from the
    Density as the Formula Bar's is. No stylesheet value sets it.
- [ ] **`ToolbarRow` and the Toolbar Items** as child components, laid out in markup order (SH-49).
  - The default row, when `ToolbarContent` is absent: Bold, Italic, Underline, Strikethrough, Font
    colour, Fill, Borders, Left, Center, Right, Number Format, Percent, Comma, Format Cells….
  - A Consumer's own item, `ToolbarButton`, takes an `OnClick` and can decline giving the keyboard
    back.
  - A layer-1 test finds no internal member referenced from an item type.
- [ ] **Each item's state and behaviour** (SH-50).
  - The state comes from `CellFormatAt` of the Focus cell, whenever the Selection changes.
  - A press acts through the public commands (ticket 56's Selection).
  - Every item is unavailable while an edit is open, through `EditingChanged`.
  - A pointer press keeps DOM focus on the Sheet. The toolbar is one Tab stop, with ← and → inside.
- [ ] **Both Chromes draw the items.**
  - The built-in Chrome uses inline-SVG icons, coloured by `currentColor` and Visual Tokens.
  - `ExSheet.MudBlazor` uses `MudToggleIconButton`, `MudIconButton` and `MudMenu`.
  - Dropdowns take Format Cells' frames (ADR-0071).
  - Tooltips give the name and the key, through `SheetWords`.
- [ ] **`/sheet` and its MudBlazor twin** show the toolbar. It replaces the page's "Format selection
      as #,##0.00" button, and the page carries one Consumer row (an action) to show the slot.
- [ ] **Layer 3** on both hosts under both Chromes, with a clean console (SH-18):
  - a press straight after Shift+ArrowDown on the Server host;
  - Bold's pressed state following the Focus;
  - the toolbar greyed during an edit;
  - the keyboard still on the Sheet after a press.

KeyTips and Ctrl+F1 are ticket 160.

## Comments

*(2026-10-01, orchestrator.)* **On hold, to be decided with the user.** The user raised the idea of shipping the
toolbar as a component rather than as a DemoHost sample: an opt-in part in `ExSheet` and in
`ExSheet.MudBlazor`, built only on public commands. That would change ADR-0071's "A toolbar is a sample, not a
product". The user will grill it after `claude/exsheet-cell-format` merges. Until then nothing here is built.

*(2026-10-02, grilled with the user.)* **Decided: shipped, as ADR-0100.** One toolbar per Sheet, part
of the Sheet, opt-in. Its contents are declared as child components in markup order, with a Consumer
row allowed. The Chrome draws the items. Everything is greyed during an edit. KeyTips and Ctrl+F1 are
split into ticket 160. `Status` moves from `needs-triage` to `ready-for-agent`.
