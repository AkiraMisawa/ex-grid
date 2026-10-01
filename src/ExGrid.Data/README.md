# ExGrid.Data

The Ex family's immutable data. A **Snapshot** is a copy of tabular data at one version, held
column by column: text as a dictionary of codes, money as exact scaled 64-bit integers, dates as
clock values. It has no dependency. The family's bundled sources read it — ExPivot's first — so a
pass over a million rows is a loop over arrays rather than over objects.

- **Six kinds**: Text, Decimal, Double, Integer, Date and Boolean, with a **Blank** possible in each,
  kept apart from `""` and 0.
- **Ways in** that yield a Snapshot or refuse whole, naming the row and the column: the Consumer's
  objects through typed accessors, which box nothing and keep the objects by reference; and columns,
  a value or a span at a time, for data a reader has read itself.
- **Loads in slices**, yielding between them and reporting progress, with a `CancellationToken`,
  so a browser keeps painting while a million rows load.
- **Change Batches**: records added, changed and removed by a **Record Key** make the next Snapshot,
  sharing everything the batch did not touch. A reader is told what left and what came, so it can
  fold the change in rather than start again.

A Snapshot is data, not a query engine: it does not sort, filter, group or aggregate.

> **This is a prerelease (`0.x`).** `ExGrid.Data` ships with ExPivot and is not part of ExGrid's
> release. The API may change between prereleases.

## Requirements

- **.NET 10 or newer.** The package targets `net10.0` and has no dependencies.

## From objects

```csharp
using ExGrid.Data;

record Trade(long Id, string Desk, decimal Notional, DateOnly TradeDate);

var builder = new SnapshotBuilder<Trade>()
    .Integer("Id", t => t.Id)
    .Text("Desk", t => t.Desk)
    .Decimal("Notional", t => t.Notional)
    .Date("TradeDate", t => t.TradeDate, caption: "Trade date")
    .Key("Id");

Snapshot snapshot = await builder.BuildAsync(trades, new SnapshotLoadOptions { Progress = progress }, token);
```

## Reading

```csharp
var desk = (TextColumn)snapshot["Desk"];
var notional = (DecimalColumn)snapshot["Notional"];
for (var s = 0; s < snapshot.SliceCount; s++)
{
    var slice = snapshot.Slice(s);
    ReadOnlySpan<int> codes = slice.Codes(desk);          // into desk.Dictionary; -1 is a Blank
    DecimalValues values = slice.Decimals(notional);      // scaled longs, or decimals
    ReadOnlySpan<ulong> removed = slice.Removed;          // rows this version does not hold
    // ...
}
```

## From columns

```csharp
var columns = new SnapshotColumnsBuilder(options, token);
var desk = columns.Text("Desk");
var price = columns.Double("Price");
foreach (var row in source)
{
    desk.Append(row.DeskSpan);        // a string is made only for text not seen before
    price.Append(row.Price);
    await columns.CheckpointAsync();  // cancellation, progress, and a yield when the slice is spent
}
Snapshot snapshot = await columns.BuildAsync();
```

Whole spans append at once, a text column may come as codes into another producer's dictionary,
which is taken under the Snapshot's rules, and a bulk append takes Blanks as a bit set.

## Live data

```csharp
ChangeBatch batch = builder.Batch(added: newTrades, changed: amendedTrades, removedKeys: [1042L]);
SnapshotChange change = snapshot.Apply(batch);   // whole, or refused naming the key
snapshot = change.After;                          // change.Before reads exactly as before
// fold: subtract change.Removed (rows of Before), add change.Added (rows of After)
```
