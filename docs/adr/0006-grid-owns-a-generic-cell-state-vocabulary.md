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
| Format | the value | thousands separators, two decimals, date format | **Column** (the `Format` delegate) |
| Value-derived decoration | the value | red when negative, yellow above a threshold | **Column** (the tone rule — see below) |
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

## How a change reaches the screen: the lookup's identity

*(Decided while implementing. The ADR had settled that the state is **asked for** by
(row, column) and left open how the grid learns that an answer would now be different.)*

Metadata changes without any row instance changing — a batch lands, and the rows are the
same objects they were. The grid cannot see that by comparing rows, and re-reading every
visible cell on every render is exactly the per-cell cost row memoisation exists to avoid.

**The lookup delegate's identity is the change signal.**

```
hold the lookup in a field                → same reference = the metadata is unchanged
hand over a NEW lookup when it changes    → reference changed = repaint the rows
```

It is the same rule as ADR-0003's "a new row instance, never a rewrite in place", and the
same rule as its "cache delegates passed as parameters in a field" — one discipline for a
Consumer to learn instead of three. A version number beside the lookup was rejected for
being a second thing to keep in step: "swapped the lookup, left the version alone" fails
silently and looks correct on screen.

**The cost is real and is accepted:** rewriting the dictionary behind a lookup that stays
reference-identical leaves the old states painted. That is why the sample's cells page has
two buttons doing the same edit — one hands over a new lookup, one does not — and why a
bUnit test pins the second one's screen as unchanged.

## The two Column-held rows of the table, given their shape

*(Decided 2026-09-02, when the first Consumer request for "negatives in red" arrived and
the table above turned out to name the owner of both rows without saying what either
looks like. The code had neither: cells painted the value's bare `ToString()`, and
nothing derived anything from a value.)*

**Format** is a delegate on the column, `Func<object, string>`, and it is the one text
the grid shows for a value: the cell, copy's `text/plain`
([ADR-0005](./0005-copy-refuses-rather-than-truncates.md)), the filter's value list, the
Auto width estimate and the text the editor opens with. Copy's raw `text/html` never
goes through it. A null value paints empty and is not offered to the delegate — an
absent value is a Cell State's business (Missing), not a format's. The delegate owns
the culture; the grid takes no view on separators.

**Value-derived decoration** is a second delegate on the column, the **tone rule**
`Func<object, CellTone>`, answering a closed enum — `None / Positive / Negative` — that is
painted as an interned class (`ex-tone-positive`, `ex-tone-negative`) and coloured by a
Visual Token (`--ex-tone-positive-color`, `--ex-tone-negative-color`). Null is never
offered to it either. A tone names a **meaning**, never a colour, and the bare grid's
tokens default to `inherit`: a Consumer that declares a rule and no theme sees ordinary
cells, which is Excel's default number format too. The MudBlazor Wrapper maps the two
tokens onto the palette's error and success.

Three shapes were considered and refused:

- **The grid derives the tone itself** — `ex-negative` on every Number cell below zero,
  no rule, the token the only opt-in. Refused by the user on the ground that how a
  number is shown is the Consumer's to say, not the grid's to assume; a grid that colours
  the sign unasked is one step from a grid that brackets it unasked. The counter-argument
  in *Considered Options* above ("the Consumer would end up writing 'negatives in
  brackets' every time") is met by the rule being one comparison, declared once per
  column.
- **Excel's own form, `#,##0;[Red]-#,##0`** — the colour inside the format. It is the
  form a finance Consumer already knows, and it puts a colour name in C#, which
  [ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md) forbids for
  the reason it gives: appearance that travels through C# cannot follow a theme switch
  without a render. The tone enum is that form with the colour taken out and left to
  the token.
- **An open class string per cell** — the most general shape and the one
  [ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)
  refuses: a string built per cell is an allocation on the render path (P5), and a
  vocabulary the Consumer invents is a vocabulary no theme can style.

`Positive` is in the vocabulary from the start because a P&L screen colours gains and
losses as a pair; a third member (a threshold breach, say) waits for a Consumer to ask.

**Cost.** One delegate call per painted cell of a column that declares a rule — the same
path and the same price as `Format`, beside which it runs on the value already fetched —
and the `CellClasses` table grows from 80 interned strings to 240, once, at start-up. A
column without a rule pays nothing. Measured on the render bench's `RowComponentTone`
mode against `RowComponent`, the settled design — 800 cells (40 × 20), 200 iterations,
Release, Chrome 152 headed under WSLg, so the absolute numbers are that machine's and only
the pair is the point:

| Run | `RowComponent` p50 / p95 / max | `RowComponentTone` p50 / p95 / max |
|---|---|---|
| 1 | 12.2 / 13.3 / 16.6 ms | 13.0 / 14.9 / 20.4 ms |
| 2 | 12.1 / 13.2 / 15.8 ms | 12.7 / 13.5 / 15.6 ms |
| 3 (saved as `results/20260902-103201-410.json`) | 12.1 / 12.8 / 13.4 ms | 13.1 / 14.1 / 20.6 ms |

About **+0.6 to +1.0 ms at the median, 5–8%**, for a rule on every one of the 800 cells —
the same order as the per-cell metadata lookup this ADR already accepted (+14–18%), and
under it. The maxima swing run to run (the bench's known variance); the medians do not.
The number is the rule's cost on every cell; a Consumer declaring it on one column of
many pays that column's share.

## Missing paints no substitute text

Also decided while implementing. The five states decorate; none of them replaces the
cell's text. **Missing** is the one worth stating outright: it does not paint a dash, an
`n/a` or anything else. A Blank the Consumer really holds and a cell nobody could answer
for must stay distinguishable, and inventing a glyph for the second is the grid saying
something it was not told (ADR-0023 owns what Blank means).

## A state must never be the thing that disappears

*(Decided while implementing, after a review found two ways it could.)* A cell can be
under three things that all want to paint its ground: the **Pinned Columns**' opaque
background (they must be opaque — the columns underneath pass beneath them), the **Row
Kind** of the row it sits in ([ADR-0024](./0024-row-kind-is-a-declared-role-not-a-hierarchy.md)),
and its own **Cell State**. Two rules settle it, both in CSS:

- The pinned ground is a `background-color`; every tint is a `background-image` layer, so
  a tinted pinned cell keeps both instead of one silently replacing the other.
- Cell State is written with a doubled class (`.ex-cell.ex-state-x`) and declared last, so
  **a state outranks a role**. A state is something the grid was explicitly told; a role is
  something about the row's neighbours. If one of the two has to be invisible, it is not
  the one the Consumer named.

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
