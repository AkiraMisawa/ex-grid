# 15: A selection is read as series in columns

Status: done

**Why:** [ADR-0251](../../../adr/0251-a-chart-is-defined-in-the-datas-terms-and-a-stale-gesture-is-refused.md), [ADR-0258](../../../adr/0258-the-selection-bridge-follows-the-rows-it-was-made-from.md) (M3-Q1, M3-Q2).

- [x] A context-menu command the Consumer adds to the grid in one line (`ContextCommands`), worded through `CommandLabel`; ExGrid unchanged
- [x] An axis column (a date column, or a number column the Consumer names) charts series in columns, each a column at the Row Keys it was made from
- [x] A selection taken under another Row Sequence Version is refused ("The rows were reordered after the selection was made. Select again.")
- [x] No axis column, two, or a chosen text column is refused by name; one cell charts the Window and leaves out the text columns nobody chose
- [x] Names and panels from Header Groups; a series with negatives beside positives (a Diff) in a panel of its own, decided when the series is made (moved from ticket 06)
- [x] Rows beyond the Window are read through the Grid Source; a selection it cannot hand over whole is refused

**Blocked by:** 08.

Roadmap: internal
