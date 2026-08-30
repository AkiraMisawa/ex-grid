# The grid owns a generic Cell State vocabulary. The Consumer's vocabulary stays out

The grid understands "this cell is in an unusual state" as a **generic vocabulary** — normal /
stale / missing / error / modified. Appearance is decided by the theme, and **accompanying data
such as tooltip text stays opaque** and is rendered by the Consumer. A Consumer's own vocabulary
(as-of stamps, staleness) does not enter the grid.

Cell State rides on the **Cell Metadata** mechanism from
[ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md) — not stored on the cell,
but asked for by (row, column).

## What justified a generic vocabulary

Two Consumer requirements with entirely different origins landed on the same mechanism.

| Consumer | What it wants to convey | Origin |
|---|---|---|
| A position screen | this cell's value is as of close-of-business, the one next to it is intraday | a batch as-of stamp, at (book × metric) grain |
| A what-if screen | this cell was overridden by the user | scenario Overrides |

One alone would have been "that Consumer's convenience". **Two arrived at the same shape
independently.** Both are states that cannot be derived from the value and can only be supplied
from outside, and both want to be signalled by changing appearance. That is enough to justify
putting a vocabulary in the grid.

## It is distinct from value-derived decoration

Easy to conflate, so state it plainly. "Changing how a cell looks" has **three different
origins**.

| | Decided by | Example | Held by |
|---|---|---|---|
| Format | the value | thousands separators, two decimals, date format | **Column** |
| Value-derived decoration | the value | red when negative, yellow above a threshold | **Column** (a rule) |
| **Cell State** | (row, column) metadata | stale, missing, error, modified | **asked of the Consumer** |

The first two are decided by looking at the value, so the grid needs no special knowledge and a
rule on the column suffices. This ADR concerns only the third.

## Considered Options

- **Keep it entirely opaque and let the Consumer paint every cell** — rejected. The Consumer
  would end up writing "numbers are right-aligned, thousands-separated, negatives in brackets"
  every time, which is backwards for a component whose claim is Excel-like operability. It is
  **also worse for performance**, because
  [ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md) established that custom
  rendering means componentising that column.
- **Adopt the Consumer's vocabulary directly (as-of / staleness)** — rejected. It fuses the grid
  to one Consumer. The term **Consumer** exists in `CONTEXT.md` precisely to avoid this.

## Consequences

- **The granularity of the vocabulary has to be decided up front.** Adding values later is easy;
  changing what one means is a breaking change. The present five (normal / stale / missing /
  error / modified) are induced from two cases, and should be revisited when a third Consumer
  appears.
- **Degree is not expressed.** A Consumer may want to distinguish close-of-business from a
  particular intraday run, but Cell State says no more than "stale". The timestamp itself goes in
  the accompanying data, on the tooltip.
- **The cost is measured and acceptable.** A per-cell metadata lookup is +14–18% against plain
  markup and is included in the measured 1.90 ms (800 cells) with row-level memoisation.
  **Keep the lookup light** — roughly one dictionary probe, with no string building and no
  allocation. It is called once per cell, on every render.
