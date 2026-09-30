# 03: A Snapshot read from a `DbDataReader`

Status: ready-for-agent

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

- [ ] DA-10, tested through `DataTableReader`, which is itself a `DbDataReader`, so the tests need
  no database package
- [ ] DA-5 and DA-6 hold for the reader

## Comments
