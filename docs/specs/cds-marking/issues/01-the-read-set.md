# 01: The Read Set

Status: ready-for-agent

**What to build:** `Sheet` answers a cell's Read Set
([ADR-0120](../../../adr/0120-the-engine-tells-which-linked-table-cells-a-value-was-computed-from.md)),
in `ExSheet.Engine`. This is a product change: it ships in the package, not in the sample.

- **The answer:** the Linked Table cells (table, row, column, and the row's key where the table has
  one) whose values the evaluation of the cell's current Value took as operands. It follows the
  Formula cells the cell reads.
- **Consulted cells are reported apart:** the lookup array of `XLOOKUP` decides the row, and is not
  read.
- **Only what was evaluated counts:** a branch of `IF` not taken adds nothing, and a waiting table
  adds nothing.
- **It is computed on request,** by evaluating again with tracing over the Values and snapshots the
  Sheet holds. Nothing is recorded during recalculation.
- **The public surface** is a method on `Sheet` and a record for a table cell. Each has an XML doc
  comment; the build fails without one.

**Blocked by:** None

- [ ] SH-56, layer 1: an `XLOOKUP` reads the return cell and consults the key column; `SUM(T[C])`
  reads the column; a cell reading a cell reports both; `IF`'s untaken branch adds nothing; a
  waiting table adds nothing; the answer matches the Value after a push and after an edit
- [ ] A Sheet that is never asked recalculates exactly as before (the existing suite, and a timing
  run on a large Sheet recorded, not gated)
- [ ] The cost of asking every Point of the sample's book once per snapshot, measured and recorded
  in the ticket's comments
- [ ] `src/ExSheet.Engine/README.md` documents it
