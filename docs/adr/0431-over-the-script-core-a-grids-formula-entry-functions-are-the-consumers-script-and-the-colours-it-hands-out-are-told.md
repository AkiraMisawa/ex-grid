# Over the Script Core, a grid's Formula entry functions are the Consumer's script, and the colours it hands out are told

*Decided with the user on 2026-10-28 in the project thread "ExSheetのBlazor版をTSコアの包みにする" ("Make Blazor's
ExSheet a wrapper over the TS core"). The user first chose to keep the functions in C#, asked asynchronously with
the keys held behind them ("3にして、遅いなと思ったら2かな", "3, and 2 if it feels slow"). Shown where the Script Core
asks them, the user chose the Consumer's own script ("2: JS functions (ScriptFunction)").*

This settles what [ADR-0430](./0430-over-the-script-core-a-pointing-scope-is-held-in-the-browser-and-its-csharp-object-only-names-it.md)
section 3 left to its own decision, applies
[ADR-0420](./0420-a-chart-drawn-in-the-browser-takes-format-codes-and-the-consumers-own-script-in-place-of-a-dotnet-function.md)
section 3 to ExGrid, and adds to the public surface under
[ADR-0201](./0201-the-public-surface-is-what-consumers-chromes-and-the-sibling-products-use.md) rule 3.

## Context

On 2026-10-28:

- **The Razor ExGrid takes five .NET functions of Formula entry** ([ADR-0051](./0051-formula-entry-completion-point-mode-and-the-formula-bar.md),
  [ADR-0057](./0057-references-are-outlined-in-colour-while-a-formula-is-edited.md), the #411 hooks): `PointAt`,
  `ReferenceText`, `CycleReference`, `ReferencesIn` and `CompleteEditorText`. ExSheet hands them to the grid it
  draws; a Consumer that puts a Formula editor of its own on a grid hands them itself.
- **Blazor's ExGrid over the Script Core forwards `CompleteEditorText`** as a question to .NET, since the core
  already takes an answer that comes later. It refused the other four.
- **The Script Core asks the other four synchronously, where the browser decides a key.** `pointAt` is asked inside
  an arrow's `keydown`, and its answer settles whether the browser moves the caret. `referencesIn` is asked on every
  change to the text, to colour the References as they are typed. `cycleReference` is asked on F4, and
  `referenceText` as a press or an arrow writes the Reference. A .NET function cannot be called from there: Blazor
  calls .NET synchronously from the browser only on WebAssembly (ADR-0420's "Considered and rejected").
- **Asking .NET later would rework the core React shares.** The core would stop every arrow, Home and End in the
  editor and move the caret itself, with Shift and Ctrl, when .NET said no, or would ask .NET ahead of time for
  every caret position of each new text and hold all typing until it answered. On Blazor Server each key typed in a
  Formula would wait a round trip.
- **ExGrid's `OutlinedColumns` and `OnReferenceKeyColoursChanged`, and ExSheet's `OnLinkedColumnColoursChanged`,
  were not carried either.** A colour reported by the browser would have to be made in .NET, and `ReferenceColour`
  had no public way to make one (ADR-0430 section 3).

## Decision

### 1. The four synchronous functions are the Consumer's script

On `ExGrid.Script.ExGrid`, `PointAt`, `ReferenceText`, `CycleReference` and `ReferencesIn` keep the Razor
component's names and are each a `ScriptFunction`: a function exported from a JavaScript module the Consumer serves,
as ADR-0420 section 3 has a chart take one. The Script Core calls it as it calls a React Consumer's function: at
once, where it decides, with the same arguments and the same answer.

- **`ScriptFunction` moves to `ExGrid.Data`**, beside `FormatCode`, so ExGrid and ExChart take the one type. It
  has not shipped, so the move is no Change Note of its own; the note that adds it says where it is.
- **The modules load before the grid is made.** The adapter imports each module, once per page, before it mounts
  the core, so no key is decided without the function. A function the grid is first given after it is made is
  refused by name, as a later `PointedAt` is (ADR-0430): the core would decide keys without it until it loaded.
- **What fails is refused by name, never answered another way.** A module that does not load, or an export that is
  missing or is not a function, stands in the grid's place as its notice, and no grid is made. A function that
  throws, or answers what its option does not take, throws an error naming the function, where the core asked it.
- **`CompleteEditorText` stays a .NET function**, asked as it is today: the core takes its answer when it comes, and
  drops one for text the user has typed on from.

### 2. The colours the core hands out cross to .NET as what they are

- `ExGrid.Script.ExGrid` takes `OutlinedColumns` and `OnReferenceKeyColoursChanged`, and `ExSheet.Script.ExSheet`
  takes `OnLinkedColumnColoursChanged`, with the Razor components' types and meanings.
- A colour crosses as its place in the palette, and .NET reads it back through `ReferenceColour.Reported(place)`,
  made for an adapter as `GridSelection.Reported` is (ADR-0201's note of 2026-10-27). It is hidden from a Consumer's
  editor. A Consumer still never chooses a colour (ADR-0057): the core hands each one out, and what a Consumer passes
  to `OutlinedColumns` is a colour it was told.

## Considered options

- **The .NET functions asked asynchronously, the keys held behind each answer** (the user's first choice). Rejected
  once its cost was shown: several pull requests reworking Point, F4 and the colouring in the core React shares,
  and a round trip per key in a Formula on Blazor Server.
- **The .NET functions called synchronously from the browser.** WebAssembly only, as ADR-0420 found for a chart's
  format; on Server the grid would point on one host and not the other, which spine rule 1 refuses.
- **JavaScript source in a string.** Needs `unsafe-eval`, and the code lives in a C# string; rejected in ADR-0420.

## Consequences

- A Consumer moving a Formula editor of its own from the Razor ExGrid writes those four functions in a module,
  once, for both hosts; React's Consumer already writes them in TypeScript.
- ExSheet over the Script Core needs none of them from its Consumer: its Sheet hands them to its grid in the
  browser.
- Layer 3 judges, on both Blazor hosts, that a grid given the four points and cycles with keys typed at once, and
  that a Sheet's colours outline a grid outside any Scope. What a function answers is the Script Core's own tests'.
