# Run 17, draws and rename: proposals for the orchestrator

Area `draws-rename` (cases b1, b2, b3 of `verification/2026-10-11-windows-17/report.md`, Part B;
`records/part-b.jsonl`; `shots/part-b/b1-*`, `b2-*`, `b3-*`). The engine change, the tests, ticket 15's
comment and the Change Note are on branch `claude/run17-draws-rename`. What follows is the text a
background agent may not write: ADR-0180's notes, both READMEs, the Docs Site's Formulas page, SH-66 and
two sentences that now name an answered question. Each is given as the text to replace and the text to
put in its place.

## What the run observed, and what was done

| Case | Excel | ExSheet before | Done |
|---|---|---|---|
| b1 | Every Ctrl+Z and Ctrl+Y (two rounds) drew a number not shown before: never the draw before the edit nor the one after it | Draws again on each undo and redo | Agrees. `ADR0180_an_undo_draws_again` became `ADR0180_an_undo_and_a_redo_draw_again`: two rounds of undo and redo, every draw new, B1 empty and 1 in turn |
| b2 | The rename drew again; Excel offered its undo; Ctrl+Z put `Sheet1` back and drew again | A rename, its undo and its redo drew nothing (ADR-0180's first note of 2026-10-11) | **Followed**, as that note said it would. `CountChange()` in `Sheet.ApplyRename` and `Sheet.UndoRename`, once per operation as `_operationDepth` ensures; a rename to the Sheet's own name, and its undo and redo, change nothing. `ADR0180_a_rename_draws_nothing` became `ADR0180_a_rename_draws_again`; new `ADR0180_a_rename_to_the_same_name_draws_nothing`, three rename counts in `ADR0180_each_change_counts_once`, and `SeedWiringTests.ADR0180_a_rename_its_undo_and_its_redo_draw_again` |
| b3 | Each Refresh All drew again: twice with the file unchanged, once after it changed, once more unchanged | `PushLinkedTable` counts as a change whatever its rows (`Sheet.LinkedTables.cs`, "refused or taken") | Agrees. New `ADR0180_a_snapshot_draws_again_whatever_its_rows`, and a second push of the same rows in `ADR0180_each_change_counts_once`. The same declaration again still draws nothing |

## ADR-0180

**The list of changes** (under "The draws change with a change, and only then"). Replace

> - an edit of an Entry, and an insertion or a deletion;
> - the undo or the redo of one of those;

with

> - an edit of an Entry, an insertion or a deletion, and a rename of the Sheet (2026-10-12, below);
> - the undo or the redo of one of those;

and replace

> - a Linked Table's snapshot;

with

> - a Linked Table's snapshot, whatever its rows (2026-10-12, below);

**"Undo draws again."** Replace

> Excel's answer was not observed; the next Windows run asks it.

with

> Excel's undo does the same: on the seventeenth Windows run every Ctrl+Z and Ctrl+Y, two rounds of
> each, drew a number not shown before, never the draw before the edit nor the one after it (Part B,
> b1).

**After the first note of 2026-10-11** (renaming the Sheet), add:

> *(2026-10-12, from the seventeenth Windows run, Part B b2: Excel drew again on the rename, offered its
> undo, and drew again when Ctrl+Z put the name back. As the note above said, this follows Excel's
> answer. A rename is a change, and so are its undo and redo, each counted once; it is in the list
> above. A rename to the name the Sheet already has changes nothing and draws nothing, nor do its undo
> and redo. The decision of 2026-10-11 is replaced; `INDIRECT`'s text still reads the Sheet by the name
> it has.)*

**After the second note of 2026-10-11** (a Linked Table's declaration), add:

> *(2026-10-12, from the seventeenth Windows run, Part B b3: each Refresh All drew again, twice with the
> file unchanged, once after it changed and once more unchanged. A refresh brings data, and ExSheet's
> counterpart is a Linked Table's snapshot, which already counted whatever its rows; nothing changes.
> The same declaration again still draws nothing: a refresh does not declare its query again.)*

**Considered options**, "Undo brings back the draws it undoes". Replace

> Not taken while Excel's answer is unknown.

with

> Not taken: Excel's undo draws again (the seventeenth Windows run).

**Consequences.** Replace

> - The next Windows run asks Excel what an undo does to a draw.

with

> - The seventeenth Windows run asked Excel what an undo, a rename and a refresh do to a draw: each
>   draws again. A rename now counts as a change (2026-10-12).

## `src/ExSheet/README.md`, "Random draws"

Replace

> the same Value, every edit, insertion, deletion, undo, redo, Linked Table snapshot and Linked Table
> declaration draws again,
> and the clock, a format, a width or a rename of the Sheet draws nothing.

with

> the same Value, every edit, insertion, deletion, rename of the Sheet, undo, redo, Linked Table
> snapshot (the same rows again included) and Linked Table declaration draws again, as Excel's undo,
> rename and refresh were seen to, and the clock, a format or a width draws nothing.

## `src/ExSheet.Engine/README.md`, the `RAND` paragraph

Replace

> insertion or a deletion, the undo or redo of one, a Linked Table's snapshot, and a Linked Table's
> declaration that declares it or replaces it each count once and draw again; a recalculation by the clock, a format, a rename of the Sheet (ADR-0180, 2026-10-11)
> and a new seed draw nothing.

with

> insertion or a deletion, a rename of the Sheet, the undo or redo of one, a Linked Table's snapshot
> whatever its rows, and a Linked Table's declaration that declares it or replaces it each count once
> and draw again; a recalculation by the clock, a format, a rename to the Sheet's own name and a new
> seed draw nothing.

and replace

> Excel, nothing draws without a change, and there is no manual recalculation. An undo draws again;
> what Excel's undo does to a draw has not been observed yet.

with

> Excel, nothing draws without a change, and there is no manual recalculation. An undo, a redo and a
> rename draw again, as Excel's were seen to (ADR-0180, 2026-10-12).

## Docs Site, `samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor` (`/exsheet/formulas`), the `RAND` item

Replace

```html
                the seed, the cell, which call in its Formula it is and how many changes the Sheet has had since it
                was opened, so every edit, insertion, deletion, undo, redo, Linked Table snapshot and Linked Table
                declaration draws again,
                while the clock, a format or a width draws nothing. Unlike Excel, nothing draws without a change.
                Renaming the Sheet is not a change: it recalculates what <code>INDIRECT</code> reads by the Sheet's
                name, and the draws come out the same; whether Excel draws again on a rename has not been observed yet.
                An undo draws again rather than bringing back the draws it undoes; what Excel's undo does to a
                draw has not been observed yet. <code>RANDBETWEEN</code>
```

with

```html
                the seed, the cell, which call in its Formula it is and how many changes the Sheet has had since it
                was opened, so every edit, insertion, deletion, rename of the Sheet, undo, redo, Linked Table
                snapshot and Linked Table declaration draws again,
                while the clock, a format or a width draws nothing. Unlike Excel, nothing draws without a change.
                An undo and a redo draw again rather than bringing back the draws they undo, as Excel's do; a
                rename draws again, as Excel's does, and so do its undo and redo. A snapshot draws again even
                when its rows are the ones the table already held, as Excel's Refresh All does with the file
                unchanged. <code>RANDBETWEEN</code>
```

The Example's text ("Type into any cell and every draw changes ... undo, and they change again") stays
true. No Example shows a rename; none is needed for this sentence.

## Definition of Done, SH-66

Replace

> An edit, an insertion or deletion, the undo or redo of one, and a Linked Table's snapshot draw again;
> a recalculation by the clock and a change of format draw nothing.

with

> An edit, an insertion or deletion, a rename of the Sheet, the undo or redo of one, a Linked Table's
> snapshot whatever its rows, and a Linked Table's declaration draw again; a recalculation by the
> clock, a change of format and a rename to the Sheet's own name draw nothing.

(The declaration is ADR-0180's second note of 2026-10-11, which SH-66 does not name yet either.)

## Two sentences that name the question as open

- `docs/specs/exsheet-functions/spec.md`, the Status paragraph: after "which that run asks by hand."
  add "*(The seventeenth run asked them, 2026-10-11: an undo and a rename each draw again in Excel;
  ExSheet's undo already did, and its rename now does, ADR-0180's note of 2026-10-12.)*"
- ADR-0047's dated addition of 2026-10-11 names the two questions as left for the sign-off's run. It is
  a dated record and can stay; if the orchestrator adds a pointer, it is "(answered by the seventeenth
  Windows run: ADR-0180, note of 2026-10-12)".

## The Change Note

No `run17-draws-rename-*.md` was written. `RAND`, `RANDBETWEEN` and `Seed` are in no published version
(they came with #78, after `v0.1.0-beta.2`), so a Consumer of a published version sees no different
result, and a Behaviour note would say otherwise. ADR-0190's `Kind:` is also one of Added, Fixed,
Behaviour and Breaking; "Changed" would fail the Docs Site's build. The feature's own Added note,
`docs/releases/unreleased/sheet-seed.md`, said "never with the clock or a rename", which is now false;
its last sentence was corrected in place to name the rename among the changes. If the orchestrator wants
a note of the area's own regardless, it is:

```
Product: ExSheet
Kind: Added
Docs: exsheet/formulas#supported-functions
ADR: 0180

Renaming the Sheet is a change, as in Excel: `RAND` and `RANDBETWEEN` draw again on a rename and on its undo and redo, and a rename to the Sheet's own name draws nothing.
```

## Elsewhere, not this area's

- NAME-036's claim that Excel does not undo a rename (`ExcelCases/sheet-names.json`) is corrected by the
  records-and-widths area, which owns that file. b2 shows Excel offers the undo and Ctrl+Z puts the
  name back.
