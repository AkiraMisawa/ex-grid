# Shared cases

The cases of ExGrid's pure logic, written once as data: an input and its expected result
([ADR-0350](../../docs/adr/0350-exgrids-core-moves-to-typescript-and-react-and-blazor-both-take-it.md),
section 5). xUnit runs them against the C# core (`tests/ExGrid.Tests/Cases/`) and Vitest against the
Script Core (`src/npm/core/test/`); the `chart` area is ExChart's layout, run against `ExChart.Engine`
(`tests/ExChart.Engine.Tests/Cases/`) and `@ex-grid/chart` (`src/npm/chart/test/`)
([ADR-0370](../../docs/adr/0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md), section 2). A case is green on the C# core before the Script Core takes it, so
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
| `chartRefused` | A chart that cannot be drawn honestly (`chart` only) | `ChartRefusedException` | `ChartRefusedError` |

A number JSON cannot hold is written as a string: `"Infinity"`, `"-Infinity"`, `"NaN"`.

## Files

One directory per area, one file per group of cases. Each file is an object:

| Key | Meaning |
|---|---|
| `about` | What the file covers, and where its cases came from |
| `kind` | Which runner reads it: `selection`, `range`, `edgeBand`, `held`, `calls`, `clipboard`, `query`, `source`, `fetch`, `displayText`, `operand`, `find`, `replace`, `summary`, `arrowRead`, `arrowShape`, `arrowWrite`, `changeBatch`, `chart`, and ExPivot's (`pivot/`) |
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
| `headingMarks` | The Heading Mark's runs, each `[start, count]`: `columns` and `rows` it marks, and `wholeColumns` and `wholeRows` it covers whole under the case's extent, or under `under`, `[rows, columns]`, when it names another (ADR-0008) |
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
the Script Core; `{"double": text}` a number JSON cannot hold (`"NaN"`, `"Infinity"`). A date is written as its date type: `{"dateTime": text}` is a
`DateTime` in C# and a `Temporal.PlainDateTime` in the Script Core, `{"dateOnly": text}` a `DateOnly` and a
`Temporal.PlainDate`, `{"dateTimeOffset": text}` a `DateTimeOffset` and a `Temporal.ZonedDateTime` at that
offset, and `{"jsDate": text}` a `DateTimeOffset` and a JavaScript `Date`. A column may declare its
`dateType`, as C#'s `ColumnInfo` and the Script Core's `QueryColumn` do. An operator, a
combinator, a direction or a column's type written as a number is one cast in from outside the vocabulary.
A `clauses` or `values` list or a `combinator` written as `null` is absent, as a Query that came through JSON writes it.

## `source`

A Grid Source taken through what the grid does to it (ADR-0001), run by
`tests/ExGrid.Tests/Cases/SourceCaseTests.cs` against `GridSource.From` and `src/npm/core/test/source.test.ts`
against the Script Core's `GridSource.from`. The file names its `columns` as a `query` file does, and a case
may name its own. A case gives its `rows`, values written as a `query` case writes them, and `steps` taken in
turn. A row is an object of its own, and stands for its place in `rows`: a Window is read as those places,
and a row replaced keeps its place.

A case that names a `rowKey`, a column whose value is each row's Row Key, makes a live source (ADR-0141), whose
clock is a fake one only `advance` moves; `fromThrows` and `fromSaying` say that making it is refused. A live
case reads its Window by key (`keys`), since a row it hands over is new and stands for no place. A row a live
step hands over is an entry: its values, a new object, or `{"same": key}`, the object last handed under that
key, the rows' own to begin with.

A row of `rows` written `{"sameAs": place}` is the object at that earlier place again, one row by identity
(ADR-0003), and reads as that place.

| Step | Does |
|---|---|
| `{"setColumns": true}` | Binds the columns; `"rebuilt"` binds new objects, each equal to the one it was built after; a list of names binds those alone |
| `{"setSorts": [{"column", "direction"}]}` | Applies a Sort |
| `{"setFilter": filter}` | Applies a Filter, `null` for none |
| `{"replaceRow": place, "with": [values]}` | Replaces the row at that place with a new one; `"with": "itself"` hands it back as its own replacement, and `{"stranger": [values]}` in place of a place names a row the source does not hold. In a live case, `{"window": i}` names the row at position `i` of the Window, and `{"same": key}` the object last handed under that key |
| `{"apply": {"added", "changed", "removedKeys"}}` | Hands over a Change Batch: entries added and changed, keys removed; every member optional |
| `{"replaceAll": [entries]}` | Hands over a whole new list |
| `{"gatherInterval": ms}` | Sets how long changes are gathered |
| `{"advance": ms}` | Moves the fake clock on, running what falls due |
| `{"publishGathered": true}` | Publishes what was gathered at once |
| `{"cellChangedAt": true}` | Reads the source's Change Highlight, which starts its recording; `changedAt` reads through what was read |
| `{"changeTimesKeptFor": ms}` | Sets how long a change time is kept |
| `{"markRow": place, "marked": true}` | Marks or unmarks one row (ADR-0043), the row named as `replaceRow` names it |
| `{"markAll": true}` | Presses the Mark Column's header to mark (`true`) or unmark the result, under the Row Sequence Version in force, or under `"version": n` |
| `{"markPositions": [{"start", "count"}]}` | A positional gesture over those rows of the result, under the version in force or `"version": n` |
| `{"rowKind": {"column", "kinds": {text: kind}}}` | Hands the marks a Row Kind: a row whose value in that column reads as one of the texts is of its kind (`"group"`, `"total"`), every other row `"detail"` |
| `{"find": request, "is": [row, column]}` | A Find step through the source (ADR-0055), the request written as a `find` case writes it and read under the version in force; `"is": null` for nothing found |
| `{"replace": request, "answers": {…}}` | A Replace All through the source (ADR-0332 §3), the request written as a `replace` case writes it, under the version in force unless it names its own. The write sets a text column's text, and a number column's number where the text reads as one (an optional `-`, digits, and an optional `.` and digits), refusing it as `not a number` otherwise. With `"entryPrefix": {column: prefix}`, a cell of that column is matched by an entry the editor would open with, the prefix and the value's invariant text, and the write takes the prefix off again; with `"applyDuringWrite": batch`, the first write hands over that Change Batch first, as a feed would while the Consumer plans. `answers` holds any of `count`, `version`, `refused` and `changes` as a `replace` case's `is` does, `changes` a count here, and `applied: true`, that every rewritten row is in the Window by identity. The rows a Replace All rewrites keep their places |
| `{"summarize": request, "summary": {…}}` | A Selection Summary through the source (ADR-0130), the request written as a `summary` case writes it and read under the version in force, and what it shows as a `summary` case's `is` writes it, with `texts` as there. Numbers in a source are written as bigints here, which both cores hold exactly: the C# source holds a JSON number as a double, which it sums in Excel's arithmetic |
| `{"needRange": {"start", "count"}}` | Says the grid is about to show those rows |
| `{"rows": {"start", "count"}, "is": [places]}` | Reads rows of the result |
| `{"distinctValues": column, "is": [values]}` | Reads a column's value list; `"tooMany": true` in place of `is` |
| `{"expect": {…}}` | Reads the source: `window` (places), `values` (the Window's rows as values), `windowStart`, `totalCount`, `loading`, `version` (the Row Sequence Version), `sorts`, `filter`, and `heard`, the changes heard since the last `expect`; and of a live source `keys` (the Window's Row Keys), `windowKept` (whether the Window is the same object as at the last `expect`), `vouches` (that no Window holds a row twice), `handed: true` (every row of the Window is the object last handed under its key), `gatherInterval`, `changeTimes` (whether the source has a Change Highlight), `changeTimesKeptFor`, and `changedAt`, `{key: {column: ms}}`: when the cell of the Window's row under that key last changed, in milliseconds from the case's start, `null` for none. Of its Row Marks: `marked`, the places whose row is marked, ascending; `windowMarks`, whether each row of the Window is marked; `markCounts`, `[markedInResult, rowsInResult, markedOutsideResult]`; `markHeader`, the header's state (`"none"`, `"some"`, `"all"`); `markedRows`, the rows an action runs over, in the source's order, as values, or under a Row Key as keys; and `marksHeard`, the changes the marks reported since the last `expect` |

A step that is refused says so with `throws` and `saying`, as a `query` case does.

A column a `source` file declares may name a `format`, which both runners know by name: `whole` shows a
number rounded to a whole one, as C#'s custom format `0` writes it (the cases hold no negative half, where
JavaScript's `Math.round` and C# part).

## `fetch`

`GridSource.fetch` (ADR-0025), the source over a result a server holds, run by
`tests/ExGrid.Tests/Cases/FetchCaseTests.cs` against `GridSource.Fetch` and `src/npm/core/test/fetch.test.ts`
against the Script Core's `GridSource.fetch`. The file names its `columns`, by name alone: the server, not
the source, reads them. A row is a text, `Row n` for the row at position n of the result. A case gives the
`readAhead` the source is made with, whether a listener `hears` failed fetches, and may give `rowKey: true`,
each row its own Row Key, and `distinct`, the values the Consumer's value list answers with. The server
answers by hand: a fetch the source asks waits until a step answers it, by its place among the fetches asked
so far, from 0. Each step is read once the promises it set going have run.

| Step | Does |
|---|---|
| `{"bind": true}` | Binds the columns |
| `{"setSorts": [{"column", "direction"}]}` | Applies a Sort |
| `{"setFilter": filter}` | Applies a Filter, `null` for none |
| `{"requery": true}` | Says the Query's meaning changed outside its Sort and Filter (ADR-0343) |
| `{"dispose": true}` | Disposes the source |
| `{"needRange": [start, count], "as": name}` | Says the grid needs those rows, and names the promise it is answered with; `"throws"` in place of `as` for a range refused as it is asked |
| `{"rows": [start, count], "as": name}` | Reads rows for a copy (ADR-0005), and names the promise |
| `{"answer": i, "page": {"start", "count", "total", "orderToken"}}` | Answers fetch `i` with a page of `count` rows from `start`, or the `rows` it lists, of a result `total` rows long, with the server's order token if any |
| `{"fail": i, "error": text}` | Fails fetch `i` with that message |
| `{"settled": name}` | The named promise has settled, and was fulfilled; with `throws` and `saying` it was refused as a `query` case says, and with `"rejects": text` it rejected with the failure of that message |
| `{"pending": name}` | The named promise has not settled |
| `{"same": [name, name]}` | Two ranges asked for were answered with one promise |
| `{"read": name, "is": [rows]}` | The rows the named read answered with |
| `{"asked": i, "range": [start, count], "sorts": [...], "filtered": [columns]}` | What fetch `i` asked: its range, its Sort, and the columns its Filter names |
| `{"distinctValues": column, "is": [values], "handed": [columns]}` | A column's value list, and the columns the Filter handed to the Consumer's value list names; `"tooMany": true` in place of `is` |
| `{"expect": {…}}` | Reads the source: `calls`, the range of every fetch asked so far; `cancelled`, whether each was called off; `windowStart`, `windowCount`, `first` (the Window's first row), `totalCount`, `loading`, `version`, `sorts`, `filter`; `lastError`, `null` or a text its message holds; `reported`, texts the messages of the failures heard since the last `expect` hold, in order; `vouches`; and `heard`, the changes heard since the last `expect` |

## `displayText`

The text a value cell displays (ADR-0006, ADR-0350 section 16), run by
`tests/ExGrid.Tests/Cases/DisplayTextCaseTests.cs` against `ColumnInfo.TextFor` and
`src/npm/core/test/display-text.test.ts` against the Script Core's `textFor`. A case lists `texts`, each a
`value` written as a `query` case writes it, an optional `format` as a `source` column names one, and the
text it `is`.

## `operand`

What a filter condition's typed value reads as under a culture, and the text a value reopens in (ADR-0023,
ADR-0370 section 10): C#'s `FilterPanelChoices.ReadOperand`, `OperandText` and `RefusalText`, and the Script
Core's `chrome/operand-reading.ts`. Every case names its `culture`, a BCP 47 tag or `invariant`.

| Field | Meaning |
|---|---|
| `type`, `dateType`, `text` | A reading: the column's type (and a Date column's date type, `dateTime` unless given), and the text typed |
| `reads` | The value it reads as, written as a query case writes a value, a number as `{"number": decimal text}`; `null` for no value (empty text) |
| `refused` | Or the refusal's kind: `notReadable`, `readsTwoWays` or `notTheColumnsDateForm` |
| `readings` | For `readsTwoWays`, the two readings: a number's in the culture and as the invariant culture reads it, a date's month first and day first |
| `read` | For `notTheColumnsDateForm`, the date the text read as |
| `says` | The built-in refusal's words, whole |
| `operandText`, `is` | A reopening: the value, and the text it reopens in |
| `readsBack` | The column type the text is read back as, to the same value |

## `find` and `replace`

The reference Find step (ADR-0055) and the reference Replace All (ADR-0332), the rules `GridSource.from`
answers with and a remote source is held to, run by `tests/ExGrid.Tests/Cases/FindCaseTests.cs` against
`GridFind.Step` and `GridReplace.Plan` and by `src/npm/core/test/find.test.ts` against `findStep` and
`planReplace`. The file names its `columns` and its `rows`, each row the texts of its cells in that order:
what a Find matches, the displayed text, or what a Replace matches, the entry. A request is written
`{"text", "from", "backward", "matchCase", "wholeCell", "scope", "columns"}`, and a Replace All's adds
`"replacement"` and `"editable"`; a flag left out is off, `columns` and `editable` are the file's columns,
`from` and `scope` none.

A `find` case lists `steps`, each `{"find": request, "is": [row, column]}`, `null` for nothing found.

A `replace` file also names its `numberColumns`, whose write refuses a text that does not read as a number
(an optional `-`, digits, and an optional `.` and digits) as `not a number`; every other write sets the
text. A case may give its own `rows`, repeat them `repeat` times, and read them under the version `readIn`
(0 unless given; the request's `version` is 0 unless it names one). It says what the plan `is`: any of
`count`, `version`, `refused` (`[row, column, message]`, `null` where the column is not Editable),
`changes` (`[row, values]`, each rewritten row by its place and its new texts) and `changeCount`; or how it
is refused, with `throws` and `saying`. `"writeInPlace": true` makes a write that hands back the row it was
given.

## `summary`

The reference Selection Summary (ADR-0130) and the exact arithmetic it sums in (ADR-0350 section 16), run by
`tests/ExGrid.Tests/Cases/SummaryCaseTests.cs` against `GridSummary.Of` and by
`src/npm/core/test/summary.test.ts` against `summaryOf`. The file names its `columns`, and its `rows`, each
the values of its cells in that order, written as a `query` case writes them; a case may give its own of
either, and repeat its rows `repeat` times. A number is read as the Script Core reads one: a JSON number as C#
converts a double to a decimal, a double only where no decimal holds it, and `{"bigint": text}` exactly, as
an integer of any width.

`ask` is the request, `{"ranges", "columns", "figures"}`: the ranges as a list of ranges, the file's columns
unless it names its own (a name the file does not have is a column with no value, an Action Column), and the
figures by name (`average`, `count`, `numericalCount`, `min`, `max`, `sum`). `is` names every figure the
answer holds, and no other, each `"empty"`, `{"exact": text}` (the decimal, written without trailing zeros),
`{"double": number}` (a figure in Excel's arithmetic, a string where JSON cannot hold the number) or
`{"error": name}` (`divideByZero`, `notANumber`, `errorAmongValues`). `texts` gives the text a figure is shown
with where no format applies.

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

## `chart`

ExChart's layout (ADR-0370, section 2): scales, ticks, Thinning, each Series Kind's steps and gaps, the
scene and its legend, and the 3D surface, its slices and its mesh. Every file of `chart/` is of this kind, run by
`tests/ExChart.Engine.Tests/Cases/ChartCase.cs` and `src/npm/chart/test/chart-case.ts`. A file may say
`near`, how far a number may be from what a check expects in every case of the file, and `definitions`,
named definitions a case refers to by their name.

A case names one operation, or several under `"build": {name: operation}`, each answer then read under
its name. An operation is one key, beside a `definition` or a `surface` where it reads one; with none, the
answer is the definition or the surface itself:

| Operation | Answers |
|---|---|
| `"scene": {metrics, view, style, independentScales, legend}` | The laid-out chart (`ChartScene.Build`, `buildScene`) |
| `"drawing": {axis, series, from, to, pixels, quotes}` | What one series draws, in the axes' units |
| `"thin": {drawing, toPixel, pixels}` | The drawing thinned to the pixels, and what each thinned pixel held |
| `"through": {source, drawn, kept}` | A Chart Source's thinned pixels carried through a drawing's own (ADR-0344) |
| `"tenor": years`, `"nice": [min, max(, target)]`, `"steps": [min, max, step]`, `"decades": [min, max]`, `"logSteps": [min, max]` | The tick arithmetic; `nice` answers `[min, max, step]` |
| `"fit": {ticks, angle, map, textWidth, lineHeight, gap}`, `"horizontal": {axis, style, from, to, points, map, textWidth, lineHeight}` | The horizontal ticks, a label left out where it would touch its neighbour |
| `"candles": {series, from, to, period}` | A Price Range's candles merged into a period |
| `"legendMembers": key` | The series a legend entry stands for |
| `"appearance": {colourSlots}` | `[looks by series, whether colours repeat]` |
| `"format": {format, value}` | A number written as the engine writes it with that .NET format (`0.####`, `0.##`, `R`, `G3`, `N0`) |
| `"mesh": {}` | What the surface's 3D entry draws (`SurfaceMesh.Of`, `surfaceMesh`) |
| `"slice": {axis, value}`, `"slicePath": {axis, value}` | A slice as a 2D chart (`null` where the surface holds no such slice), and its line on the surface |

A definition, an axis and a series are written as the Script Core's options objects name them, each key
optional where the options' is; a series that leaves out `kind` declares none. A position is a number or
`{"date": "yyyy-MM-dd"}`, read as its day number from 0001-01-01. A value is a number or `null`, a gap. A
point is `[x, y]`, `[x, y, name]`, `{x, y, name, row}`, or a generated run, `{"generate": {count, x:
{from, step}, y, name, at}}`, whose `y` is a number, `{"line": [a, b]}` (a + b·i), `{"sin": d}` (sin(i/d))
or `{"mod": m}`, and whose `at` sets the values of single points by index. A `sampler` is `{from, to,
value}`, `value` `{"line": [a, b]}` or `{"split": [at, below, from]}`, and the positions it was asked at
are recorded under the series' id. Rows of a Price Range are `[x, open, high, low, close(, volume)]`, or
`{"weekdays": {count, from, volume, withoutOpen, set}}`, a fixed walk from 100 on weekdays from Monday
2026-01-05. A map from a position to a pixel is `{scale | over, offset}`, and a text width
`{"perCharacter": n}`.

A surface is one of four forms, each with `zTitle`, `zFormat`, `zScale` and `centre` where it names them:
a grid, `{x, y, z, method, interpolated, notes}`, whose axes are `{title, values, scale, format}` and
whose `z` is its rows, one a `y` value; points on a grid, `{points: [[x, y, z]], xTitle, yTitle}`; a
Consumer's triangles, `{nodes: [[x, y, z]], triangles: [[a, b, c]], xTitle, yTitle, xScale, yScale,
notes}`; or a sampled function, `{sampled: {method, x: [from, to], y: [from, to], z, samples, xScale,
yScale}, xTitle, yTitle}`, whose `z` is a number or `{"plane": [a, b, c]}` (a + b·x + c·y). An axis'
value is a string or a number. A format is `{prefix, suffix, places}`, the value written with `places`
fixed places or, without them, invariantly.

`expect` is a list of checks. A check reads one thing, transforms it, and asserts:

| Key | Meaning |
|---|---|
| `at` | A path: dot-separated, from the check's scope, or from the whole answer when it starts `$.`. A segment is a property, an index (negative from the end), `length`, or `[key=value]`, the one element of a list whose key holds that value |
| `yOf: [panel, side, value]`, `positionAt: pixel` | The height of a value on a panel's vertical axis, or the position under a pixel across, of the scene `of` names (the case's own by default) |
| `asked` | The positions a sampled series was asked at |
| `where`, `sortBy`, `map`, `differences`, `sum`, `count` | Applied in that order: the elements matching an object, sorted by a path, each element's path, the gaps between neighbours, their sum, how many there are |
| `is`, `isNot` | The value, compared deeply, numbers within `near` |
| `above`, `below`, `atLeast`, `atMost` | Bounds, against a number or a reference `{at \| yOf \| positionAt, of, times, plus}`, worth v × times + plus |
| `sameAs`, `differsFrom` | Another path's value |
| `has`, `lacks`, `mentions` | An element matching an object, or text a string holds; a string element holding the text |
| `empty`, `evenlySpaced`, `startsWith` | A list with nothing in it; numbers with equal gaps; a string's start |
| `each`, `then` | Checks on each element, or on the value, as their scope |

An answer is compared as the JSON it serialises to: in C#, its public properties in camelCase, an enum by
its camelCase name and a tuple as an array; in TypeScript, its own properties and a class's getters, a map
as an object and a bigint as its number. A property the Script Core leaves out where it is absent reads as
`null`, as C#'s does.

## `arrowRead`, `arrowShape` and `arrowWrite`

The Snapshot as Apache Arrow ([ADR-0065](../../docs/adr/0065-a-snapshot-travels-as-apache-arrow.md), ADR-0370
section 3), under `arrow/`: xUnit runs them against ExGrid.Data.Arrow
(`tests/ExGrid.Data.Arrow.Tests/Cases/ArrowCaseTests.cs`) and Vitest against `@ex-grid/data`
(`src/npm/data/test/arrow-cases.test.ts`). These cases are not the Script Core's: they hold the two
Snapshot packages to one reading of the same bytes, and both stay.

| Directory | Holds |
|---|---|
| `streams/` | Arrow streams and files written by pyarrow, a third reader and writer, from `make_streams.py`: each type of ADR-0065's table, each type outside it, each refused value, and bytes cut short or not Arrow at all |
| `producers/` | Streams other producers wrote (pyarrow, Polars, DuckDB), from `producers/make_streams.py` |
| `written/` | What `@ex-grid/data` writes for each `arrowWrite` case, which the C# runner reads back. `EXGRID_WRITE_ARROW_CASES=1 npx vitest run` in `src/npm/data` writes them; otherwise the Vitest runner holds the writer to them byte for byte |

An `arrowRead` case names its bytes, `"stream": "streams/…"`, and says either what they read as or how
they are refused:

| Key | Meaning |
|---|---|
| `expect` | The Snapshot: `version`, `recordKey` (`null` for none), and `columns`, each with its `name`, `caption`, `kind` and `values` (one a row, `null` for a Blank), and a Text column's `dictionary` in the order values first appear |
| `refused` | The refusal: `message`, whole, or `saying`, text it holds where the two packages word the rest apart (a hint naming a .NET or a browser API); and the `row` (1-based) and `column` it names, `null` when it names none, and the Record Key as `key` when it names one |

A value is written by its kind: Text a string, or `{"utf16": "…\\uXXXX…"}` for text no JSON reader takes
(a lone surrogate); Decimal its canonical text (`"-0.0001"`); Double a number, `"Infinity"`, `"-Infinity"`,
or `{"bits": "7ff8000000000000"}`, its IEEE 754 bits, which a double is compared by; Integer a number, or
a string past 2^53; Date `"2026-09-30"`, or `"2026-09-30T12:00:00"` with up to seven places after the
second and none of them trailing zeros; Boolean `true` or `false`.

An `arrowShape` case names a stream and gives its `shape`, each column's `name` and `kind`, or the
`refused` a read of the shape meets. An `arrowWrite` case gives a `snapshot` in the same form as
`expect`, without the dictionary, and either `refused`, or `expect` — each of the written schema's
`fields` with its `name`, Arrow `type` as Arrow's tools print it and `caption` (`null` when none is
written), and the schema's `metadata` whole — and `written`, the path of what `@ex-grid/data` wrote.
Each runner writes the Snapshot, holds the schema to `expect`, reads it back as the Snapshot it was, and
reads `written` the same way.

## `changeBatch`

A Change Batch applied to a Snapshot ([ADR-0064](../../docs/adr/0064-the-snapshot-is-the-familys-immutable-data-held-in-columns.md),
[ADR-0067](../../docs/adr/0067-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md), ADR-0370 section 5), under `data/`: xUnit runs them
against ExGrid.Data (`tests/ExGrid.Data.Tests/Cases/ChangeBatchCaseTests.cs`) and Vitest against `@ex-grid/data`
(`src/npm/data/test/change-batch-cases.test.ts`). These are not the Script Core's either, and both stay. The C# runner
writes the answers: with `EXGRID_WRITE_DATA_CASES=1`, `dotnet test tests/ExGrid.Data.Tests` writes what ExGrid.Data
answers into each batch's `expect`. It runs every case under three storages (one segment; segments of four rows; segments
of four that compact after every batch), and they must answer alike, because how a version is stored is ExGrid.Data's own.

A case gives a `snapshot`, in the form of an `arrowWrite` case's, and its `batches`, each applied to the case's Snapshot,
or with `"on": "previous"` to the version the step before made, or with `"on": n` to the version step `n` made. A batch
names `added` and `changed` records, each `columns` with a `name`, `kind` and `values` and no Record Key (a batch's records
carry no captions: the next version keeps the Snapshot's), and `removedKeys`: a string is a Text key, a number an Integer
key, `{"integer": "…"}` an Integer key past a double's exact integers; a number that is no integer, or `null`, is there to
be refused. A key a batch leaves out is a part it does not have.

Its `expect` is what the batch made: the next version's `version`; `removed`, the rows of the version before it no longer
holds, and `added`, the rows of the next it did not hold, each by its place in its version's order, ascending; and `after`,
each column's `name`, `caption`, `values` and, for Text, its `dictionary`, which keeps the version before's codes and only
grows. A refused batch is `{"refused": {message, row, column, key}}` as an `arrowRead` refusal is, or `{"refused":
"argument"}` where the batch itself cannot be made (`ArgumentException`, `TypeError`). Each runner also holds, at every step,
that the version a batch was applied to still reads as it did, and ADR-0067's fold: the rows the next version holds that
the batch did not bring are the rows of the version before less those it removed, in their order.

## ExPivot's engine, under `pivot/`

The cases of ExPivot's engine (ADR-0370, sections 2 and 5): xUnit runs them against `ExPivot.Engine`
(`tests/ExPivot.Engine.Tests/Cases/`) and Vitest against `@ex-grid/pivot-engine`
(`src/npm/pivot-engine/test/`). The C# runner writes the answers: with `EXGRID_WRITE_PIVOT_CASES=1`, `dotnet
test tests/ExPivot.Engine.Tests` writes what the C# engine answers into each case instead of holding the
engine to it, so a case is written as its input, transcribed from the fact it holds, and the Script Core is
held to the C# engine's answer. Where the two engines word a refusal apart because each names its own
platform's API (`PivotEngine.Aggregate` beside `aggregatePivot`, `Int32` beside `number`), the case says the
Script Core's words in `scriptSaying`, and an answer that differs whole for the same reason is
`scriptExpect`. A key a case leaves out is a parameter not given, which the engine's own default answers on
both sides; nothing else is filled in.

A refusal is `{"refusal": kind, "saying": message}`, the message whole, without .NET's parameter suffix. Besides
the three kinds above, `version` is a document of a version the engine does not read (`NotSupportedException`,
`PivotVersionError`), `format` one that does not read (`FormatException`, `PivotFormatError`), and `snapshot`
a value its column cannot hold (`SnapshotException`, `SnapshotError`). A value is written as it is held: a
string is text, a number a double, `true`/`false` a Boolean, `null` a Blank, and `{"decimal": "100.50"}`,
`{"integer": "7"}`, `{"double": "NaN"}` (or `"-0"`, `"Infinity"`), `{"date": "2026-01-10T13:45:00"}` the rest.
A layout is the layout's JSON without its `version`, and an Item's key is written as that JSON writes one.

### Data sets

`pivot/data/` holds the data a file names in `data` (a case may name its own): `columns`, the names of a
record's values; `fields`, each with its `name` and `type`, and its `caption`, `format`, `itemOrder`,
`datePart` or `orderKey` where it has one, and `of` where it reads another column than its name; and
`records`, a record's values in column order. A case's own `fields` or `records` stand in for the set's. A
set may instead be a Snapshot's: `snapshot`, each column's `name`, `kind` and `caption`, and the column that is
its `recordKey` where it has one; and `rows`, which a case's own `rows` stand in for. A case, or a differential
run, may write the set itself in `data`. The Order Keys a field
names (`tenor`, `tenorButOnIsText`, `tenorThrowsOn7Y`, `absolute`) are the Consumer's functions the C# tests
declared, and note each value they are called with.

### `pivotReport`

A `layout` over the data, laid out under the file's or the case's `locale` (a .NET culture name, or
`invariant`) and `words` (`english`, `japanese`, `unitedKingdom`, or an object of a word by its id), and read
back as `expect`: `headers`, the leaf headers; `lines`, a line a row (its role's letter, its labels indented
two spaces a level with `[-]` or `[+]` before an Item with a button, then `||` and each value as painted);
`spans`, the Header Group spans. `also` asks for more: `names`, `keys`, `numbers` (a double `d…`, an exact
decimal `m…`), `captions`, `empty`, `toggles`, `columns`, `tiers`, `keyCalls`. A case may aggregate another
layout (`cubeOf`) and ask whether its cube `holds` other layouts, what Filter… lists for fields (`itemsOf`),
which records stand `behind` a cell (`[row, valueColumn]`, answered by record index), and whether other
records or another layout make the same rows (`sameRowsAs`). What refuses the case is `throws`.

### `pivotWords`, `pivotGeneral` and `pivotItemKey`

`pivotWords`: every word id, with its English, Japanese and English (United Kingdom) word, `null` where an
edition has none. `pivotGeneral`: the texts of `numbers` under the `locale`, a number with no format of its
own as a PivotTable shows it (ADR-0060). `pivotItemKey`: the key each value of `of` is the Item of, each key
`named` accepted or refused, or whether each of `pairs` names one Item.

### `pivotLayoutEdits` and `pivotLayoutJson`

`pivotLayoutEdits`: a `layout` and its `edits`, each made on the case's layout or, with `"on": "previous"`,
on the step before's. An edit is a Field List gesture or a Layout menu choice: `tick`, `untick`, `place`
(with `area` and `index`), `move` (an entry `{area, index}` or `"values"`, the Σ Values field, with `to`,
`index` and the `field` it is), `remove`, `setHiddenItems` (with `hidden` and `items`), `setSort` (`sort`),
`setSubtotals` (`subtotals`), `setCollapsed` (`item`, `collapsed`), `setFieldCollapsed` (`collapsed`),
`setValueField` (an index, with `value`) and `choose`. Each is answered by the `layout` it makes, `same` where
it hands back the layout it was given, `refused` and the refusal's English `word` where it may be refused,
or a refusal; `menu` also reads the Layout menu's `chosen` and `enabled` choices, and `report` the report
the layout makes. `pivotLayoutJson`: a saved layout `read` and written again, or what refuses it.

### `pivotSource`

A Pivot Source over the data set: over its records, or over the Snapshot its columns make with the fields
in `declare` (each column a field when there is none). `fetch` puts a fetching source in front of it, as a
server's would be: `offers`, the Aggregations it answers, and `answers`, another question it answers in
place of the one asked. Each of `asks` is answered in turn:

| Ask | Answer |
|---|---|
| `{"fields": true}` | Each field's `name`, `caption`, `type`, and its `format`, `column` and `datePart` where it has one |
| `{"aggregate": question}` | The Leaf Aggregates: `leaves`, each axis's `field` and `items`, each `leaf`'s Item at every level (`at`, rows then columns) and `records`, and each value's `parts` and what it carries at every leaf — `values`, `numbers`, `nonFinite`, and `sum`, `min`, `max`, `product`, `mean`, `m2` as asked for, to the last bit. A question is `rows`, `columns`, `filters` (a field by name, or with its `hiddenItems`), `values` (a field, its counts, or with the `parts` it asks for: `sum`, `extremes`, `product`, `variance`) and `maxLeaves`. With `"order": "any"`, each axis's Items are listed in the ordinal order of their kind and value and the leaves in the order of their Items: after a batch that changes records, ExGrid.Data reads rows in its storage's order and the Script Core in the Snapshot's (ADR-0067), and such a step holds what both say |
| `{"apply": batch}` | A Change Batch applied to a source over a Snapshot (keyed by the column its `snapshot` names as `recordKey`, or with no key): its `added` and `changed` records, rows in the data set's columns, and its `removedKeys`. Answered by the Snapshot version it made (`applied`) and the Source Versions the source `told` its listeners of, each written `{version}` where it is the one the source answers under now; or by the Snapshot's refusal (`"refusal": "snapshot"`) and what was told |
| `{"items": field}` | A page of the field's Items, with `search` and `max`: `total` and `items`, under the last answer's Source Version, or `"under"` an earlier answer's (`{"answer": n}`, from 0), the version a source over a Snapshot answers under now (`"current"`), or one no source held (`"stale"`) |
| `{"details": cell}` | A page of the records behind a cell: its `rows` and `columns` path (each a `field` and its `item`), the `hiddenItems` of placed fields, `start` and `count`, under a version as a page of Items is; answered by `start`, `total`, `fields` and `records` |
| `{"report": layout}` | The report the layout makes from the source's answer, read as a `pivotReport` case reads one, `also` included |
| `{"cube": question, "from": question}` | Whether a cube is made of one question from the answer to another |
| `{"asked": true}` | How often the fetching source asked its server for Leaf Aggregates |
| `{"refresh": true}` | The source made again over the same data, which is Excel's Refresh |

A page says `"version": "asked"` when it was answered under the version it was asked under. A source's
refusal is `{"refused": kind, "field", "limit", "saying"}`, the version asked under written `{version}`.


### `pivotReportSource`

A local report source (ADR-0151 to ADR-0153, as ADR-0370 section 4 leaves them) over a Pivot Source of the
data set, made as a `pivotSource` case makes one, with the Order Keys `orderKeys` registers by a policy's name
(`longFirst`, which orders longer text first, and `throwsOnBB`, which fails on `BB`), `versionsKept`, and a
clock that starts at 2026-10-06T12:00Z and moves only when a case says. Versions are written as tokens in the
order the source first named them, `v1`… for Report Versions, `s1`… for Source Versions and `r1`… for row
sequences, in its messages too; a token the source never named (`stale`) is asked as written. Each of `asks`
is answered in turn:

| Ask | Answer |
|---|---|
| `{"window": [start, count]}` | A Window, asked with the `layout`, the `settings` (`locale`, `words`, `changeHighlightMs`, `labelMetrics`, `orderKeyPolicies`), the `locale` and the `words` the step names or the step before named (the case's `settings` before any), `markChanges`, `refreshData`, `maxLeaves`, and the `baseline` it names (`null` for none) or the Window adopted last; with `"discard": true` it is not adopted. Answered by its `version`, `sourceVersion`, `rowSequence` and `rowCount`, the `headers`, a line a row as a `pivotReport` case reads one, the `changeMarks` it lists, each row's marks (`changed`) where a cell has one, its `labelWidths` where the settings give a geometry, and the readings `also` asks for: `keys`, `paths`, `numbers`, `columns` (each value column's `name`, `role`, `valueField` and `path`), `spans`, `captions`, `tiers` |
| `{"apply": batch}` | A Change Batch applied to the source over a Snapshot, as a `pivotSource` case applies one; answered by the Source Version it made (`applied`) |
| `{"advance": ms}` | The clock moved on |
| `{"copy": query}` | A Copy under a `version`, its `columns` (an index among the label columns and then the value columns of the Window answered under it, or a name), its `ranges` (`[top, left, bottom, right]`) and `maxCells`; answered by each block's `headers`, its `headerRaws` where any header has one, and its `rows`, each cell `[text, raw, isNumber]` |
| `{"summary": query}` | A Selection Summary, its `columns` and `ranges` as a Copy's, and its `focus` (`[row, column]`); answered by Count (`values`), Numerical Count (`numbers`), the `sum`, `min` and `max` where a number was selected (`m…` or `d…`), `hasError`, the Focus value's `numberFormat` and the `locale` |
| `{"reportItems": query}` | A field's Items under a `version`, with `search` and `max`: `sourceVersion`, `total` and each Item's `key`, `label` and `isHidden` |
| `{"details": cell}` | The records behind a cell of a Window answered under a `version`: its `row` (its place in the report) and `column` (−1 for its label), `start` and `count`; answered as a `pivotSource` case's page, or by the Pivot Source's refusal |
| `{"detailsEach": version}` | The question for the records behind every cell of that Window, resolved from its rows, and its records: a line a cell, `column: the row's Items / the column's / the Hidden Items -> total: each record` |

A report source's refusal is `{"refused": kind, "field", "saying"}`; what it throws is a refusal as above.
