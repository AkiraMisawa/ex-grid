# 52: Format Cells: the seam, the built-in Chrome, and the Context Menu

Status: ready-for-agent

**What to build:** [ADR-0063](../../../adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md), "Format Cells, a Chrome seam whose frame the Chrome chooses", and
[ADR-0050](../../../adr/0050-what-exsheet-asks-of-exgrids-core.md) item 16.

**Blocked by:** 50, and the eleventh Windows run's Part A (group 5)

- [ ] **Core: a Consumer's popover in the grid's frame** (DC-60).
  - It stays inside the box and is bounded by it.
  - It takes and returns the keyboard.
  - It closes as a Cancel below one row.
- [ ] **ExSheet: the seam's contract** (SH-45).
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
- [ ] **The built-in Chrome.**
  - It is a popover through item 16.
  - More Colours takes a hex value.
  - The tabs switch with the arrow keys (ARIA's tabs pattern).
  - No script is added.
- [ ] **What opens it**: Ctrl+1 (with ticket 51), a "Format Cells…" Context Menu command, and
      `OpenFormatCellsAsync()`. Each is refused while an edit is open.
- [ ] Layer 2; Layer 3 on both hosts.
