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
| `kind` | Which runner reads it: `selection`, `range`, `edgeBand`, `held`, `calls`, `clipboard`, `query` |
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

## `clipboard`

The copy, paste and fill rules (ADR-0005, ADR-0011, ADR-0014, ADR-0035, ADR-0050 item 3) and a copy's assembly, run by
`tests/ExGrid.Tests/Cases/ClipboardCaseTests.cs` and `src/npm/core/test/clipboard.test.ts`. A case's
`steps` make a selection with the gestures of `selection` (on the case's `extent`, or the file's), and
plan on the selection as it stands:

| Step | Plans |
|---|---|
| `{"planCopy": {"cellCap": n, "withHeaders": true}}` | A copy; both keys optional |
| `{"planPaste": {"source": [rows, columns], "notEditable": [columns] or "every", "spill": true, "plainTextOnly": true}}` | A paste of a block that shape; every key but `source` optional. `spill` declares that a paste may spill, within the extent |
| `{"planFill": {"direction": "down" or "right", "notEditable": …, "cellCap": n}}` | Ctrl+D or Ctrl+R |

and says what it answers, with one of:

| Key | Meaning |
|---|---|
| `refused` | The reason, as the C# enum's member in camelCase (`tooLarge`, `targetNotEditable`); with `column`, the column a `targetNotEditable` refusal names |
| `plan` | Approved, and the plan holds every key given: a copy's `segments`, `orientation`, `totalRows`, `totalColumns`; a paste's `targets`, `source` (`[rows, columns]`), `collapses`, `sourceCells` (`[[cell, [row, column]], …]`, the block cell landing on each) and `outside` (cells no target holds, whose block cell is refused); a fill's `source` (a range) and `shape`, and a paste's keys for its write. `{}` says only that it is approved |
| `throws` | The refusal of a call that is not a decision: a cap below one cell, a shape with no rows, a fill up or left |
| `assemble` | Approved, and the copy's two flavours (ADR-0005) are the step's `text` and `html`, both written; with `plan` too, the plan holds its keys as well |

`assemble` names what the copy reads. A cell displays `d{row}.{column}` and is raw `r{row}.{column}`, and
there is no header row, unless the step says otherwise:

| Key | Reads |
|---|---|
| `display`, `raw` | The text every cell displays, or holds raw |
| `headers` | A header row: `true` for `H{column}`, or the text every header shows |
| `headerRaws` | `{column: raw}`: a header's raw form, for the columns named; the others have none. Without `headers` a raw form is never read, and one read fails the case |

## `query`

The reference implementation's Filter and Sort semantics (ADR-0001, ADR-0023), run by
`tests/ExGrid.Tests/Cases/QueryCaseTests.cs` against `GridQueryEngine` and `src/npm/core/test/query.test.ts`
against the Script Core's `query/query-engine.ts`. The file names its `columns`, each `{"name", "type"}`;
a case gives its `rows`, each an array of one value per column, then any of `filter` and `sorts`, and says
what stays with `keeps` (the positions of the rows kept, in the Query's order) or how the Query is refused
with `throws` and `saying`.

| Key | Holds |
|---|---|
| `filter` | `{"columns": {name: {"clauses": [{"operator", "value", "values"}], "combinator"}}, "opaque"}`; `combinator` and `opaque` optional |
| `sorts` | `[{"column", "direction"}]`, the primary first |

A value is JSON's own: text, `true`/`false`, `null` for a Blank, and a number, which is a double in C# and a
number in the Script Core. `{"bigint": text}` is a whole number held exactly, a long in C# and a bigint in
the Script Core; `{"double": text}` a number JSON cannot hold (`"NaN"`, `"Infinity"`). An operator, a
combinator, a direction or a column's type written as a number is one cast in from outside the vocabulary.
A `clauses` or `values` list or a `combinator` written as `null` is absent, as a Query that came through JSON writes it.

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
| `is` | The answer: a value, or `null` for none. An object is compared by every key, as it serialises in C# (camelCase) and as its own properties in TypeScript; a key one side leaves out is `null` there |
| `near` | With `is`, how far a number may be from it, where the arithmetic is not exact in both languages |
| `atLeast`, `atMost` | Bounds a number holds, inclusive, in place of `is`: where the fact is a bound (a default estimate covers a measured width), not a value |
| `has`, `lacks` | Members a list or set holds, and members it does not, in place of `is`: where the fact is membership (a key claimed or not), not the whole list |
| `throws`, `saying` | The refusal, and text its message holds in both languages |
| `then` | Checks on the answer, as the subject of each |

The strings `"Infinity"`, `"-Infinity"` and `"NaN"` are numbers wherever they stand in a `calls` case,
argument or answer: the Script Core's runner has no parameter types to tell text from a number by, so a
case cannot pass or expect them as text, and the C# runner refuses one that passes them to a string.

An argument may be another subject, `{"new": "Subject", "with": {name: value, …}}` (and `"via"`), made as a
case makes one: a `CellTextMetrics` passed to `OverflowRules.decide`, a `GlyphWidthTable` to
`withGlyphWidths`. With `"after": [{"call": name, "args": […]}, …]` the subject is what those calls answer
in turn, each on the answer before it: an `AutoWidth` that has observed a width, passed to
`resolveWidthPx`. An array passed where C# takes a tuple, or a list of them, is read element by element. A tuple answered is
the array of its elements, as the Script Core answers one. A menu's command is written `{"id", "enabled"}`,
what a menu's keys read of it; a case never runs one. A range, given or answered, is its four numbers, `{"topRow", "leftColumn",
"rowCount", "columnCount"}`, and a Reference colour its place in the palette, a number.
A member of a closed vocabulary, a C# enum and a union of strings in the Script Core, is written as its name in camelCase
(`"stale"`). A case passes a number in its place only to show a value cast in from outside the vocabulary refused, and
then one that names no member: C# would read a small one as the member at that place.
A case reads what the core's own code reads: in C#, public and internal members, never private ones;
in the Script Core, a module's exports.

The runners find members by name (`tests/ExGrid.Tests/Cases/CallCaseTests.cs` by reflection,
`src/npm/core/test/calls.test.ts` from its table of subjects), so a misspelt member fails the case, as
does a check that says neither `is` nor `throws`, and a case that checks nothing.
