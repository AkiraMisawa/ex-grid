# 54: A formatting toolbar on the DemoHost

Status: needs-triage

**What to build:** the sample of [ADR-0071](../../../adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md), "A toolbar is a sample, not a product". It is built only on
public commands.

**Blocked by:** 48 and 50 (52 for its Format Cells button)

- [ ] **A toolbar on `/sheet`** and on its MudBlazor twin.
  - Buttons: bold, italic, underline, strikethrough, Font colour, Fill, the border presets, a few
    Number Formats, the Alignment, and Format Cells….
  - Each button's state comes from `CellFormatAt` of the Focus cell, whenever the Selection changes.
  - The toolbar greys out while an edit is open, through the existing notification.
- [ ] It replaces the page's "Format selection as #,##0.00" button.
- [ ] Layer 3: the page on both hosts, with a clean console (SH-18).

## Comments

*(2026-10-01, orchestrator.)* **On hold, to be decided with the user.** The user raised the idea of shipping the
toolbar as a component rather than as a DemoHost sample: an opt-in part in `ExSheet` and in
`ExSheet.MudBlazor`, built only on public commands. That would change ADR-0071's "A toolbar is a sample, not a
product". The user will grill it after `claude/exsheet-cell-format` merges. Until then nothing here is built.
