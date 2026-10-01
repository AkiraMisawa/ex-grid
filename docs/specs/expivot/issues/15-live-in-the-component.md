# 15: Live data in the component

Status: done

**What to build:** the component's side of ADR-0066.

- **Redrawing:** changes are gathered and redrawn every 250 ms by default. The Consumer may set the
  interval, and 0 redraws on every change.
- **Keeping state:** a change of values alone keeps the Row Sequence Version, the Selection and an
  open menu or panel.
- **The Change Highlight:** `CellChangedAt` is answered by comparing painted text with the previous
  reports that are still within the highlight's duration. Only data marks a cell, and every cell of
  a new row is marked.
- **The Stale Report:** its notice, with Retry.
- **A server's source:** its "changed" notice makes ExPivot ask again.

**Blocked by:** 11, 12, change-highlight 01

- [x] PV-35, PV-36, PV-37, PV-38, with a fake `TimeProvider`

## Comments

2026-10-01: Built, against fake sources (the engine's folding of a Change Batch is ticket 11's).
ExPivot listens to `Source.Changed` — the handler is held, removed when the source is replaced and
on dispose, and marshalled with `InvokeAsync` — and asks again for the whole answer. New
parameters: `RedrawInterval` (250 ms; zero asks on every change), `ChangeHighlightDuration` (1 s)
and `Clock` (the registered `TimeProvider`, else the system's, as the grid resolves its own);
`IsStale` reads the state.

- **Gathering.** A change is asked for no sooner than `RedrawInterval` after the last change
  reached the screen (shown, or found unshowable), so redraws stay that far apart whatever the
  latency; the first change after a quiet spell is asked at once. A change never cancels a
  question out: a user's layout question, or one for newer data, is answered first, and the
  changes that arrived meanwhile are asked for after it. A user's gesture supersedes a question
  for newer data (the user's layout wins), and its own question answers the change; one laid out
  from the answer held gathers the change again. Refresh and Retry ask at once. A question asked
  because the source said its data moved on is quiet — no loading indication — so a live report
  does not flicker; Refresh, Retry and a new source dim it as before. A notice naming the Source
  Version already on screen asks nothing.
- **Keeping state.** The Row Sequence Version moves only when the rows do (`HasSameRowsAs`), so a
  change of values keeps the Selection; menus and panels stay open, and Filter… lists the new
  version's Items.
- **The Change Highlight.** A history of the reports of the recent data versions under one layout
  and one set of words (`ReportHistory`), bounded to the versions whose marks can still show plus
  one baseline. A cell is marked at the time of the newest version whose painted text differs
  from the version before; cells are matched by the row's role, Value Field and Items and the
  column's name, by position while the rows (or columns) are the same and by key otherwise. A cell
  with no counterpart — a new row's, or a new column's — is marked. Anything but data (a layout,
  sort, collapse, form, Show Values As, format, words, new caps) starts the history again. The
  grid gets a new delegate per data version, the same one otherwise, and null while nothing can
  be marked.
- **The Stale Report.** A question for newer data (a source's notice, Refresh, Retry, a new source)
  under the layout on screen that is refused, fails, or whose report would break a rows or columns
  cap leaves the report as it was, with a notice in an always-present `role="status"` region under
  the toolbar: "Showing the data as of {time}: {what happened}", the time in the report's culture
  (with the date when not today's), and Retry. It goes when an answer is laid out — a new one, or
  the held one a cap refused once a layout fits it. A layout's refusal stays the toolbar's
  `role="alert"` notice, and the layout goes back. `IPivotChrome.StaleReport` draws it
  (`PivotStaleReportContext`); `MudPivotChrome` draws a warning `MudAlert` with a Retry
  `MudButton`. The new words (`stale-report`, `stale-too-many-cells`, `stale-too-many-rows`,
  `stale-too-many-columns`, `stale-source-failed`, `stale-source-refused`, `retry`) have English
  in `StaleReportWords` until `PivotWords` holds them with their Japanese.

Layer 2: `LiveDataTests`, `ChangeHighlightTests` and `StaleReportTests` in
`tests/ExPivot.Components` (with `LiveSource`), and `MudPivotStaleReportTests`. Layer 3 waits for
`/pivot-live` (ticket 19).
