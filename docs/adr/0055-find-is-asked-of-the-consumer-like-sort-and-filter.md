# Find is asked of the Consumer, like sort and filter, and the grid answers Ctrl+F itself

*(Renumbered 2026-09-28 when `main` was merged with the ExSheet branch, which had taken 0046–0053. It was ADR-0047 on `main`.)*

Excel's Ctrl+F finds a value anywhere in the sheet. In ExGrid the key reached the browser, whose
find searches the page — which holds only the rows the grid has painted. It would report "no
match" for a value a few hundred rows further down, or stop at a match it happened to have
painted while missing the one before it. **That is the quiet wrong answer the spine refuses**:
a search that looks complete and is not.

**Decision: while the grid holds DOM focus, it claims Ctrl+F. Where a search has been wired, it
opens the find panel, a Chrome seam, and asks the Consumer where the next match is; where none has
been wired, it refuses and says so.** The grid does not search, because it does not hold the data
([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)) — exactly as it does not
sort or filter. `GridSource.From` is the reference implementation of what a match is, as it is
for filtering ([ADR-0023](./0023-filter-and-sort-semantics-of-the-reference-implementation.md)).

## What is asked, and what comes back

One request per step, `GridFindRequest`:

| Field | Meaning |
|---|---|
| `Text` | What to look for. Never empty — an empty field asks nothing |
| `MatchCase` | Off: `OrdinalIgnoreCase`, ADR-0023's rule. On: `Ordinal` |
| `WholeCell` | Off: the cell's text contains `Text`. On: it equals it |
| `Backward` | Shift+Enter, "previous" |
| `From` | The Focus; the search starts at the cell after it (before it, backward). None: the start (the end) |
| `Scope` | The selected ranges when more than one cell is selected — Excel's "search the selection". None: every row |
| `Columns` | The column names in the grid's current order, visible ones only. The Consumer does not know the order the user dragged, or which columns are hidden |
| `RowSequenceVersion` | The order the positions above were read in ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)) |

**The order searched is Excel's default, by rows:** row by row, and across a row in the order of
`Columns`, starting after `From` and wrapping past the end to the start. The cell `From` names is
therefore the *last* one considered, and a lone match finds itself — as in Excel. A cell covered
by two ranges of the scope counts once.

**What is matched is the displayed text** — the column's format applied, the text a user reads —
not the raw value. That is Excel's default ("look in: values"), and it is the only form the user
can type: nobody searches for `1234.5` in a column that shows `1,234.50`. The cost is that the
Consumer must format exactly as the grid does, so **the column's text function travels to a Grid
Source in `ColumnInfo`**, and `InMemoryGridSource` matches against that function rather than
re-deriving it. A remote Source that formats on the server has the same obligation, and the
reference implementation is the test it is held to.

The answer, `GridFindResult`, is **a position — a row in the current order and a column name —
or "not found"**. The grid then:

- **drops an answer whose version is not the current one**, and refuses as `OrderChanged`: the
  order moved while the question was out, and a position read under the old order would land on
  a different row (ADR-0011);
- **otherwise moves the Focus there and reveals it.** With a scope, the Focus moves inside the
  selection and the selection stands, so the next step searches the same ranges; without one, the
  selection collapses onto the found cell. A row outside the Window is reached by the ordinary
  Range Request that revealing it raises;
- **on "not found", refuses as `NotFound`**, and the panel shows it.

A step asked while one is out cancels the one that is out, whose answer is discarded — the fetching
Source's rule ([ADR-0025](./0025-what-the-bundled-fetching-source-promises.md)).

## Where the search comes from

- **A Grid Source answers it** through `IGridSource.FindAsync`, advertised by `CanFind`. The
  interface's default is "cannot", so an existing Source keeps compiling and is honestly reported
  as having no search. `InMemoryGridSource` implements it; `GridSource.Fetch` takes an optional
  `find` delegate and answers with it.
- **A push-mode Consumer answers it** through the `OnFind` parameter.
- **Nobody answers it**: Ctrl+F is still claimed, opens nothing, and raises `OnFindRefused` with
  `Unavailable`. Letting the key through would hand the user the browser's incomplete search on
  exactly the grids where nothing better exists.

## The panel

**A Chrome seam, `IGridChrome.FindPanel(FindContext)`**, drawn by the built-in Chrome and by each
Wrapper ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md),
[ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)). It is a popover
and follows every popover rule: it takes the keyboard through its context's `FocusRequest`
([ADR-0039](./0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md)), stays inside
the grid's box ([ADR-0040](./0040-a-popover-stays-inside-its-grids-box.md)), closes on Escape or on a
pointer-down elsewhere in the instance, and returns DOM focus to the root when it closes. Enter in its field is "next", Shift+Enter "previous". The
context carries the field's text, the two options and the last outcome as an enum — the grid holds
no strings, so "not found" is worded by Chrome, into a live region
([ADR-0033](./0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md)).

The panel stays open across steps, and the text survives closing and reopening within the
instance, as Excel's dialog keeps its last search.

## What the key does elsewhere

- **In the Cell Editor, Ctrl+F is taken and does nothing** — Excel's Find is disabled while a cell
  is being edited, and handing the key to the browser would open the incomplete search.
- **In a Consumer's control inside a Template cell, the key is the control's**, as every key is
  there (ADR-0020/0037).
- **Outside the grid, the browser's find is the browser's.** The listener is on the instance root,
  not `document` ([ADR-0018](./0018-multiple-instances-must-be-independent.md)), so the grid cannot
  see a Ctrl+F pressed while it does not hold focus, and its painted rows are all that search will
  find. **This is a known limit, not an oversight:** fixing it needs a document-level listener,
  which ADR-0018 rejected for making every grid on a page answer every key.

## Not decided here, deliberately

- **Find All** (Excel's list of every match) and **Replace** (Ctrl+H). Replace is a bulk write —
  an Edit Intent over an unbounded, non-rectangular set of cells — and needs its own decision about
  refusal, caps and undo. Find All needs a result list the Consumer pages. Neither is claimed.
- **Search by columns, in formulas, in comments, by format.** Excel's other options have no
  counterpart in a grid that holds no formulas and no formats of its own.

## Consequences

- New public types: `GridFindRequest`, `GridFindResult`, `FindContext`, `FindRefusalReason`,
  `FindOutcome`; new parameters `OnFind` and `OnFindRefused`; `IGridSource` gains `CanFind` and
  `FindAsync` with defaults; `ColumnInfo` gains the column's text function; `GridSource.Fetch`
  gains `find`.
- **The glossary gains Find.**
- **Ctrl+F is no longer the browser's while the grid is focused**, on every grid. A Consumer that
  wants the browser's search back has no switch for it, on purpose: that search is the wrong
  answer for a virtualised grid.

## Settled in review — 2026-09-27

*(A review of the first implementation found three cases this ADR had left open, and one
mechanism that did not hold. Each was decided with the user; the text above is unchanged, and
this section overrides it where the two differ.)*

**Ctrl+F inside the grid's own popovers is the grid's too.** The gate had followed ADR-0039's rule
that a popover's contents own every key but Escape, so Ctrl+F pressed in the find field — the
natural way back to it — opened the browser's search, which FD-1 forbids. Now:

- in the find panel's field, Ctrl+F selects the field's text, as Excel's dialog does. It is done
  by the key listener itself, as the held-key replay already types into a field (ADR-0010): the
  selection is the field's own behaviour, not a meaning the grid gives the key, and the core is
  not told;
- in any other of the grid's popovers — a column's, the Context Menu — Ctrl+F closes that popover
  and opens the find panel, exactly as it would from the root;
- in a Consumer's control inside a Template cell the key stays the control's, as before.

**`OnFind` beside a bound Source is refused by name**, as `Window` beside `Source` is
([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md), FN-2). `OnFind` is the push
mode's answer; with a Source bound there would be two answers to one question, and silently
preferring either is the quiet choice the spine refuses.

**An answer outside the request is the Consumer's defect, raised by name.** A column name that is
not among the request's `Columns`, or a row outside the result the grid holds, cannot be the
answer to the question asked, so the grid throws, naming the request — it does not reword a bug as
"the rows were reordered". `OrderChanged` keeps exactly one meaning: **the order, or the visible
columns, moved while the step was out** — the version differs, or a column the request named has
since left the grid.

**The column's format travels in `ColumnInfo`, not a composed text function.** A closure built per
column made `ColumnInfo`'s record equality fail for every rebuilt column, so a Consumer that
rebuilds its column array made `InMemoryGridSource` requery on every repush — its documented no-op
lost. `ColumnInfo` carries `Format`, compared by delegate identity like `Value`, and answers the
displayed text itself (`TextOf`), which is also the one place the rule is written: the row paints
by it and a Source matches by it.

The public surface this ADR adds is therefore also `GridFind` (the reference step), `FindPanelLabelIds`
(the built-in panel's words), `GridSelection.FocusOn`, and `GridKeyClaims.CanFind` beside the
other key claims.

*(2026-09-28, third Windows run, decided with the user.)* Excel's Find scrolls a found cell to the
middle of the view; ExSheet's reveals it as every Focus move does, as far as it must, so a cell below
the view lands on its bottom row. **Kept**: FD-5 asks that the cell be revealed, and it is, and
ExGrid keeps one reveal rule rather than a second one for Find.
