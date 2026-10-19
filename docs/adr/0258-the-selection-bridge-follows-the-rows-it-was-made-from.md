# The selection bridge follows the rows it was made from

*Decided with the user, 2026-10-19, in the ExChart project's M3 grilling (grid integration). It
continues ADR-0251's "The selection bridge", which this ADR does not change: a stale selection is
refused, nothing is guessed, one cell charts the Window, and gestures on the chart are ordered
(ADR-0240).*

**ExChart supplies the grid's side of a chart, ready-made, and a chart made from a selection keeps
meaning the rows it was made from, whatever the grid does next.** ExGrid is not changed and does not
know ExChart (ADR-0250's dependency direction).

## The command, and where the chart goes

*M3-Q1, M3-Q5.* ExChart supplies a context-menu command (ADR-0036) that a Consumer adds to the grid's
menu in one line. It builds the Chart Definition from the selection and raises it as an event; where
the chart is shown is the Consumer's.

- The Built-in package supplies the bare `<ExChart>` and nothing to place it in.
- The MudBlazor Wrapper supplies a **Chart Dock**: a panel beside or below the grid, never over it,
  with several charts as tabs, which the command can open directly.
- Nothing floats over the grid, as Excel's chart objects do: a chart over the grid hides values the
  reader came to see (spine rule 1).

ExGrid's core takes no "Chart selection" command of its own: it would make the grid depend on the
chart.

## Series in columns or in rows

*M3-Q2.* The orientation comes from the grid's declarations, never from the selection's shape (Excel
guesses from it, and a one-row selection then charts the wrong way without saying so):

- a selection holding an axis column (dates or numbers) charts **series in columns**;
- a selection of columns with declared positions (tenor years) charts **series in rows**;
- when a selection holds both — a curve history, dates down and tenors across — both charts mean
  something (a tenor's history, a date's curve), so only then does the menu offer two items,
  "Series in columns" and "Series in rows";
- when it holds neither, the command is refused by name.

The Consumer may fix the orientation on the command.

## Adding a series

*M3-Q4 (the user's U7).* The grid's menu offers "Add to chart: *name*" for each open chart the
Consumer registered as a target. The added series keeps rows of its own (U8) and takes its panel by
the Header Group defaults (ADR-0251). A series whose axis does not match the chart's (tenors into a
chart along dates), or one already in it, is refused by name.

## What a chart does when the data moves

*M3-Q3, M3-Q7.* A chart made from a selection is the set of Row Keys it was made from:

- **A recalculated value is redrawn.**
- **A row that no longer exists becomes a gap**, and a Note under the chart (ADR-0257) says how many
  of the selected rows are gone. It is never drawn as zero, and never joined across.
- **A new row is not added**, even inside the dates the chart spans: the definition never widens
  without the user asking. A sort or a column order has no meaning for a set of keys, so a "range"
  would not be defined beyond dates.
- **Filtering the grid leaves the chart as it is.** The grid's filter and the chart are two views of
  the same data; the chart is narrowed under Chart Filters. Excel's default ("plot visible cells
  only") makes a chart change while a grid is briefly narrowed, and change back.

The bridge therefore reads a chart's values **by Row Key from the Consumer's data, never from the
Window**: read from the Window, a row only filtered out of the grid would be reported as gone.

## The chart and the grid together

*M3-Q6.* A click on a point raises an event with its Row Key and column, and the ready-made bridge
scrolls the grid to that row and puts Focus on it. Nothing links on hover, in either direction: it
would cost a round trip per pointer move on the Server host, draw a second hover band beside the
grid's own (ADR-0029), and widen the chart's JavaScript entry (ADR-0255).

## Settled elsewhere

- Nesting a chart's state in the grid's Saved View does not arise: the chart holds no View State and
  raises events for what the user changes (M2-Q4).
- A Diff goes to a panel of its own by default (ADR-0251, M1-Q7).

## Considered and rejected

- **ExGrid's core command** (Excel's way): the grid would depend on the chart.
- **Every Consumer writing the bridge**, as the proof of concept did: every application would write
  the same wiring.
- **Orientation by shape**, and **asking every time**: the first guesses; the second always offers
  one item that is refused or meaningless.
- **A snapshot of the values** with an "as of" time: the grid and the chart would show different
  numbers for the same row.
- **Following a range of dates**, and **following the grid's filter**: both widen or narrow the chart
  without the user asking it to.
- **Hover linking both ways**: see above.
