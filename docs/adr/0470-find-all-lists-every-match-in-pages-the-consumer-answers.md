# Find All lists every match, in pages the Consumer answers

*Proposed 2026-11-03, for 0.2.0: the user put Find All in the release on 2026-11-03, which is the trigger
[§21.11 of the Definition of Done](../definition-of-done.md) left unnamed. Three questions are the user's, and
are asked under "The choices" below; the text states the recommended answer to each until the user answers.*

## Context

[ADR-0055](./0055-find-is-asked-of-the-consumer-like-sort-and-filter.md) gave every grid a Find that moves the
Focus to the next match, and left **Find All**, Excel's list of every match, undecided: "Find All needs a result
list the Consumer pages". [ADR-0332](./0332-find-reaches-every-product-replace-is-a-write-asked-of-the-consumer-and-the-matches-in-view-are-marked.md)
added Replace and the Match Mark and kept Find All out; the Match Mark marks only the painted cells and counts
nothing, because only the painted rows are known.

Excel's Find All lists, under the dialog, one line per matching cell (Book, Sheet, Name, Cell, Value, Formula),
says "*n* cell(s) found", and moves the active cell to a line chosen from the list. The list is the only place
Excel tells a user how many cells match.

The grid holds no rows (ADR-0001). A list of every match is therefore an answer only the Consumer can give, and it
is unbounded: a search for `1` over 10⁶ rows of twenty columns may match millions of cells. It cannot cross a
circuit or a network in one answer, and it must not be cut short quietly (the spine's first rule): a list that
stops at a thousand and says nothing is the wrong answer ADR-0055 was written against.

## Decision

### 1. Find All is a button in the find panel, wherever Find is answered

- **The panel gains Find All** beside Find previous and Find next, in every product whose Find is answered: ExGrid
  where its Consumer answers it (§2), ExSheet over its cells, ExPivot over its report as shown. No key is claimed
  for it; Excel has none beyond the dialog's access key.
- **What Find All searches is what a step searches**: the text and the two options, the selection as the scope
  when more than one cell is selected, the visible columns in the current order, and, outside a selected scope,
  the headers (ADR-0333). The order of the list is a forward walk's from the start: the header rows first, then the
  data rows, by rows. The Focus is not where the list starts; the list is the whole set.
- **Where the Consumer answers Find but not Find All, the button is not drawn**, as Replace is not offered where it
  cannot land (ADR-0332 §2). A Find All composed from repeated steps is refused as a design: it is one round trip
  per match.

### 2. The Consumer answers in pages, and says the exact count

The grid asks a **Find All Request**: Find's pattern, the scope, the columns, the Row Sequence Version, and the
**range of the list** it wants (the first entry and how many). The Consumer answers a **Find All Page**:

| Field | Meaning |
|---|---|
| `total` | How many cells match, exactly. Never an estimate and never capped |
| `matches` | The entries of the range asked: each a row in the order read, a column name and the cell's displayed text |
| `rowSequenceVersion` | The order the rows were read in |

- **The count is exact and uncapped** (the spine's fifth rule: listing is cheap per page, so nothing is capped).
  A Consumer that cannot count its matches cannot answer Find All, and leaves it unwired.
- **The grid asks for the range the list shows**, as the grid asks for the Window (ADR-0001): the first page when
  Find All is pressed, then each range the list is scrolled to, with a read-ahead. A page asked while another is out
  waits its turn; a new Find All cancels every page out, as a new step cancels the last (ADR-0025).
- **An answer is held to the request.** A `total` that moves between pages of one list, a match outside the scope
  or outside the request's columns, or more matches than the range asked, is the Consumer's defect and is raised
  by name, as ADR-0055's review section raises an answer outside the request.
- **Where it comes from**, as Find's: `GridSource.from` answers it by the reference rule (`findAll` over the rows,
  the counterpart of `findStep`); `GridSource.fetch` takes an optional `findAll` delegate; a push-mode Consumer passes
  `onFindAll`; a Blazor Consumer's `IGridSource` gains `FindAllAsync` with a default of "cannot", and the push mode
  `OnFindAll`. `OnFindAll` beside a bound Source is refused by name, as `OnFind` is.

### 3. The list is drawn by the core, under the panel's contents, in every Chrome

*(The first of the choices below.)*

- **The core draws the list** inside the find panel's popover, below the contents a Chrome slot draws, in the
  built-in Chrome and under every Wrapper (MudBlazor, Mantine, MUI). It is a virtualised listbox: a fixed row
  height taken from the Grid Metrics (the grid never measures, rule 2), a fixed number of visible lines, and only
  the lines in view in the DOM, so a list of millions holds a few dozen elements.
- **Its look is Visual Tokens** (ADR-0027, listed in ADR-0029), so a Wrapper styles it without drawing it. A Chrome
  slot's contents gain only the count line's words and the Find All button, through `GridFindPanelSlotContext`.
- **The count line** says "*n* cells found" in the panel's words (`findPanelLabelIds`), in the live region the
  outcome already uses (ADR-0033). "No cells found" for none.
- **Each line shows where the match is and its displayed text.** Where it is: in ExGrid, the column's header text
  and the row (the second of the choices); in ExSheet, the Sheet's name and the cell's address (`Sheet2!B12`);
  in ExPivot, as ExGrid. A header match says it is in the header (ADR-0333's "Found in the header").

### 4. Choosing a line moves the Focus there, as a step that found it would

- **A click on a line, or Enter on it, moves the Focus to that cell and reveals it**, exactly as a step that found it
  does (ADR-0055): within a selected scope the selection stands, otherwise it collapses onto the cell. A header line
  makes that header the current match and the Focus stays (ADR-0333 §1). In ExSheet with Within: Workbook, a line on
  another Sheet shows that Sheet first (ADR-0332 §5).
- **The list's own keys:** ↑ and ↓ move through the lines and move the Focus with them, as Excel's list does;
  Home and End go to the first and the last line; the panel stays open. Tab leaves the list as it leaves any control
  in the panel (ADR-0039).
- **The chosen line is the current match**, so Find next and Find previous go on from it, and the Match Mark marks
  it apart.
- Every choice is an input in the Input Order (ADR-0240), carrying the Row Sequence Version the list was read in: a
  choice made under another version is refused as the order having changed, never applied to the row now at that
  place (the spine's sixth rule).

### 5. When the list goes away

- **The list is dropped, and the panel says so, when the Row Sequence Version moves** (a sort, a filter, a column
  reorder, rows added or removed), as the selection is dropped on reorder (ADR-0011). It is not run again by itself:
  a Find All may be costly, and the user presses it again.
- **It is dropped when the text or an option changes**, so the list never shows the matches of a search the field
  no longer holds, and when the panel closes.
- **A value that changes without moving the version leaves the entries already shown as they were found**; a page
  read later reads the values as they now stand. Excel's list behaves the same way. Choosing a line moves the Focus by
  position, which the version still vouches for.

### 6. Not in this version

- **Selecting every match** (Excel's Ctrl+A in the list) *(the third of the choices)*.
- **Sorting the list by a column of its own**, which Excel offers by clicking the list's headers. The list's order is
  the walk's order.
- **Find All over a Replace**: Replace and Replace All keep ADR-0332's rules, and a Replace All drops the list, as
  its writes may move what matches.

## The choices

These are the user's. Each is stated with the option the text above takes until the user answers.

1. **Who draws the list** (§3). *Recommended:* the core draws it under every Chrome, styled by Visual Tokens, so its
   paging, virtualisation and keys are written once and behave alike under every Wrapper (the spine's fourth rule).
   *Against it:* each Wrapper (MudBlazor, Mantine, MUI) draws the list with its kit's own components, which looks
   more native but writes the virtualised, paged list four times and lets them drift.
2. **How a line names the row in ExGrid and ExPivot** (§3). *Recommended:* by its position in the current order,
   "Row 1,234", with an optional `findAllRowLabel` a Consumer passes to name it by its own text instead (a trade's
   id, say). *Against:* the position alone (meaningless where the grid shows no row numbers), or a label always
   required (Find All would not work for a Consumer that does not write one).
3. **Selecting every match** (§6). *Recommended:* left out of 0.2.0 and reserved, because the Selection would need
   every match at once, not a page, and the gestures that act on a Selection of scattered single cells (copy refuses
   it, ADR-0005) give the user little for it. *Against:* included, as Excel has it, with the Consumer asked for every
   match's position once, uncapped.

## Consequences

- New public types in `@ex-grid/core`: `GridFindAllRequest`, `GridFindAllPage`, `GridFindAllMatch`; `findAll` (the
  reference) beside `findStep`; `GridSource.find` gains a sibling `findAll`; `GridSource.fetch` and `createGrid` take
  `findAll` and `onFindAll`; `GridFindPanelSlotContext` and `FindPanelView` gain Find All and the count. React and
  Blazor adapters carry them; .NET's `IGridSource` gains `FindAllAsync`, `InMemoryGridSource` and `GridSource.Fetch`
  answer it, and the component gains `OnFindAll`.
- ExSheet answers Find All over the Sheet or the Workbook, in its own panel (ADR-0332 §5); ExPivot over its report as
  shown, reading the report source in blocks as its step does.
- `CONTEXT.md` gains **Find All**; ADR-0055's "Not decided here" and §21.11 of the Definition of Done lose Find All,
  and the Definition of Done gains its criteria.
- Each product's Docs Site page says what Find All does, with an Example; a Change Note for each product.
