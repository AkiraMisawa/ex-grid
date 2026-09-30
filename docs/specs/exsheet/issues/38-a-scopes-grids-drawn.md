# 38: What a Scope's grids draw while a Sheet points

Status: done

**What to build:** ADR-0058, "What is drawn", and ADR-0057's note of 2026-09-30. The Scope wires the
structured-reference outlines into its grids itself. It dashes the pressed cell or column, and the
written `XLOOKUP(...)` lies on the grey ground as a whole.

**Blocked by:** Ticket 37

- [x] While a Formula is edited in a Sheet of the Scope, the Linked Table columns it reads are
      outlined in its registered grids in their colours (ExSheet's `OnLinkedColumnColoursChanged`,
      mapped through the correspondence), with no `OutlinedColumns` written by the page (SH-34)
- [x] `/sheet` drops `OutlinePositions` and `/sheets` drops `OutlineLeft`/`OutlineRight`: their
      Scopes do it now, and SH-31's layer 3 test still passes (SH-34, SH-31)
- [x] A pressed cell: dashes over it (ticket 34's request), found by the row's key, following the row
      through a sort; not drawn while the row is not painted, never scrolled to (SH-34, DC-53)
- [x] A pressed column header: dashes over the column's body (SH-34)
- [x] The dashes go when Point ends (an operator typed, the caret moved, a commit, a cancel); the
      column outlines stay until the edit ends (SH-34)
- [x] The written `XLOOKUP(...)` is the pointed span: it lies on the grey ground as a whole, unless it
      follows the `=` directly, and the two column references in it wear their colours' darker shades
      (`PointedSpan` and `AddReferenceSpans` in `ExGrid.ReferenceText.cs`, which today match one
      Reference exactly) (SH-34)
- [x] Layer 2; Layer 3 on both hosts: `=SUM(1,`, a press on a PV cell; the Id and PV columns outlined
      in the colours their text wears; the dashes on the pressed cell, and on the same row after the
      positions grid is sorted; the grey under the whole `XLOOKUP(...)` (SH-34)

## Comments

2026-09-30, implemented on `agent/pointing-scope-38`.

- **The outlines.** ExSheet tells its Scope the linked columns whenever it tells its Consumer
  (`OnLinkedColumnColoursChanged` still fires, for a grid in no Scope), and on joining a Scope while an
  edit is open. The Scope keeps each Sheet's list, the latest told last, and sets each registered
  grid's `GridPointedAt.OutlinedColumns`: the told columns of the grid's table (matched without regard
  to case), each over the grid columns the registration makes it — every grid column whose entry names
  it, and the grid column of its own name unless that one has an entry naming another. A new list is
  set only when its entries differ. A Sheet that leaves the Scope takes its outlines with it. `/sheet`
  and `/sheets` no longer wire `OutlinedColumns` or `OnLinkedColumnColoursChanged`.
- **A case the correspondence cannot see.** The Scope does not know a grid's column names, so a grid
  column that is its table column by name is outlined only when named exactly as the Sheet declares
  the column, while a press on it finds the column without regard to case (ticket 37). Documented on
  `RegisterGrid`'s `tableColumns` and in `src/ExSheet/README.md`; an entry in `tableColumns` covers any
  other casing.
- **The dashes.** A press that writes sets `GridPointedAt.Dashes`: `OverColumn` for a header, and for a
  cell `OverCell` by a function that reads each painted row through `tableRow` and compares its key
  with the written one as `XLOOKUP`'s exact match does — the engine gains
  `FormulaEntry.LookupFinds(Value key, Value? candidate)` for it — so the dashes follow the row through
  a sort and a Window of new instances, are drawn nowhere while it is not painted, and never scroll.
  They go when the Sheet's grid tells a Point state other than `WrittenFromOutside` (ExSheet forwards
  `OnPointStateChanged` to the Scope, whether or not it holds the keyboard): an operator typed, the
  caret moved, a press on the Sheet, F2, a commit or a cancel. The keyboard leaving the Sheet does not
  end Point, so the dashes stay, which is what lets a sort be seen to carry them. A further press
  moves them, from one registered grid to another too.
- **A drag's take-back** returns the text to what it was before its press, and the dashes to what
  they were with that text: none after `=SUM(`, the earlier press's dashes after a chain of presses. The
  ADR says "the dashes go"; restoring the earlier press's dashes along with its text is read as the
  same rule, since the grid's take-back also restores the Sheet's own pointing outline.
- **The grey.** `AddReferenceSpans` draws a pointed span that is exactly one Reference as before (one
  span, `ex-reference-N ex-reference-pointed`), and anything longer as one `span.ex-reference-pointed`
  with the References inside it in their own spans. A pointed span that cuts through a Reference wears
  no look, as before. The stylesheet declares the darker shade on the inner spans too, so each
  `currentColor` is its own Reference's rather than the inherited shade of the text around it
  (layer 3 checks that the two inks differ).
- **`/sheet`** shows its positions through `GridSource.From`, so a header click sorts it while the
  Sheet does not point; Revalue replaces each row in the source.
- **Tests.** Layer 1: `PointedTextTests` (+1, `LookupFinds`). Layer 2: `PointingScopeTests` in a new
  partial file, `PointingScopeDrawingTests.cs` (+12: the outlines through the correspondence and only in
  the Scope, until the edit ends; two Sheets' outlines; the dashes by key through a sort, new instances
  and an unpainted row, with no scroll; a header's dashes; the dashes going for an operator, the caret,
  Enter, Escape and a press on the Sheet; a further press and a drag's take-back; the grey as a whole,
  one Reference, and after `=`), and `ReferenceTextTests` (+5). `ScopedSheets` can show the registered
  grid other rows. Layer 3, `pointing-scope.spec.mjs` (+3), headless on macOS, chrome, both hosts: the
  ticket's case (outline colours equal to the text's, the dashes on C3 and then on C4 after a sort by
  PV, the grey under the whole lookup with the inner inks darker and distinct), a header's dashes and
  an operator, and a lookup after `=` then Enter. SH-31's tests in `declarations.spec.mjs` and
  `sheets.spec.mjs` pass with the pages' wiring gone.

