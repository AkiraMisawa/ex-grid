# A Blazor pivot's Order Key crosses as the keys .NET answers with its Items

*Proposed on 2026-10-28 in the project thread "ExPivotのBlazor包みを仕上げる" ("Finish ExPivot's Blazor wrapper"), and
put to the user with the alternatives below. Until the user answers, the implementation follows the recommendation.*

This fills in what [ADR-0370](./0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md) section 1's
Blazor ExPivot does with what ExPivot's Razor component takes as .NET objects: a field's Order Key
([ADR-0060](./0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md)), a report source and its Order Key policies
([ADR-0151](./0151-server-pivots-send-report-windows-and-share-the-local-engine.md),
[ADR-0152](./0152-report-apis-may-change-and-remote-reports-recover-their-baseline.md)), and the clock.

## Context

On 2026-10-28:

- **The Script pivot orders Items in the browser.** Its engine, `@ex-grid/pivot-engine`, sorts a field's Items by
  the field's Order Key where the field has one, calling it once for each Item it lays out, and only where the report
  orders the field by its labels (ADR-0060). A Blazor Consumer's Order Key is a `Func<object, IComparable?>`: a .NET
  function the browser cannot call. `ExPivot.Script.ExPivot` refused such a field by name, and told the Consumer to
  declare the order with `ItemOrder`.
- **ExChart met the same question** and answered it in
  [ADR-0420](./0420-a-chart-drawn-in-the-browser-takes-format-codes-and-the-consumers-own-script-in-place-of-a-dotnet-function.md):
  the browser never calls a .NET function while it draws, because a chart asks for values at positions the browser
  chooses, as many as it likes.
- **A pivot's Items are not chosen by the browser.** Every Item the engine orders reaches it in an answer from the
  source, which is in .NET: the Leaf Aggregates (`Aggregate`) and Filter…'s list (`Items`). So .NET holds every Item
  an Order Key can be asked for, at the moment it sends it.
- **The report source and its policies end.** ADR-0370 section 3 ends ADR-0151's server report Windows with
  ExPivot.Engine at step 3: a server answers Leaf Aggregates, and the engine in the browser builds the report. An
  Order Key policy (ADR-0152) names a function a server registered to order a report that server computes.

## Options

- **A. .NET answers each Item's key with the answer that names it** (recommended). The Consumer's C# keeps working.
- **B. A function exported from the Consumer's own JavaScript module**, as ADR-0420 has it for a chart. Exact, but a
  Blazor Consumer writes the order twice, once for each pivot, and the C# Order Key stays refused.
- **C. Refuse the field, as before**, with `ItemOrder` for a fixed order. An order computed from the Item, such as a
  tenor's length in days, cannot be declared.

## Decision (as proposed: option A)

### 1. A field says it has an Order Key, and its keys come with its Items

The field crosses with `orderKey: true`. Each answer that names Items of such a field, an `Aggregate` answer's row and
column Items and an `Items` page, crosses after a line of their keys: `{"orderKeys":[{"field":…,"items":[[item, key], …]}]}`,
a line break, and the answer as PivotJson writes it. Neither text holds a line break of its own, since both are written
without indentation and a line break inside a string is escaped, so the answer is not written into a string and read
twice. An answer that names no Item of such a field crosses as before. The key of each Item is computed in .NET with
the value .NET's own engine hands an Order Key for that Item: a text Item's text, a number's double, a date's
`DateTime`, a Boolean. A Blank and `#NUM!` are ordered by no key, as in both engines, and are not listed.

The browser files each key under the value its engine hands the Order Key for the Item
(`pivotItemValue`, exported by `@ex-grid/pivot-engine`), and with the Source Version of the answer that brought it.
The field's Order Key there answers from what .NET sent. Within one Source Version an Item has one key and a field's
keys one .NET type, whichever answer brought them: a key that contradicts one already answered under that version is
refused by name, not chosen by which answer arrived last (spine rule 6). A key answered under a newer version replaces
the Item's key, since an Order Key may read the data that changed; the pivot asks for a newer version's report only
once the report before it is answered, so the keys of the two newest versions are kept and older ones let go.

### 2. A key crosses as the browser compares it, in .NET's order, exactly

The browser compares two keys of a field as text ordinally, as numbers, as Booleans, or as whole numbers. .NET writes
each key as one of these, in the order its own `CompareTo` puts that type in:

| .NET key | Crosses as |
|---|---|
| `string` | text |
| `bool` | a Boolean |
| `double`, `float`, `Half` | a number, from its round-trip text (`NaN` and the infinities included) |
| integers, `char`, `Int128`, `UInt128`, `BigInteger` | a whole number |
| `decimal` | a whole number of 10⁻²⁸ths, which holds every decimal exactly |
| `DateTime`, `DateTimeOffset`, `TimeOnly`, `TimeSpan` | a whole number of ticks (UTC ticks for an offset) |
| `DateOnly` | its day number |
| `Guid` | a whole number from its bytes in the order `CompareTo` reads them |
| an enum | its underlying value |

A field's keys are of one .NET type, as ADR-0060 has it: within an answer .NET refuses a key of another type than the
field's first, and across the answers of one Source Version the browser does, by the .NET type each key crosses with,
so two types that cross alike, an `int` and a `decimal`, are not compared as one. A key of any other type is refused by name, never
compared another way. Text keys are compared ordinally, as the Script Core compares every text key; .NET's engine
compared them in the current culture, so a Consumer whose text keys differ only by culture sees the ordinal order.

### 3. A failed key is raised where .NET's engine would raise it

The Order Key is called for every Item an answer names, whether or not the report orders by it. What it throws, and a
key the browser cannot hold, are answered as that Item's failure and raised only when the engine asks for the key,
as .NET's engine would have called the function only then: a function that threw is refused as "The Order Key of
Region failed on 'East'.", and a key refused by .NET in its own words, naming the field, is refused in them
(`PivotOrderKeyError` passes through the engine as it is).

### 4. The report source, its policies and the clock stay with the Razor pivot

`ExPivot.Script.ExPivot` takes no `ReportSource` and no `OrderKeyPolicies`: ADR-0370 section 3 replaces the report
source with a source that answers Leaf Aggregates, `PivotSource.Fetch` among them, and a policy names a function on a
server that no longer orders the report. They are not "not carried yet": a Consumer moves to `Source`, and gives a
field the Order Key it needs there. It takes no `Clock` either: the browser paints the Change Highlight and writes a
Stale Report's time by its own clock and time zone, which is the user's on both hosting models; a .NET clock could
only move .NET's notion of time, which the browser does not read.

## Consequences

- A Blazor Consumer's Order Key works unchanged on the Script pivot, and on ExBoard's pivots, which share the crossing
  (`src/Shared/Script/PivotSourceCrossing.cs`).
- An answer from a source with an Order Key grows by a key for each Item it names of a keyed field, no larger than the
  Items themselves. The function is called once for each such Item of each answer, also where the report orders the
  field by a value and asks for no key: an answer does not say how the report will order it, and asking the keys later
  would be a round trip per ordering. An Order Key is held to ADR-0060's contract, a function of the Item's value, so
  calling it more often changes nothing it orders; one that is slow, or calls out, costs that on every answer.
- An Order Key that depends on more than the Item's value, or changes its answers over time, is called again with each
  answer, and the browser orders by the answer it is ordering.
