# Over the Script Core, what a grid paints comes with each row, and what the Consumer answers later is asked in its turn

*Recorded on 2026-10-29 in the project thread that removes the C# core (ADR-0370 step 3), under the user's standing
instruction to carry 0.3.0 through without stopping ("止まらないで", "don't stop"). It applies the rules ADR-0350
section 7, [ADR-0420](./0420-a-chart-drawn-in-the-browser-takes-format-codes-and-the-consumers-own-script-in-place-of-a-dotnet-function.md),
[ADR-0421](./0421-a-consumers-context-commands-may-be-answered-later-and-the-input-after-them-waits.md) and
[ADR-0431](./0431-over-the-script-core-a-grids-formula-entry-functions-are-the-consumers-script-and-the-colours-it-hands-out-are-told.md)
already set to the rest of the Razor component's surface; no option the user had not already chosen for another
member was taken.*

## Context

On 2026-10-29:

- **ADR-0370 step 3 removes the C# core**, and with it the Razor `ExGrid.Components.ExGrid`. Every DemoHost page and
  Docs Example that draws an ExGrid on Blazor moves to `ExGrid.Script.ExGrid` first, and so does every Consumer.
- **`ExGrid.Script.ExGrid` carried the first slice** (ADR-0350 section 7): columns, rows through a Grid Source,
  edits, pastes, fills, Cell States, Find, the Context Menu's commands, completion, Formula entry, Header Groups and
  the slots. It refused by name, or did not have, the rest: Row Kinds, Cell Appearance, change times, Row Marks
  through the component, the Consumer's copy, Replace All, the Selection Summary's answerer and its events, declared
  keys, the pager, the request margin, Row Headings, the hidden header, the Name Box's label, a Find rule of the
  Consumer's and the data edge, and toned columns and the Mark Column.
- **The Script Core asks each of these in one of three ways.** As it paints a row (the kind, a cell's appearance,
  when it changed, whether the row is marked, a value's tone); as a question it can wait on, with a signal
  (a copy's answer, a Selection Summary, a Replace All, a declared key, a marking gesture); or synchronously by
  position or pattern, where the browser decides a key or paints a place no row may yet stand in (a Row Heading,
  the Name Box's label, whether a cell matches, where Ctrl+arrow stops).

## Decision

### 1. What a row paints is answered in .NET and crosses with the row

`RowKind`, `CellAppearance`, `CellChangedAt`, a toned column's tones and whether a row is marked are asked of each
row as .NET writes it, beside its values and its Cell States, as ADR-0380 section 5 already has Cell States cross.
A row whose answers change is another object in the browser, so the core paints it again (ADR-0003). A delegate's
identity is the change signal, as on the Razor component: another one sends the rows the browser shows again.

- **A change time crosses on .NET's clock**, with .NET's time on the state it came with, and the browser reads it
  on its own clock by the difference. A server whose clock is not the browser's highlights for the right time.
- **Marks cross only beside a Mark Column.** A grid's marks are its `Marks` or, where unset, its source's; both set
  is refused by name, and so is a Mark Column with neither. The counts come with the state.

### 2. What the Consumer answers later is asked in its turn

A copy's answer and a Selection Summary are questions the browser numbers, asked in the component's turn among the
grid's events and called off by number when a newer one supersedes them, as the Context Menu's commands are
(ADR-0421). Replace All, a declared key and a marking gesture are writes of the user's: each is handed to the
Consumer in its turn, what it writes crosses before the browser hears it was handled, and the input after it waits
(ADR-0142). An exact figure crosses as its digits and scale in text, which JSON holds whole.

### 3. What the browser asks by position or pattern is the Consumer's own script

`RowHeadings`, `NameBoxLabel`, `FindMatch` and `DataEdge` are each a `ScriptFunction`, loaded before the grid is
made, refused by name as ADR-0431 section 1 has the Formula entry functions refused. `FindMatch` is handed the row's
values by column name, since the browser holds no row of .NET's.

### 4. The grid's own words cross as a table, asked of every id

`CommandLabel` words the grid's own UI (ADR-0036): its commands, its fields, its refusals, its summary, its find
panel and its reload notice. The browser words them as it draws, synchronously, so .NET asks the function of every
id the grid words, the public constants of the classes that name them, as the grid is made and whenever another
function is given, and sends the answers as one table; an id answered null keeps the grid's words. A placeholder such
as `{column}` is a mark inside the words, not an id, and is not asked. The commands `ContextCommands` answers keep
their own words, `ContextCommandLabel`'s, which come with the menu (ADR-0421).

*Added on 2026-10-29, in the same thread, when the DemoHost pages and the Docs Examples moved onto
`ExGrid.Script.ExGrid` and an Example that replaced two refusals' words had no parameter to take them.*

## Considered options

- **Asking .NET as the core paints.** A round trip per row or per cell on Server, and no answer at all where the core
  paints synchronously. Rejected for painting as ADR-0420 rejected it for a chart's format.
- **Asking the synchronous four of .NET later, the keys held.** Rejected as ADR-0431 rejected it for Formula entry:
  it reworks the core React shares, for a round trip per key on Server.

## Consequences

- A Consumer that answered Row Headings, the Name Box's label, a Find rule or the data edge in C# writes them in a
  module once, for both hosts.
- A grid with `CommandLabel` sends its words once per function, a few hundred short strings, not per row or key.
- Each row crosses with what it paints, so a grid with appearances, change times or marks sends more per row; a grid
  without them sends nothing more.
