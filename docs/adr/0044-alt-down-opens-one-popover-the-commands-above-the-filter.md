# Alt+↓ opens one popover: the column's commands above its filter, as Excel's drop-down

*(Decided with the user, 2026-09-26, after comparing Alt+↓ with Excel's AutoFilter drop-down.)*

Excel opens one drop-down from a filtered header. It holds the sort commands, "Clear Filter From",
the condition filters, a search box, the checkbox list of the column's distinct values, and OK and
Cancel. The grid opened two things in turn: the column menu
([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)) — sort, Filter, hide, pin, size to
fit — and, from its "Filter" item, the filter panel
([ADR-0009](./0009-filter-panel-contract.md)) as a second popover. Every piece Excel shows was
there, one step further away than someone coming from Excel expects to find it. No ADR had
decided the split. It was how the two seams happened to be wired.

## Decision

**Alt+↓, and the header's ▾, open one popover.** The column's commands stand at the top, and
below them, where the column can be filtered, stands its filter: the search, the value list or
the condition, and OK and Cancel. The "Filter" command goes, because what it opened is now
already open. Hide, pin and Size to fit stay in the command list. Excel keeps those on the
header's own menu, and this grid has only the one menu to put them in. A column that cannot be
filtered shows the commands alone.

**The two seams stay two seams.** `IGridChrome` still supplies the column menu and the filter
panel separately, each with its own context (ADR-0009/0010), and the core stacks what the two
return inside the one popover it places ([ADR-0040](./0040-a-popover-stays-inside-its-grids-box.md)).
A substituted Chrome, `ExGrid.MudBlazor`'s included, changes what each half looks like and nothing
about where they stand or when they close. OK, Cancel, a command run, Escape or a dismissing press
closes the whole popover, and the keyboard goes back to the root
([ADR-0039](./0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md)).

**The keyboard is ADR-0039's, over both halves.** The popover opens with DOM focus on its first
enabled command. ↑ / ↓ move among the commands as they did. Tab leaves the commands for the
filter's first control, moves through the filter's controls, and wraps back to the first command,
so focus never leaves the popover by Tab. Which letters jump where is decided below.

Rejected:
- **Keeping the two steps.** Nothing is lost by it, but it is the one place a user coming from
  Excel has to learn a different route to something they reach for daily.
- **A filter-only drop-down with the commands elsewhere.** Sorting from the same drop-down is
  half of what Excel's is used for.

## Open

- The letters: Excel's accelerators (S, O, C, E) and whether the built-in labels show them.
- "Clear filter" as a command, as Excel's "Clear Filter From", in place of the panel's Clear.
- "Add current selection to filter", and a condition filter as rich as Excel's.

## Consequences

- **The Definition of Done's menu rows change with it**: the built-in menu's item list (FN-18)
  loses "Filter", and KB-29 to KB-31 are read over the one popover.
- **ADR-0036's Context Menu is unaffected.** It is the command list over a selection, with no
  filter under it.
