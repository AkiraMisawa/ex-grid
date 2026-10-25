# Shared cases

The cases of ExGrid's pure logic, written once as data: an input and its expected result
([ADR-0350](../../docs/adr/0350-exgrids-core-moves-to-typescript-and-react-and-blazor-both-take-it.md),
section 5). xUnit runs them against the C# core (`tests/ExGrid.Tests/Cases/`) and Vitest against the
Script Core (`src/npm/core/test/`). A case is green on the C# core before the Script Core takes it, so
the cases are the oracle of the move; when the C# logic is removed, its runner goes with it and the data
stays.

A runner never fills in a key a case leaves out: every key a gesture's table row names is written, and
a missing one fails the case, where a default would let a misspelt key pass on a value nobody chose.
The one exception is said where it applies (`times`).

A case says what the grid does, not how a language spells it: "no Focus" is `null` here, where C#
throws and TypeScript answers `undefined`, and a refusal is named by its kind, not by an exception type:

| Refusal | Meaning | C# | TypeScript |
|---|---|---|---|
| `outOfRange` | An argument outside what the grid has | `ArgumentOutOfRangeException` | `RangeError` |
| `argument` | An argument of a kind the member does not take | `ArgumentException` | `TypeError` |
| `invalidOperation` | An operation the state does not allow | `InvalidOperationException` | `Error`, neither of the above |

A number JSON cannot hold is written as a string: `"Infinity"`, `"-Infinity"`, `"NaN"`.

## Files

One directory per area, one file per group of cases. Each file is an object:

| Key | Meaning |
|---|---|
| `about` | What the file covers, and where its cases came from |
| `kind` | Which runner reads it: `selection`, `range`, `edgeBand`, `held`, `calls` |
| `extent` | `[rows, columns]`, the grid every case of the file runs against unless it names its own |
| `cases` | The cases. Each has a `name`, which starts with the ADR it holds to (CLAUDE.md, "Put the ADR number in the test name") |

## Positions

A cell is `[row, column]`, both from 0, or Excel's A1 notation (`"B2"` is `[1, 1]`). A range is
`[topRow, leftColumn, rowCount, columnCount]`, or A1 (`"A1:C3"`, `"C2"`). A list of ranges is an array
of them, or Excel's `Selection.Address` (`"A3:C3,C2,A2,A1:C1"`), the ranges in their order.

## `selection`

A case is a list of `steps`, applied in order to a selection that starts empty. A step is one gesture,
or `expect`, `save` or `restore`.

| Step | Gesture |
|---|---|
| `{"click": cell}` | Click |
| `{"extendTo": cell}` | Shift+click, and each move of a drag |
| `{"toggleRange": cell}` | Ctrl+click |
| `{"toggleColumn": column, "focusRow": row}` | Ctrl+click on a Column Heading |
| `{"toggleRow": row, "focusColumn": column}` | Ctrl+click on a Row Heading |
| `{"move": direction}` | Arrow (`"up"`, `"down"`, `"left"`, `"right"`) |
| `{"extend": direction}` | Shift+arrow |
| `{"moveToEdge": direction, "answer": cell}` | Ctrl+arrow; `answer`, when given, is the Consumer's edge answer for any cell |
| `{"extendToEdge": direction, "answer": cell}` | Ctrl+Shift+arrow, likewise |
| `{"moveByViewport": rows}` | PageDown (positive) or PageUp |
| `{"extendByViewport": rows}` | Shift+PageDown or Shift+PageUp |
| `{"selectAll": true}` | Ctrl+A |
| `{"selectAll": {"firstRow": row, "rowCount": rows}}` | Ctrl+A under a pager |
| `{"selectWholeColumns": true}` | Ctrl+Space |
| `{"selectWholeRows": true}` | Shift+Space |
| `{"cycle": key}` | `"enter"`, `"shift+enter"`, `"tab"` or `"shift+tab"` |
| `{"nextCorner": true}` | Ctrl+. |
| `{"collapseToFocus": true}` | Shift+Backspace |
| `{"focusOn": cell}` | A Find step inside the selection |
| `{"extendToColumn": column, "focusRowIfEmpty": row}` | Shift+click on a column header |
| `{"extendToRow": row, "focusColumnIfEmpty": column}` | Shift+click on a Row Heading |
| `{"place": range, "focus": cell}` | A Consumer's placement |
| `{"selectColumn": column, "focusRow": row}` | Click on a column header that selects |
| `{"selectRow": row, "focusColumn": column}` | Click on a Row Heading |

`cycle` and `nextCorner` take `"times"`, how often the key is pressed, and `"focuses"`, the Focus
expected after each press (which also sets how often, when `times` is absent). With neither, the key is
pressed once.

Any gesture takes `"extent": [rows, columns]` for that step alone, and `"throws"`: `"outOfRange"` (an
argument outside what the grid has) or `"invalidOperation"` (a selection that no longer fits, or an
answer that cannot be followed). A step that throws leaves the selection as it was.

`{"save": "name"}` keeps the selection under a name; `{"restore": "name"}` goes back to it.

`{"expect": {...}}` holds the selection to any of:

| Key | Holds |
|---|---|
| `empty` | Whether nothing is selected |
| `ranges` | The ranges, in their order |
| `rangeCount` | How many ranges |
| `rangeAt` | `[index, range]`: the range at that index |
| `includesRange` | A range that is among them |
| `focus`, `extent`, `focusRange` | The Focus, the Extent and the Focus range; `null` for none |
| `cellCount` | The selected-cell count |
| `contains`, `notContains` | Cells inside and outside the selection |
| `coversColumn`, `coversRow` | `[[index, covered], …]` |
| `wholeColumns` | The whole columns, ascending |
| `topLeftOfRangeMadeLast` | That cell, or `null` |
| `sameAs`, `differsFrom` | The name of a saved selection it equals, or does not |
| `unchanged` | Equal to the selection before the step that came before this one |

## `range`

Each case is one of: `{"fromCorners": [cell, cell], "expect": range}`; `{"subtract": {"range": range,
"cell": cell}, "expect": ranges}`; `{"subtractArea": {"range": range, "area": range}, "expect":
ranges}`; `{"create": [top, left, rows, columns], "throws": "outOfRange"}` or with `"expect"` the
range itself; `{"contains": {"range": range, "cells": [[cell, inside], …]}}`; `{"cellCount": range,
"expect": count}`.

## `edgeBand`

Each case is `{"positionPx", "lengthPx", "rowsPerViewport", "expect"}`: the signed rows a tick.

## `held`

A case is `steps` over a held selection that starts empty under `"version"`: `{"click": cell}` replaces
the selection under the same version, `{"under": version}` reconciles with the version now current, and
`{"expect": {"version": n, "empty": bool, "sameAsBefore": bool}}` holds it.

## `calls`

The general kind, for logic that is a value and its members: geometry, metrics, widths. The file names
its `subject`, the type a case makes, and may give `defaults`, arguments every case starts from. A case
makes the subject with `"create": {name: value, …}`, the defaults with its own arguments over them,
named as the Script Core's options object and the C# constructor's parameters both name them; with
`"via": "factory"`, through that static factory instead. A case refused at its creation says so with
`"throws"` (and may add `"saying"`) and checks nothing after it. A case with no `create` checks only
statics.

`"expect"` is a list of checks, each naming one member, as the Script Core spells it (C# spells it with a
capital):

| Check | Reads |
|---|---|
| `{"get": "member"}` | A property of the subject |
| `{"call": "member", "args": [ … ]}` | A method of the subject, with the arguments in order |
| `{"call": "member", "args": {name: value, …}}` | A method taking named arguments: the Script Core's options object, and the C# parameters of those names |
| `{"static": "member"}`, with or without `"args"` | A static property or method of the subject's type; where C# has a static class (`OverflowRules`), the Script Core's functions of that module |

and says what it answers:

| Key | Meaning |
|---|---|
| `is` | The answer: a value, or `null` for none. An object is compared by every key, as it serialises in C# (camelCase) and as its own properties in TypeScript |
| `near` | With `is`, how far a number may be from it, where the arithmetic is not exact in both languages |
| `throws`, `saying` | The refusal, and text its message holds in both languages |
| `then` | Checks on the answer, as the subject of each |

The strings `"Infinity"`, `"-Infinity"` and `"NaN"` are numbers wherever they stand in a `calls` case,
argument or answer: the Script Core's runner has no parameter types to tell text from a number by, so a
case cannot pass or expect them as text, and the C# runner refuses one that passes them to a string.

An argument may be another subject, `{"new": "Subject", "with": {name: value, …}}` (and `"via"`), made as a
case makes one: a `CellTextMetrics` passed to `OverflowRules.decide`, a `GlyphWidthTable` to
`withGlyphWidths`. An array passed where C# takes a tuple, or a list of them, is read element by element.
A case reads what the core's own code reads: in C#, public and internal members, never private ones;
in the Script Core, a module's exports.

The runners find members by name (`tests/ExGrid.Tests/Cases/CallCaseTests.cs` by reflection,
`src/npm/core/test/calls.test.ts` from its table of subjects), so a misspelt member fails the case, as
does a check that says neither `is` nor `throws`, and a case that checks nothing.
