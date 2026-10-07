# ExPivot.Engine

The pivot engine of ExPivot, with no UI and one dependency, `ExGrid.Data`. It aggregates a
**Snapshot** — the Ex family's immutable data, held in columns — under a **Pivot Layout** — which
fields stand in Filters, Rows, Columns and Values, with each one's settings — into a **Pivot
Report**: the rows, the label columns, the value columns and the header rectangles over them, as
Excel's PivotTable lays them out.

- **Items** are a field's distinct values: text told apart ignoring case and labelled by its first
  spelling, numbers by value, dates by their clock value, a Blank as `(blank)`, ordered as Excel
  orders them, by an **Order Key**, or by a Value Field. A Date column's **year, quarter or month**
  is a field of its own: `2026`, `Qtr3`, `Sep`, ordered by the calendar.
- **Hidden Items** leave their records out of every cell and every total.
- **Aggregations**: Sum, Count, Average, Max, Min, Product, Count Numbers, StdDev, StdDevp, Var,
  Varp. A total is aggregated from its records, never from the totals below it, and money stays
  exact: Integer and Decimal columns are summed as integers, never through a `double`.
- **The three report forms** — Compact, Outline, Tabular — with subtotals at the top or the
  bottom, grand totals, collapsed Items, several Value Fields (**Σ Values**) in rows or columns,
  and Show Values As (% of Grand Total, % of Column Total, % of Row Total).
- **Live data**: a Change Batch moves the Snapshot on, and the answer held for the current question
  is brought up to date from what the batch removed and added.

The engine's behaviour is the specification of ExPivot's semantics (ADR-0060 in the repository):
a server that answers a pivot itself is held to it.

> **This is a prerelease (`0.x`).** ExPivot ships with ExGrid, at the same version; upgrade them
> together. The API may change between prereleases.

## Requirements

- **.NET 10 or newer.** The package targets `net10.0` and depends only on `ExGrid.Data`.

## Install

```sh
dotnet add package ExPivot.Engine --prerelease
```

## Declaring the fields: the standard way

Declare each field once, with a typed accessor and its pivot settings. The declaration makes both
the Snapshot column the records are read into — without boxing a value — and the Pivot Field over
it:

```csharp
using ExPivot.Engine;

record Trade(string Id, string Region, string Desk, string Tenor, DateOnly TradeDate, decimal Pnl, double Price);

var fields = PivotFields.Of<Trade>()
    .Key("Id", t => t.Id)                                        // the Record Key, for live data
    .Text("Region", t => t.Region)
    .Text("Desk", t => t.Desk, itemOrder: ["Rates", "Credit"])   // Excel's custom lists
    .Text("Tenor", t => t.Tenor, orderKey: Tenors.Months)        // ON, TN, 1W … 1Y6M … 30Y
    .Date("TradeDate", t => t.TradeDate, caption: "Trade date")
    .Month("Month", of: "TradeDate")                             // Jan … Dec, by the calendar
    .Number("Pnl", t => t.Pnl, caption: "P&L", format: "#,##0.00")   // decimal: exact
    .Number("Price", t => t.Price);                              // double: Excel's arithmetic

var source = PivotSource.From(trades, fields);                   // or FromAsync, in slices
```

- **A Number** takes `decimal?` (an exact Decimal column), `double?` (a Double column, whose
  non-finite values are `#NUM!`), or `long?` and `int?` (an Integer column, summed exactly).
- **A Date** takes `DateTime?`, `DateOnly?` or `DateTimeOffset?`, held as the clock it shows; pass
  UTC for instants. `Year`, `Quarter` and `Month` declare its parts.
- **null is a Blank**, in every kind; an empty string is a value.
- **An Order Key** is a function from an Item's value to what Items are ordered by, called once per
  Item: ties fall back to the label, an Item it gives no key (null) comes after the keyed ones, a
  function that throws is refused naming the field and the value, and two values with one key stay
  two Items. The engine parses no tenor; the key is yours:

  ```csharp
  static class Tenors
  {
      // Months to maturity: "1Y6M" is 18, as "18M" is, and the two stand side by side.
      public static IComparable? Months(string tenor)
      {
          if (tenor is "ON" or "TN")
              return tenor == "ON" ? -2m : -1m;
          decimal months = 0, number = 0;
          foreach (var c in tenor)
          {
              if (char.IsAsciiDigit(c))
              {
                  number = (number * 10) + (c - '0');
                  continue;
              }
              var unit = c switch { 'W' => 0.25m, 'M' => 1m, 'Y' => 12m, _ => 0m };
              if (unit == 0 || number == 0)
                  return null;   // not a tenor: no key, so after the tenors
              months += number * unit;
              number = 0;
          }
          return number == 0 && months > 0 ? months : null;
      }
  }
  ```

A field known only at run time is declared with an untyped accessor,
`new PivotField<Trade>("Region", PivotFieldType.Text, t => t.Region)`, and
`PivotSource.From(trades, fields)` reads its values boxed. Typed declarations box nothing and are
the standard way.

## A Snapshot of your own

A Snapshot read from a CSV, a `DbDataReader` or an Arrow stream is pivoted as it is:

```csharp
var source = PivotSource.From(snapshot);   // one field per column, captioned and typed by it
var source = PivotSource.From(snapshot,    // or fields named over its columns
[
    new PivotField("Desk", PivotFieldType.Text),
    PivotField.DatePartOf("Quarter", column: "TradeDate", PivotDatePart.Quarter),
]);
```

A field naming a column the Snapshot does not have is refused by name.

## Computing a report

```csharp
var layout = new PivotLayout
{
    Rows = [new PivotFieldPlacement("Region"), new PivotFieldPlacement("Desk")],
    Columns = [new PivotFieldPlacement("Month")],
    Values = [new PivotValueField("Pnl", PivotAggregation.Sum)],
};

var query = PivotQuery.For(layout);                      // what the layout asks; MaxLeaves caps it
var answer = await source.AggregateAsync(query, ct);    // or a refusal: "this layout needs more than 200,000 cells"
var cube = PivotEngine.Cube(query, answer, source.Fields);
var report = PivotEngine.Report(cube, layout,
    new PivotOptions { Culture = CultureInfo.GetCultureInfo("en-US") });

report.Rows[0].Labels[0].Text;                 // the first region
report.ValueAt(report.Rows[^1], 0)!.Text;      // the grand total of January
```

A report row says what it stands for — its role, its Value Field, its Items (its `Key`) and its
labels — and holds no value and no report: a value cell is asked of a report, which computes it when
it is first read and keeps it (ADR-0161).

A layout that changes only how the result is laid out — collapsing an Item, sorting, the form, the
totals, a format, or an Aggregation whose parts the cube holds (Sum and Average share one) — is laid
out from the same cube (`PivotCube.Holds`), with no new question.

In a browser, the cube of a large answer and its report take seconds, so they are made in slices:

```csharp
var cube = await PivotEngine.CubeAsync(query, answer, source.Fields, slicing: null, ct);
var report = await PivotEngine.ReportAsync(cube, layout, options, slicing: null, ct);
```

- **The same cube and the same report** as `Cube` and `Report`, made a piece at a time. The thread
  is yielded whenever a slice of `PivotSlicing.Budget` (30 ms) is spent, so the page keeps painting.
- **Cancelled, they stop at the next yield.**
- **A small answer never reads the clock**, and its task is complete when it returns.
- `PivotReport.HasSameRowsAsAsync` compares the rows of two reports the same way.

Over records in memory, `PivotEngine.Compute(records, fields, layout)` does all of it in one step,
on the calling thread.

## Asking a Pivot Source

A report is computed from the **Leaf Aggregates** a **Pivot Source** answers with (ADR-0066): for
every combination of the row and column fields' Items that has records, the parts each Value
Field's Aggregation is computed from — counts, an exact or `double` sum, the extremes, the product,
the running variance, and only the parts that are asked for. Every subtotal and grand total is
merged from the leaves' parts, which combine exactly.

- **`PivotSource.From`** is the reference: it answers from a Snapshot's columns, in slices that
  yield between them — the pass over the records, and the answer assembled after it — so a browser
  keeps painting; a cancelled question stops at the next slice.
- **`PivotSource.Fetch(fields, features, aggregate, items, details)`** carries the Consumer's own
  transport to a server, which answers the same questions — from the same engine over a Snapshot,
  or from SQL, building its answer with `PivotAnswerBuilder` — and is held to `From`'s answers.
- **Every answer carries its Source Version.** A field's Items (`ItemsAsync`) and the records behind
  a cell (`DetailsAsync`, `PivotReport.DetailsQuery`) are asked for under it, and a source that can
  no longer answer under it refuses, rather than show records that do not add up.
- **`PivotJson`** writes every question and every answer as versioned JSON and reads it back:
  decimals exactly, doubles to the last bit, and the leaves column by column.

## Live data

A source over a Snapshot with a Record Key takes Change Batches (ADR-0067):

```csharp
source.Changed += change => …;   // ask again: the source has moved on to change.SourceVersion

source.Apply(fields.Batch(added: newTrades, changed: amendedTrades, removedKeys: ["T-1042"]));
var answer = await source.AggregateAsync(query, ct);   // brought up to date, not read again
```

- The Snapshot makes the next one, whole or not at all; a refused batch changes nothing.
- The answer held for the current question is brought up to date from what the batch removed and
  added. Counts and an Integer or Decimal sum — integers — are updated by subtraction and addition;
  every other part is recomputed for the leaves the batch touched, from their records. Leaves come
  and go with their Items. Every leaf equals a fresh aggregation of the new Snapshot, to the last
  bit.
- A batch that compacts the Snapshot moves its rows; the next question is then answered afresh.
- A batch applied while an answer is being assembled from the answer held waits until that answer
  is made, and is folded in then: no answer is half a batch.
- **The source answers a field's Items and a cell's records under its current version and the
  versions of its last four answers** (`SnapshotPivotSource.AnswersHeld`), and refuses an older one:
  holding every version would hold every Snapshot a live feed ever made.

### The next report from the last

ExPivot makes a live redraw's cube and report from the ones on screen (ADR-0161). What a source
does for it is public:

```csharp
var asked = query.WithChangedSince(versionOnScreen);   // the version of the answer the asker holds
var answer = await source.AggregateAsync(asked, ct);
answer.ChangedLeaves;   // the leaves that changed since, the leaves made afresh, or null
```

- **An answer may say which of its leaves changed** since the version the question names
  (`PivotAnswer.ChangedLeaves`, `PivotLeafChanges`). The bundled source says it from its fold, or
  that the leaves were made afresh when it could not fold. A server's answer may say it
  (`WithChangedLeaves`; `PivotJson` carries it), and one that does not is compared with the answer
  before it, leaf by leaf.
- **The next cube shares the axis trees and the cells**, and computes again only the cells on the
  changed leaves' paths — a changed leaf's cell, every subtotal above it on both axes, and the grand
  totals — each from its leaves, as a cube built afresh computes it. It takes its own copy of the
  values that change; the cube on screen stays exactly as it was.
- **The next report shares every row whose painted text did not change**, and makes the others
  anew, so a grid repaints only those (ADR-0003). A report row holds no value and no report, so
  sharing it holds no report alive.
- **Some redraws are made afresh**, with every row a new instance and the same result: another
  question or other fields, leaves that came or went, a batch the source could not fold, an order by
  a Value Field or a Show Values As, or so many changed leaves that building afresh is cheaper.

## The layout's rules and its saved form

`PivotLayoutEdits` holds the Field List's rules as functions from one layout to the next: ticking
a field, dropping it on an Area, moving and removing an entry, hiding Items, sorting, collapsing,
and a Value Field's settings. `PivotLayoutJson` writes a layout as versioned JSON and reads it back,
refusing a version it does not know.
