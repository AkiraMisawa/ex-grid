# 05: `ExGrid.Data.Arrow`

Status: ready-for-agent

**What to build:** a new project, `src/ExGrid.Data.Arrow`, referencing `ExGrid.Data` exactly and
`Apache.Arrow` within a stated range (built with 23.0.0). Its tests are
`tests/ExGrid.Data.Arrow.Tests`.

- **Reading:** Arrow's IPC stream and file formats, read into a Snapshot.
  - Work from the buffers, never the per-value accessors, which were measured 10–40 times slower.
  - Remap another producer's dictionary to the Snapshot's rules.
  - Map types by ADR-0064's table, and refuse anything outside it by name.
  - Refuse a compressed stream, naming its codec, unless the Consumer passes a codec factory.
- **Writing:** an uncompressed IPC stream.
  - Text as a dictionary of `utf8`, Decimal as `decimal128` at the column's scale, Double as
    `float64`, Integer as `int64`, Boolean as `bool`.
  - Date as `date32` when every value is a midnight, and otherwise as the coarsest `timestamp` unit
    that holds every value exactly.
  - A Blank as a null slot.
  - Captions, the Record Key and the version in the schema's metadata.

**Blocked by:** 01

- [ ] DA-13: a round trip is equal column by column and Blank by Blank, with the metadata
- [ ] DA-14: other producers' dictionaries; the type table; the refusals; with and without a codec
- [ ] DA-15: the Date units, and no compression
- [ ] DA-16: the package check reads and writes a stream through the packed packages
- [ ] DA-1: the references as stated

## Comments
