# Over the Script Core, a Pointing Scope is held in the browser, and its C# object only names it

*Decided with the user on 2026-10-28 in the project thread "ExSheetのBlazor版をTSコアの包みにする" ("Make
Blazor's ExSheet a wrapper over the TS core"). Shown that the Script Core's Pointing Scope is a browser object, that
a registered grid's rows are read as the table's rows by a .NET function, and that Blazor's ExGrid over the Script
Core takes no `PointedAt` yet, the user chose to keep the Pointing Scope as it is ("今のScopeをそのまま使う", "use
the current Scope as it is") over a Scope type of the Script components' own, or leaving pointing for later.*

This fills in how [ADR-0370](./0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md) section 1's
Blazor adapters carry [ADR-0058](./0058-a-formula-points-across-grids-through-a-pointing-scope.md)'s Pointing Scope,
amends ADR-0058's "On a circuit" for those adapters, and adds to ExGrid's public surface under
[ADR-0201](./0201-the-public-surface-is-what-consumers-chromes-and-the-sibling-products-use.md) rule 3.

## Context

On 2026-10-28:

- **The Razor ExSheet points through `ExSheet.PointingScope`**, a C# object a Consumer makes per group, passes to
  each Sheet, and registers each grid in (`RegisterGrid`, which answers the `GridPointedAt<TRow>` the grid takes as
  its `PointedAt`). The Scope decides in C#: whether a grid is pointed at, what a press writes, which it refuses,
  where an arrow steps.
- **The Script Core has the same Scope in TypeScript** (`PointingScope` in `@ex-grid/sheet`, the counterpart of the
  C# one, member for member). React's Consumer makes one and passes it to its Sheets and grids. A press on a grid
  and the keys typed in the Sheet meet in one page, in the order they were made.
- **Blazor's ExSheet over the Script Core** (`ExSheet.Script.ExSheet`) refused `PointingScope` by name, and
  Blazor's ExGrid over it (`ExGrid.Script.ExGrid`) took no `PointedAt`.
- **A registered grid's row is read as the table's row by the Consumer's function**, a
  `Func<TRow, IReadOnlyList<Value?>>`, and a .NET function cannot be called from the browser where the press is
  decided ([ADR-0420](./0420-a-chart-drawn-in-the-browser-takes-format-codes-and-the-consumers-own-script-in-place-of-a-dotnet-function.md)
  section 1 for the same reason).
- **ADR-0058's "On a circuit" accepts one gap and closes another with a held-key event**, because the Razor
  components decide in C# and a script shared between instances was ruled out.

## Decision

### 1. The Consumer keeps the C# Pointing Scope; for Script components, the browser holds it

A Consumer moving from the Razor components to the Script ones changes the components' types and nothing about
pointing: the same `PointingScope`, the same `RegisterGrid`, the same `ExSheet.PointingScope` and `ExGrid.PointedAt`.

For the Script components the C# `PointingScope` decides nothing. It names one Scope in the page, by an identifier
it makes, and the Script Core's own `PointingScope` is that Scope: one per C# Scope per page, made by the first
Script ExSheet of it to attach. Every press on a registered grid, every arrow, every refusal, the outlines and the
dashes are the Script Core's, as React's are. What crosses to .NET is what the Consumer hears: a refusal
(`OnPointingRefused`).

The Scope is joined in the page through the Shared Script
([ADR-0411](./0411-every-products-blazor-script-imports-the-core-from-one-shared-script-exgrid-ships.md)): it keeps
the Scopes of the page by identifier, and a grid's registration waits there until a Sheet of its Scope brings the
Scope, since ExGrid's script cannot make an object of ExSheet's. A declaration is registered once, by its own
identifier, so a grid attached again does not stand in the Scope twice. Only what the Consumer put in one Scope is
joined, which is ADR-0058's condition on the held-key event, now holding for the whole Scope.

### 2. A registered Script grid sends each row as the table's row

While a Script ExGrid takes a declaration a Scope made, it writes, beside each row it sends, the row read as the
table's row: the Consumer's function, asked in .NET as the row is written, its Values written as `SheetWire`
writes a Linked Table's rows (ADR-0201's note of 2026-10-28). The browser's Scope reads a pressed row's key, and
finds a dashed row, from what crossed. A row the Consumer changes without saying so is as stale here as its
cells are, and the function must be cheap and must not throw, as ADR-0058 already asks of it.

### 3. A Scope is Razor or Script, and a Scope of both is refused by name

A Razor component and a Script one cannot share a Scope: one decides in C# and the other in the page. The Scope
takes the kind of its first member, and a member of the other kind throws `InvalidOperationException` naming both
kinds as it joins: a Sheet as it is given the Scope, a grid as it is given the declaration. Nothing points quietly
half the time.

A `GridPointedAt<TRow>` a Consumer made itself, whose `OnPress` is a .NET function, is refused by Blazor's Script
ExGrid by name for now. So are the Razor ExGrid's other .NET functions of pointing and Formula entry (`PointAt`,
`ReferenceText`, `CycleReference`, `ReferencesIn`, `CompleteEditorText`): each is asked by the browser at a moment
only the browser knows, which is ADR-0420's question for ExGrid, left to its own decision.

`OnLinkedColumnColoursChanged`, and the Script ExGrid's `OutlinedColumns` it feeds, are not carried yet either: the
colour a Formula's Reference wears would have to be made in .NET from what the browser reports, and a public way to
make a `ReferenceColour` is a way for a Consumer to choose one, which
[ADR-0057](./0057-references-are-outlined-in-colour-while-a-formula-is-edited.md) rules out. The
grids of a Scope are outlined by the Scope itself, in the page, as section 1 says; a grid outside any Scope waits on
that question.

*Settled on 2026-10-28 by [ADR-0431](./0431-over-the-script-core-a-grids-formula-entry-functions-are-the-consumers-script-and-the-colours-it-hands-out-are-told.md).*
`PointAt`, `ReferenceText`, `CycleReference` and `ReferencesIn` are the Consumer's script on the Script ExGrid, and
`CompleteEditorText` a .NET function asked as before. The colours cross as places in the palette, read back through
`ReferenceColour.Reported`, so `OutlinedColumns`, `OnReferenceKeyColoursChanged` and `OnLinkedColumnColoursChanged`
are carried. A `GridPointedAt<TRow>` of the Consumer's own is still refused.

### 4. What ExGrid's surface gains

A `GridPointedAt<TRow>` says where it was made: `GridPointedAt<TRow>.InBrowser`, a `GridPointedInBrowser<TRow>`,
names its Scope, its own identifier, the Linked Table, the grid columns that are other table columns, and the
function that writes a row as the table's row, and is told which kind of grid took the declaration. `RegisterGrid`
fills it in; a Consumer's own declaration has none. Both are contract under ADR-0201 rule 3: what a sibling, or a
third party's adapter over the Script Core, needs to carry a Scope's declaration.

### 5. "On a circuit" no longer applies to the Script components

ADR-0058's two gaps come from deciding in C#. With the Scope in the page, a grid is pointed at in the same turn the
Sheet's edit enters Point, and a press writes in the turn it is made, so neither the accepted gap nor the held-key
event is needed between Script components, on a circuit or on WebAssembly. The Razor components keep both, as
written, while they stand.

## Considered options

- **A Scope type of the Script components' own** (`ScriptPointingScope`). Leaves the C# Scope as it is, but every
  page that moves changes its Scope's type and its registrations as well, against ADR-0370's "change the type".
- **The C# Scope deciding for Script components too.** Mixing would work, but a press would reach the Sheet a
  round trip after it was made, and the keys typed meanwhile would be the race ADR-0058's held-key event exists
  for, rebuilt across two Script Cores. Spine rule 6 rules out narrowing it.
- **Pointing left for later.** Leaves the Razor ExSheet standing longer for a feature Excel users use constantly.

## Consequences

- `ExSheet.Script.ExSheet` takes `PointingScope` and `OnPointingRefused`; `ExGrid.Script.ExGrid` takes `PointedAt`
  from a Scope, set as the grid is made, as a Scope's grids are registered once, as the page is made.
- The Razor ExGrid tells a declaration it took it, so a Scope already joined in the browser refuses it.
- Layer 3 judges, on both Blazor hosts, what Blazor touches: a Script Sheet and a Script grid joined through one C#
  Scope, a press writing the key the .NET function read (ADR-0370 section 11). A Scope of both kinds refused is
  judged in layer 2, and what a press or an arrow writes by the Script Core's own tests.
