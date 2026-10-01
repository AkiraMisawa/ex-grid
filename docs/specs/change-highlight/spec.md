# Change Highlight

Status: ready-for-agent

Decided by [ADR-0068](../../adr/0068-change-highlight-is-asked-of-the-consumer-and-painted-without-animation.md),
in the ExPivot grilling of 2026-09-30. Exit criteria: `docs/definition-of-done.md` §26, DC-60 to
DC-62. This spec synthesises those decisions; where it and they disagree, they win.

## Problem Statement

On a live screen, values change several times a second. The eye cannot find which one moved.
Trading screens mark a changed value for a moment, and users of this family expect the same. ExGrid
has no way to do it. Three reasons make it unsafe to leave to each Consumer:

- **Rows are recycled.** A mark tied to an element would travel to another row on a scroll.
- **A fade would animate from one row's colour to another's.** That is why nothing under the
  Viewport may animate (P8, UX-6).
- **The grid does not know what changed.** It holds no values between Windows, and Row Identity is a
  reference.

## Solution

**An opt-in declaration, `CellChangedAt`, asked by row and column as Cell State is.**

- The Consumer answers with the time the cell's shown value last changed.
- The grid paints `ex-changed` on the cell while that time is less than `ChangeHighlightDuration`
  (1 s by default) before its `Clock`'s time, and removes the class in one step when the mark ends.
- A single timer re-renders only the rows whose marks end.
- The colour is one Visual Token, `--ex-change-highlight-background`, which defaults to the system
  colour `Mark`. The MudBlazor Wrapper maps it onto its palette.
- Nothing animates, and nothing is announced.

## User Stories

1. As a user of a live screen, I want a value that just changed to be marked for a moment, so that
   I see what moved.
2. As a user, I want the mark to stay on its cell when I scroll, so that it never points at another
   row.
3. As a user who asks for reduced motion, I want the mark to appear and disappear without motion,
   so that it works for me as it does for everyone.
4. As a developer, I want to tell the grid when a cell changed, from whatever my server tells me,
   so that the grid never has to compare values it does not hold.
5. As a developer, I want the mark's colour to come from my theme, so that it fits my palette in
   light and dark.
6. As a developer who declares nothing, I want nothing to change, so that my grid costs what it did.

## Implementation Decisions

- **The declaration's parameters:** `CellChangedAt` (a `CellChangeOf<TRow>` delegate, returning
  `DateTimeOffset?`), `ChangeHighlightDuration` (a `TimeSpan`, 1 s by default), and `Clock` (a
  `TimeProvider`, the system's by default).
- **The delegate's identity is the change signal**, as with `CellState`.
- **Each row records the earliest end among the marks it painted.** The grid keeps the minimum
  across rows in one timer. When the timer fires, only the rows whose marks have ended render.
- **The delegate is asked of value cells only.**
- **The class is interned**, like the other per-cell classes (P5), and no transition is added.
- **The forced-colors block restates the mark.**

## Testing Decisions

- **Layer 2**, with a fake `TimeProvider`: which cells carry the class, render counts when a mark
  ends, and nothing at all without the declaration.
- **Layer 3 on `/grid-live`**: a mark survives a scroll on its cell, and UX-6's check still passes
  with marks painted.

## Out of Scope

- Marking the direction of a change.
- An audible or announced change.
- ExSheet's use of the declaration.
