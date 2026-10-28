# A Consumer's context commands may be answered later, and the input after them waits

*Decided on 2026-10-28 in the project thread "ExChartのBlazor版をTSコアの包みにする" ("Make Blazor's ExChart a
wrapper over the TS core"), while carrying the Chart Dock to the grid the Script Core draws. Put to the user with
two alternatives: a menu that only MudBlazor's Chrome fills with .NET's commands, and leaving the Chart Dock to the
Razor grid until that component is removed. Decided by the user the same day: the menu waits for the commands.*

## Context

The Chart Dock ([ADR-0258](./0258-the-selection-bridge-follows-the-rows-it-was-made-from.md)) adds
"Chart", or "Series in columns" and "Series in rows", and "Add to chart: *name*" for each open tab, to the grid's
Context Menu. The commands come from `GridChartBridge.Commands`, handed to the grid's `ContextCommands`.

- **The Razor component** asks `ContextCommands` in .NET as the menu opens. On a circuit the opening is itself a
  round trip, so the commands are always asked of .NET's state as it stands after every earlier event.
- **The Script Core** opens its menu in the browser and asks `contextCommands` synchronously
  ([ADR-0036](./0036-the-context-menu-is-the-column-menu-shape-over-a-selection.md)). Blazor's adapter
  ([ADR-0350](./0350-exgrids-core-moves-to-typescript-and-react-and-blazor-both-take-it.md) section 7) cannot answer from .NET
  in time, so it offers no Consumer commands at all.

The commands depend on what only .NET knows. Which of "Chart" and the two orientations are offered depends on
the selected columns' declarations. "Add to chart" depends on the dock's open tabs, which change when a command
runs in .NET. Commands copied into the browser ahead of time would offer the menu as it stood before the last
command ran, whenever the user is faster than the circuit: the spine's sixth rule.

The core already holds input behind an answer that is out: a Pending Commit
([ADR-0380](./0380-a-verdict-or-a-refusal-may-be-answered-later-and-the-next-input-waits-for-it.md) section 3),
work of the Consumer's placed in the Input Order
([ADR-0240](./0240-input-to-one-product-instance-is-taken-in-one-order.md) section 6), and a
declared key's answer (ADR-0240's note of 2026-10-19).

## Decisions

### 1. `contextCommands` may answer a promise, and the menu stands once it settles

In TypeScript, `contextCommands` answers `readonly GridCommand[]` or a promise of them. An answer given at once
behaves exactly as before.

While a promise is out, the menu's opening takes its place in the Input Order. Every input made meanwhile is held,
in its order, and taken once the menu stands, as behind a Pending Commit. A key typed after the right-click
therefore reaches the menu, however fast it follows. A menu standing when the next is asked closes at once, with
the keyboard back on the grid, so no press reaches its commands while the next menu's are out.

The menu does not open if, while the answer was out, an order, a source or the columns moved, or the Consumer
placed the Selection elsewhere (`placeSelection`, which is not an input and is not held). It would act on a
Selection those changes took, and an open menu closes on the same changes (ADR-0011, ADR-0036). Where it opens,
it stands where it was asked to, in the Viewport's box, as an open menu stays there when the grid scrolls.

A promise that rejects opens nothing. One that rejects with an `AbortError` was given up by whoever answers, who
says why itself, as a Find step's is (ADR-0055), and nothing more is said. Any other rejection reaches the page
unheard, as a declared key's does.

### 2. A command's `invoke` may answer a promise, which holds the input after it

`GridCommand.invoke` answers `void` or a promise. A promise takes the command's place in the Input Order: every
input made after the press is held until it settles, and taken against what it left. A menu opened at machine
speed after "Chart" is therefore asked of the dock with its new tab. It is never asked of the dock as it stood.

This is the same placement as a declared key's answer. The core's own commands and a Consumer's synchronous
commands are unchanged.

### 3. Blazor's ExGrid over the Script Core takes `ContextCommands` and `ContextCommandLabel`

`ExGrid.Script.ExGrid` takes the Razor component's `ContextCommands`, and `ContextCommandLabel` for their words.

- As the menu opens, the script asks .NET with the browser's number for the menu, the source's generation and the
  columns' number it holds, the row by the id it crossed with, the column, the Selection and its Row Sequence
  Version. The answer is asked in its turn among the grid's events. .NET answers each command's id, whether it is
  enabled, and its words.
- A menu opened under columns, a source or an order .NET no longer holds is not handed to the Consumer, whose
  positions would be read against another grid. .NET sends what it owes the browser and answers it as moved; the
  script gives the menu up with an `AbortError`, and none stands. A component gone answers nothing, and the menu
  is given up the same way.
- A press runs the command by the menu's number and its index. .NET keeps the commands of the last menu asked,
  lets them go once one has run, and refuses any other menu's as internal. The source's owed state crosses before
  the run is answered, so the input held behind it is taken against what the command wrote.
- The context's `Commands` is empty: the core's own commands run in the browser. Its `Close` is refused by name,
  because the core closes the menu as a command runs.
- `ContextCommandLabel` words the Consumer's commands only, and is named so: the Razor component's
  `CommandLabel` also words the grid's own commands and refusals, which the Script Core words itself today.
- `RowSource` exposes the source the grid reads: `Source`, or the one it made of `Rows`. A command reads the rows a
  Selection covers from it.

### 4. The Chart Dock carries over

- `GridChartBridge.ScriptGrid` charts the Script grid's selections. It is set in place of `Grid`, and setting both
  is refused. The bridge reads rows from the grid's `RowSource` and shows a point's cell through the grid's
  `PlaceSelectionAsync`.
- `MudChartDock.ScriptCore` draws each tab's chart with `ExChart.Script.ExChart`.

## Considered and rejected

- **Only MudBlazor's menu, filled in .NET.** With a Razor Chrome, the menu's content is already drawn by .NET a
  round trip later (ADR-0410), so .NET could add its commands as it draws. No core change is needed. But the
  Script Core's own menu would show no Consumer command, and a press on one would run in Razor, outside the
  Input Order that ADR-0410 keeps every slot in. A `ContextCommands` that only works under one Chrome is a
  parameter that is quietly ignored under the other.
- **The commands copied into the browser ahead of time.** .NET would send the commands after every selection
  change and every command run. The menu would show whatever had arrived when it opened: the timing-dependent
  outcome the spine's sixth rule forbids.
- **Leaving the Chart Dock to the Razor grid.** It stands until the Razor components are removed (ADR-0370
  section 11). After that the Chart Dock would be gone for Blazor Consumers, and every Blazor Consumer's own
  context commands with it.

## Consequences

- React's Consumer may also answer `contextCommands` with a promise, and run a command asynchronously.
- A Blazor Consumer moving to the Script grid keeps its `ContextCommands` unchanged, apart from `Commands` being
  empty and `Close` refused.
- The Chart Dock reaches parity over the Script Core, under MudBlazor.
