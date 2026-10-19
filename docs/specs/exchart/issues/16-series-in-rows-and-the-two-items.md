# 16: Series in rows, and two menu items only when both mean something

Status: done

**Why:** [ADR-0258](../../../adr/0258-the-selection-bridge-follows-the-rows-it-was-made-from.md) (M3-Q2).

- [x] Columns with declared positions (tenor years) chart series in rows: a row, by its Row Key, across a Header Group's positioned columns
- [x] A selection over one positioned column stands for its rows' whole curves
- [x] A number column with no declared position is refused by name; a group whose columns declare different Series Kinds is refused
- [x] Only a selection that holds both an axis column and positioned columns offers "Series in columns" and "Series in rows"; the Consumer may fix the orientation

**Blocked by:** 15.

Roadmap: internal
