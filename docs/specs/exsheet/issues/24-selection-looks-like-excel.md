# 24: The Focus and a single range look like Excel's, and the header's rule is whole

Status: ready-for-agent

**What to build:** ADR-0008, "Excel's look for the Focus and a single range" (2026-09-29), and
ADR-0030's change of the same day, for ExGrid as a whole. Found on `/sheet`: the active cell's left
edge was half as thick as its other three, a selected range had no outline, and the header's rule
stopped short of column A.

**Blocked by:** None (can start immediately)

Core (ExGrid):

- [ ] The Focus outline lies wholly inside its cell, so no layer above the selection layer covers
      part of it: beside a Pinned Column, beside the Headings, under the header (UX-18)
- [ ] The fill handle stays centred on the outline's corner; the forced-colors outline of a range is
      drawn inside as well
- [ ] The Focus cell is never tinted: a range holding the Focus is painted with a hole there, as
      geometry resolved in C# and emitted inline, and the range stays one element (UX-19)
- [ ] A selection of one range shows one outline around the whole range and none around the Focus
      inside it; a one-cell selection shows the Focus outline alone; several ranges are each tinted
      with no outline, and the Focus cell among them untinted and outlined (UX-19)
- [ ] The header's rule runs under a Pinned Column's header and under the Headings' corner (UX-17)
- [ ] Every one of these reads `--ex-focus-outline` or `--ex-header-rule-color`; no token is added
      (ADR-0029)
- [ ] The hole adds nothing measurable to a drag: measured before it is claimed (ADR-0008)

ExGrid.MudBlazor:

- [ ] `--ex-focus-outline` is the palette's primary in the light scheme, and in the dark scheme the
      primary with its lightness raised through relative colour syntax (ADR-0030, 2026-09-29)
- [ ] UX-9 measures both schemes; if the dark one does not clear 3:1, it keeps the ink colour and
      ADR-0030's paragraph says so

## To observe on the next Windows run

Carry these into the next `verify-on-windows` procedure. They are read, not observed, and ADR-0008
waits on them:

1. Excel, a single range dragged from A1 to C5: is there any outline around A1 itself, apart from
   the range's outline and the untinted cell?
2. Excel, a selection made of two ranges with Ctrl+click (A1:B2, then D4:E6): which of them carries
   an outline, if any; does the active cell carry a border; where is the fill handle, if any?
3. Excel, a whole column and a whole row selected from the Headings: the same questions as 1.

Screenshots of each, beside ExSheet showing the same selection.
