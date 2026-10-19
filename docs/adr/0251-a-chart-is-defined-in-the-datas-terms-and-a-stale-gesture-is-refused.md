# A chart is defined in the data's terms, and a gesture taken against an older order is refused

*Decided with the user, 2026-10-19. The proof of concept's choice (research note §3.2), with the user's rule that
each series has dates of its own (U8, 2026-10-13).*

A **Chart Definition** names what is charted by what it is, never by where it was painted:

- a series is a **column, by its name, at a set of Row Keys** (ADR-0140), or **a row, by its Row
  Key, across a set of columns**, each column at a position the Consumer declares;
- every series carries rows or columns of its own, as every Excel series has its own ranges;
- the horizontal axis is a column (dates or numbers), positions the Consumer declares per column
  (tenor years), or categories in a declared order.

The proof of concept's three shapes — series in columns, series in rows, and a model a page built
with a `Func` — become one: a list of series, each a list of points `(x, y?)` with its Series Kind
(ADR-0253), its panel, its axis side and its slot. Series in columns and series in rows are two
**readers** of a grid that produce that list; a Consumer may produce it directly. ExPivot's pivot
chart, later, is a third reader.

## Why

- A sort, a recalculation or a column drag leaves the chart meaning the same thing; a definition by
  indices would silently chart other rows (ADR-0011's reason).
- One shape underneath means one tooltip, legend, filter, CSV and test suite, not three.

## The selection bridge

`ExChart` turns a grid selection into a Chart Definition through the context menu's Consumer
command (ADR-0036), which carries the ranges and the Row Sequence Version. No core change is needed.

- **A selection taken under another Row Sequence Version is refused** ("The rows were reordered after
  the selection was made. Select again.") — spine rule 6.
- **Nothing is guessed** (spine rule 1): headers are not data; names and panels come from Header
  Groups; a selection with no axis column, two, a text column, or a number column with no declared
  position is refused by name.
- **One selected cell charts the Window**, Excel's "chart the table around it"; text columns nobody
  chose are left out, chosen ones refused.
- **Gestures on the chart are ordered** with the product instance's input (ADR-0240): each changes
  one field of the state as it is when handled, never the whole state it was rendered with.
