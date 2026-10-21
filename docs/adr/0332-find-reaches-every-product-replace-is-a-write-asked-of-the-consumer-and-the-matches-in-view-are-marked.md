# Find reaches every product, Replace is a write asked of the Consumer, and the matches in view are marked

*(Decided with the user, 2026-10-20 and 2026-10-21, card by card, for `exsheet-workbook` ticket 10
and what it grew into. The user took the recommendation of every question except the one on where
ExSheet's Within goes, §5.)*

[ADR-0055](./0055-find-is-asked-of-the-consumer-like-sort-and-filter.md) gave ExGrid a Find asked of
the Consumer and left two things undecided: **Replace** ("a bulk write … needs its own decision about
refusal, caps and undo") and anything beyond moving the Focus. [ADR-0331](./0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md)
§8 then asked for Excel's **Within: Sheet / Workbook**. ExPivot has no Find at all: Ctrl+F over a
report does nothing. The user asked for Find in all three products, Replace wherever cells can be
written, and the matches on screen marked as other grids mark them.

## Decision

### 1. Find in every product

- **ExGrid** keeps ADR-0055's Find unchanged.
- **ExSheet** answers it over the Sheet shown, or over the Workbook (§5).
- **ExPivot** answers it over the report **as shown**: every cell that is laid out (row and column
  labels, values and totals) matched on its displayed text. **A collapsed group is not searched
  inside, and nothing is expanded to show a match**, as Excel's Find over a PivotTable reads only the
  cells on the sheet. A search never changes the report's shape.

### 2. Replace is offered where cells can be written: ExGrid and ExSheet

*(This replaces the decision of 2026-10-20 that Replace was ExSheet's only and that ExGrid gained no
seam for it. The user widened it on 2026-10-21.)*

- **Ctrl+H opens the panel with Replace**, as Excel's dialog has Find and Replace in one. It is offered
  only where a Replace can land: in ExGrid, when the Consumer handles the Replace Request (§3) and
  at least one visible column is Editable; in ExSheet, always. ExPivot has none: a report is not written.
  Where none is offered, Ctrl+H is refused and said, as Ctrl+F is where no search is wired.
- **What is matched for Replace is the cell's entry**: in ExSheet the Entry (a Formula's text, as
  Excel's Replace offers only "Formulas"); in ExGrid the text the Cell Editor would open with. Find
  keeps matching the displayed text (ADR-0055).
- **Replace** (one) replaces the current match and finds the next. It is an ordinary write of that one
  cell: an Edit Intent in ExGrid, an edit in ExSheet, with its Edit Verdict.
- **Replace All is one write over every match, all or nothing.** If any match cannot be written (a
  column not Editable, a Formula the replacement leaves unreadable, a place a Sheet refuses), nothing is
  replaced, and the notice names the cells and why. **No cap** (the spine's fifth rule: what can be
  executed is not capped). **One undo step**; the count replaced is said.
- Match case, Whole cell and the Selection as scope are Find's own options, carried over.

### 3. In ExGrid, Replace All is a request the Consumer answers

ExGrid holds no rows (ADR-0001), so only the Consumer can reach every match. **The grid raises a
Replace Request**, the find request's fields plus the replacement, under the Row Sequence Version it
was read in, and **the Consumer performs it and answers with the count or a refusal**. The grid writes
nothing itself. `GridSource.From` is the reference implementation, as it is for Find; ExSheet answers
over its cells. An answer under another version is refused as the order having moved (ADR-0011).

*Rejected:* replacing only the painted rows as a paste-shaped write (a match outside the Window would be
left unreplaced, silently).

### 4. The matches in view are marked, judged by a match function

- **While the panel holds a search, every match among the painted cells is marked**, and the current
  match apart from the rest; a match scrolled into view is marked as it is painted. Nothing is counted:
  only the painted rows are known, so no "n of m" is shown.
- **The grid judges the painted cells itself, with a match function**, so the mark follows the scroll
  with no round trip. **The default is ADR-0055's reference rule** (`GridFind`, the rule `GridSource.From`
  answers by). ExSheet passes its own, ExPivot the reference rule over its displayed text.
- **A Consumer that answers Find itself gets the mark only by passing a match function.** Its rule may
  not be the reference one, and a mark the next step does not reach would be quietly wrong; so without
  one, nothing is marked.
- The mark is painted like the Selection (ADR-0008): a class on the painted cells, a Visual Token for its
  colour (ADR-0027). Nothing per cell reaches JavaScript.

*Rejected:* asking the Consumer for the matches of every painted range (the mark would trail the scroll
on a circuit or a remote Source).

### 5. ExSheet's Within: Sheet or Workbook, in the panel ExSheet substitutes

- **ExSheet draws the find panel's contents itself**, through the Chrome seam ADR-0055 gave the panel,
  in its Built-in form and in its MudBlazor Wrapper, adding **Within: Sheet** (the default) **or
  Workbook**. ExGrid's panel is unchanged.
- With Workbook, a step past the last match of the Sheet shown goes on to the next Sheet in tab order;
  **a match on another Sheet shows that Sheet and selects the cell**. Replace All over the Workbook is
  one step on the undo stack.
- *Recommended against, and chosen:* a list of search places any Consumer declares, which ExGrid's own
  panel would show. The user preferred to keep ExGrid's panel as it is; the cost is a second panel
  ExSheet keeps in step with ExGrid's, in both Chromes.

## Consequences

- ExGrid gains the Replace Request and its answer, the match function and the mark's Visual Token;
  `GridSource.From` answers Replace. `CONTEXT.md` gains **Replace** and **Match Mark**.
- ExPivot answers Find, and passes a match function.
- ExSheet answers Find over the Workbook, answers Replace, and draws its own panel in both Chromes.
- ADR-0055's "Not decided here" loses Replace; Find All stays undecided.
- Each product's Docs Site page says what Find and Replace do there, with an Example.
