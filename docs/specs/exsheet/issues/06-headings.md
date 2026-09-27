# 06: Headings: header click selects, Row Headings, and hiding them

Status: done

**What to build:** The first ADR-0050 declaration, in the core and wired by ExSheet. A header click selects the
column. Row Headings are a pinned band beside the rows, outside the column index space, labelled by
the Consumer: a click selects the row and Shift+click extends. The corner selects all. Either
Heading can be hidden, and hiding the column header is also available to a plain ExGrid.

**Blocked by:** 02

- [x] Off by default: an existing ExGrid's header click still sorts (existing suites green)
- [x] On: click, Shift+click and the corner select as stated; nothing sorts (ADR-0050)
- [x] Row Headings are not in Selection, copy, Ctrl+A or the Enter/Tab cycle
- [ ] Either Heading hides; the Sheet still addresses `A1` (the hiding is done in the core; `A1` is ExSheet's, below)
- [x] The band's width is resolved geometry, not a stylesheet literal (ADR-0027/0028)

## Comments

The core half, 2026-09-27. `ExGrid` gains four declarations, each off by default:
`HeaderClickSelects` (a plain header click selects the column, Shift+click extends, nothing
sorts), `RowHeadings` (`Func<int, string>`: the band's label for a row's absolute position),
`RowHeadingWidth` (`double?`: explicit width, otherwise estimated from the Cell Metrics over
the first and last rows' labels) and `HideHeader` (the header band goes, and the rows start at
the Viewport's top). The Row Headings are a lead band in `ColumnGeometry` (`LeadWidthPx`,
`IsInLead`), painted as the first sticky cell of each row and the corner of the header; no
column index names them. `GridSelection` gains `SelectColumn`, `SelectRow` and `ExtendToRow`.
No JavaScript was added (DC-15). Layer 1: `HeadingsSelectionTests`; layer 2: `HeadingsTests`.

What remains:

- ExSheet's wiring (column letters as headers, row numbers as labels) is the ExSheet stream's,
  and "the Sheet still addresses `A1`" with a Heading hidden is checked there: the core never
  addresses cells by name.
- Layer 3 for DC-3 (the band held at the left edge while scrolling sideways, under both
  Chromes and on a platform with classic scrollbars), and DC-25 (two grids, one declaring).
- Dragging across Row Headings or column headers to select several is not built; Shift+click
  is the route.

