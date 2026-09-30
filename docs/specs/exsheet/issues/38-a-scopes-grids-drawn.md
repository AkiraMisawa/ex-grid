# 38: What a Scope's grids draw while a Sheet points

Status: ready-for-agent

**What to build:** ADR-0058, "What is drawn", and ADR-0057's note of 2026-09-30. The Scope wires the
structured-reference outlines into its grids itself. It dashes the pressed cell or column, and the
written `XLOOKUP(...)` lies on the grey ground as a whole.

**Blocked by:** Ticket 37

- [ ] While a Formula is edited in a Sheet of the Scope, the Linked Table columns it reads are
      outlined in its registered grids in their colours (ExSheet's `OnLinkedColumnColoursChanged`,
      mapped through the correspondence), with no `OutlinedColumns` written by the page (SH-34)
- [ ] A pressed cell: dashes over it (ticket 34's request), found by the row's key, following the row
      through a sort; not drawn while the row is not painted, never scrolled to (SH-34, DC-53)
- [ ] A pressed column header: dashes over the column's body (SH-34)
- [ ] The dashes go when Point ends (an operator typed, the caret moved, a commit, a cancel); the
      column outlines stay until the edit ends (SH-34)
- [ ] The written `XLOOKUP(...)` is the pointed span: it lies on the grey ground as a whole, unless it
      follows the `=` directly, and the two column references in it wear their colours' darker shades
      (`PointedSpan` and `AddReferenceSpans` in `ExGrid.ReferenceText.cs`, which today match one
      Reference exactly) (SH-34)
- [ ] Layer 2; Layer 3 on both hosts: `=SUM(1,`, a press on a PV cell; the Id and PV columns outlined
      in the colours their text wears; the dashes on the pressed cell, and on the same row after the
      positions grid is sorted; the grey under the whole `XLOOKUP(...)` (SH-34)
