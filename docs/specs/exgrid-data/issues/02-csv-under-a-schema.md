# 02: A CSV read under a Schema, and a suggested Schema

Status: ready-for-agent

**What to build:** `ExGrid.Data`'s CSV reader, which reads a stream of bytes straight into a
Snapshot's columns, under a Schema the Consumer declares.

- **Quoting** follows RFC 4180.
- **The separator** is a comma, a tab or a semicolon.
- **The header row** is optional. When there is one, it is matched per declared column: a missing
  column is refused by name, and an undeclared one is skipped.
- **Per column**, the Schema gives the kind; the decimal point, the thousands separator or a culture;
  a date's format; and the strings that count as a Blank.
- **An empty field is a Blank** in every kind.
- **Encodings:** UTF-8 is read with or without its byte-order mark. Shift-JIS is read through an
  opt-in encoding that alone refers to the code pages.
- **A suggestion** builds a Schema from a file's first rows and marks each column whose kind is not
  clear.

**Blocked by:** 01

- [x] DA-7: each clause a named test, including a record cut across every read boundary
- [x] DA-8: UTF-8 with and without its byte-order mark; Shift-JIS declared; the code pages referred
  to by the opt-in encoding alone
- [x] DA-9: the suggestion marks unclear columns, and applies nothing
- [x] DA-5 and DA-6 hold for the reader: progress, cancellation, and a malformed row named

## Comments

Built, 2026-10-01.

- **The Schema**: `CsvSchema(Columns)` with `Separator` (`CsvSeparator.Comma`, `Tab`, `Semicolon`),
  `HasHeader` (true by default), `Encoding` (`CsvEncoding.Utf8` by default, or `CsvEncoding.ShiftJis`),
  `BlankText` for every column, and `RecordKey`. Each `CsvColumn(Name, Kind)` declares the `Header`
  it matches (its name by default) or a `Position` (from zero; the default without a header row),
  its `Caption`, its `Culture`, `DecimalPoint` and `ThousandsSeparator` (which win over the
  culture's), its `DateFormats` (.NET's exact formats, tried in order; ISO 8601 by default), its
  `TrueText` and `FalseText` (`TRUE` and `FALSE` by default, matched ignoring case), and its own
  `BlankText`, which replaces the Schema's. A Schema is a value, changed with `with`. One that
  contradicts itself is refused with an `ArgumentException` before a byte is read: a name or a
  position twice, a decimal point equal to the thousands separator, a format .NET cannot read, a
  spelling that is both true and false or both a Boolean and a Blank, a key that is not Text or
  Integer — and a date format that would take part of a date from the day it is read (a month or a
  day without a year, an offset without a date), which .NET would fill in from today.
- **Reading**: `ReadAsync(Stream)` and `ReadAsync(path)`, from bytes into the columns: a tokenizer
  cuts RFC 4180 records over bytes, and a reader per column reads its field with no string — text
  through a cache of the codes of the bytes already decoded, numbers by a hand-written parser,
  `yyyy-MM-dd`-like formats from the bytes themselves. A record cut by the buffer is cut again
  once more bytes have come. Text is kept exactly; the other kinds set aside the ASCII spaces
  around a value. A thousands separator is read only between groups of the culture's sizes, so
  `1.5` under `.` thousands is refused rather than read as fifteen. A value marked UTC (`Z`, `GMT`)
  is the clock it shows: .NET would have turned it into the reading machine's clock.
- **Refusals** name the data record (from one), the column and the line the record begins on:
  `Row 12,345, column 'Qty': 'x' is not an integer (line 12,346).` A record of another length, an
  empty line among wider records, a quote left open, inside a field or followed by text, text not
  valid in the encoding, an impossible date, an overflow, a Decimal beyond `decimal`, a Double
  beyond `double`, a Blank or twice-carried Record Key; and by name, before any record, a declared
  column missing from the header (with every other missing one, and the near spelling the header
  has), or a header that stands twice. A file that begins with UTF-16's byte-order mark is refused
  by name: a CSV is read in UTF-8 or Shift-JIS.
- **DA-8**: `CsvEncoding.ShiftJis` alone refers to `CodePagesEncodingProvider`, and is never inlined.
  The test reads the package's metadata — every method body's IL, its locals and catch clauses,
  every signature, base, interface and attribute — and finds that member and no other; a reference
  added elsewhere is named. **`ExGrid.Data` is now marked trimmable**: a published Blazor
  WebAssembly application that read only UTF-8 shipped `System.Text.Encoding.CodePages` (686 KB,
  170 KB brotli), because Blazor keeps an assembly not marked trimmable whole. Marked, it ships none,
  and an application that asks for Shift-JIS does (measured 2026-10-01); a test holds the mark.
- **The suggestion**: `CsvSchema.SuggestAsync(stream or path, CsvSuggestionOptions)` reads the first
  `Rows` data records (1,000 by default) and returns a `CsvSuggestion`: the `Schema`, a
  `CsvColumnSuggestion` per column (its `Marks` and a few `Examples`) and the file's `Marks`. A
  `CsvMark` is a `CsvDoubt` and a sentence naming a value and its row. Marked: leading zeros
  (suggested as Text, "could be an identifier"), mixed date formats (all suggested), day or month
  first (the user's culture chooses), both `,` and `.`, a separator that could be either, an empty
  column, blank words among values (suggested as Blanks), 0 and 1 or yes and no (possible Booleans,
  never guessed), numbers too long for a Decimal, a few texts among numbers, an empty or repeated
  header (matched by position), and for the file a separator or header row not told for sure, and
  records of another length. Booleans are suggested only as Excel writes them, `TRUE` and `FALSE`.
- **Measured** (4 vCPUs, .NET 10.0.12, CoreCLR, never gated): the prototype's file — 73.7 MB,
  1,000,000 rows, 10 columns (six Text, a Date, two Decimals, an Integer), with its BOM — read from
  the file in 445–503 ms once warm (953 ms cold), allocating 60.8 MiB in all, about the columns
  themselves: no string per cell. The prototype read it in 0.47 s.
