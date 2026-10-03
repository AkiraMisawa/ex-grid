# ExGrid.Data

The Ex family's immutable data. A **Snapshot** is a copy of tabular data at one version, held
column by column: text as a dictionary of codes, money as exact scaled 64-bit integers, dates as
clock values. It has no dependency. The family's bundled sources read it — ExPivot's first — so a
pass over a million rows is a loop over arrays rather than over objects.

- **Six kinds**: Text, Decimal, Double, Integer, Date and Boolean, with a **Blank** possible in each,
  kept apart from `""` and 0.
- **Ways in** that yield a Snapshot or refuse whole, naming the row and the column: the Consumer's
  objects through typed accessors, which box nothing and keep the objects by reference; a CSV read
  as bytes under a **Schema** the Consumer declares, with nothing guessed; a `DbDataReader`, each
  column by its own type; and columns, a value or a span at a time, for data a reader has read
  itself.
- **Loads in slices**, yielding between them and reporting progress, with a `CancellationToken`,
  so a browser keeps painting while a million rows load.
- **Change Batches**: records added, changed and removed by a **Record Key** make the next Snapshot,
  sharing everything the batch did not touch. A reader is told what left and what came, so it can
  fold the change in rather than start again.

A Snapshot is data, not a query engine: it does not sort, filter, group or aggregate.

> **This is a prerelease (`0.x`).** `ExGrid.Data` ships with ExGrid and ExPivot, at the same
> version. The API may change between prereleases.

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

## From a CSV, under a Schema

```csharp
var schema = new CsvSchema(
[
    new CsvColumn("Account", SnapshotKind.Text),                       // "00123" stays "00123"
    new CsvColumn("Notional", SnapshotKind.Decimal) { Header = "Amount (USD)", DecimalPoint = ",", ThousandsSeparator = "." },
    new CsvColumn("TradeDate", SnapshotKind.Date) { DateFormats = ["dd.MM.yyyy"] },
    new CsvColumn("Live", SnapshotKind.Boolean) { TrueText = ["1"], FalseText = ["0"] },
])
{
    Separator = CsvSeparator.Semicolon,
    BlankText = ["NULL", "-"],          // an empty field is a Blank in every kind already
    RecordKey = "Account",
};

Snapshot snapshot = await schema.ReadAsync("trades.csv", new SnapshotLoadOptions { Progress = progress }, token);
```

The file is read as bytes, straight into the columns, with no string made per cell, and quoting
follows RFC 4180. A value its kind cannot read, a record of the wrong length, a quote left open or
text not valid in the encoding fails the load, naming the row, the column and the line; a declared
column missing from the header is refused by name, and a column the Schema does not declare is
skipped. UTF-8 is read with or without its byte-order mark. Shift-JIS, which Excel on Japanese
Windows saves, is read when the Schema declares `Encoding = CsvEncoding.ShiftJis`; only that member
refers to the code pages, so a trimmed browser application that never asks for it does not download
them.

For a file nobody has described, a Schema can be suggested from its first rows. It marks each
column whose kind is not clear — digits with leading zeros, mixed date formats, a comma that could
be the decimal point — and it is never applied by itself:

```csharp
CsvSuggestion suggestion = await CsvSchema.SuggestAsync("unknown.csv", cancellationToken: token);
foreach (var column in suggestion.Columns.Where(c => c.IsUnclear))
    Console.WriteLine($"{column.Column.Name}: {string.Join(" ", column.Marks.Select(m => m.Note))}");
// Show it to the user; read the file under it, or under what they changed, once they confirm.
Snapshot confirmed = await suggestion.Schema.ReadAsync("unknown.csv", cancellationToken: token);
```

## From a DbDataReader

```csharp
await using var reader = await command.ExecuteReaderAsync(token);
Snapshot snapshot = await new SnapshotDataReaderBuilder()
    .Column("trade_id", name: "Id")
    .Column("desk", name: "Desk")
    .Column("notional", name: "Notional", caption: "Notional (USD)")
    .Text<Guid>("book_id", g => g.ToString(), name: "Book")   // a type no kind reads by itself
    .Key("Id")
    .BuildAsync(reader, options, token);
```

Each column is read by its own type: `decimal` as Decimal, `double` and `float` as Double, the
integers as Integer, `DateTime`, `DateOnly`, `DateTimeOffset` and `TimeOnly` as Date, `bool` as
Boolean, `string` and `char` as Text, and `DBNull` as a Blank. A column of any other type is refused
by name unless a conversion like the one above declares its kind. With no column declared, every
column of the reader is read under its own name.

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
