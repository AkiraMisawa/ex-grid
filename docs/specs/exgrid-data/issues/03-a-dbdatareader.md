# 03: A Snapshot read from a `DbDataReader`

Status: done

**What to build:** a builder that reads any ADO.NET `DbDataReader` into a Snapshot.

- **Each column is read by its own type:**
  - `decimal` as Decimal;
  - `double` and `float` as Double;
  - the integer types as Integer;
  - `DateTime`, `DateOnly` and `DateTimeOffset` as Date;
  - `bool` as Boolean;
  - `string` and `char` as Text.
- **`DBNull` is a Blank.**
- **A column of any other type is refused by name**, unless the Consumer declares its kind and how
  to read it.
- **Columns can be chosen, renamed and captioned.**

The builder works asynchronously (`ReadAsync`), in slices, with progress and cancellation.

**Blocked by:** 01

- [x] DA-10, tested through `DataTableReader`, which is itself a `DbDataReader`, so the tests need
  no database package
- [x] DA-5 and DA-6 hold for the reader

## Comments

Built, 2026-10-01.

- **`SnapshotDataReaderBuilder`**, a declaration reused for every build, as `SnapshotBuilder<T>` is:
  `Column(field, name, caption)` chooses a column read by its own type; `Text<TField>`,
  `Decimal<TField>`, `Double<TField>`, `Integer<TField>`, `Date<TField>` and `Boolean<TField>` choose
  one read through a conversion (`Text<Guid>("book_id", g => g.ToString())`), the value read with
  `GetFieldValue<TField>` and a `null` it returns a Blank; `Key(name)` names the Record Key;
  `BuildAsync(reader, options, token)` reads the reader's current result set with `ReadAsync`, in
  slices that yield and report the rows read, and neither moves it to its next result set nor
  closes it. With nothing declared, every column is read under its own name; once one is, only the
  declared ones, in the order declared.
- **By type**: `decimal` Decimal; `double`, `float` Double; `long`, `int`, `short`, `byte`, `sbyte`,
  `ushort`, `uint` Integer, and `ulong` within a long's range (beyond it, refused by row and column);
  `DateTime` (its `Kind` ignored), `DateOnly` (its midnight), `DateTimeOffset` (its clock, the offset
  dropped) and `TimeOnly` (on the first day, as a CSV's time-only format reads) Date — ADR-0063 says
  "a date or a time is Date", and `TimeOnly` is a time of day; `TimeSpan` is refused as the brief
  says, since it is as often a duration (`interval`) as a time; `bool` Boolean; `string`, `char`
  Text; `DBNull` a Blank in every kind.
- **Refusals**, before a row is read and by name: a column of another type (`Column 'Book': its
  type, Guid, is not one a Snapshot reads by itself; declare its kind and how to read it. So are
  'Blob' (byte[]) and 'Held' (TimeSpan).`), a declared column missing from the reader (with the near
  spelling it has), two columns of one name or a column of none when every column is read, a Record
  Key the reader gives as another kind. By row and column: a getter or a conversion that throws
  (`Row 257, column 'Notional': reading the value threw OverflowException: …`, the exception kept
  as the inner one), a `ulong` beyond 64 bits; and, from the build, a Blank or twice-carried key.
- **Progress** while the Record Keys are indexed now stays at every row read, in the columns builder
  that the CSV and the reader both finish through, as a build from objects does; a keyed load went
  from 10,000 rows back to 1,024.
- **Measured** (4 vCPUs, .NET 10.0.12, CoreCLR, never gated): 100,000 rows of the same ten columns
  through a `DataTableReader` in 75–85 ms once warm (363 ms cold), of which the reader alone, every
  value read once by its typed getter, takes 49–67 ms. 23.1 MiB allocated: 10.7 MiB of it is the
  reader's own boxing, measured on that bare pass, and the rest the columns, grown as they fill.
