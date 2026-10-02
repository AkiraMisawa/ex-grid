# 141: Column edges lie on Device Pixels

Status: done

**What to build:** ADR-0090's third part. Where the grid builds its `ColumnGeometry`, each column's left
edge is its position rounded to the nearest Device Pixel, the Row Headings' width included, and a
column's painted width is the distance to the next edge. A column's rule is drawn in whole Device
Pixels, as a row's is. Every grid.

**Blocked by:** 140

- [x] **Edges are summed first and rounded once**, so the total never drifts (VZ-17).
- [x] **The View State, a resize's report and Auto width keep the declared widths.**
- [x] **A change of resolution rebuilds the geometry once**; rows skip afterwards as before (ADR-0003).
- [x] **Layer 1** at 1, 1.25, 1.5 and 2.25; **layer 3** at 150%: a vertical gridline is one Device
      Pixel of `#E0E0E0` on a Sheet.

## Comments

2026-10-02, claude/exsheet-part-c. `ColumnGeometry` takes the ratio: an edge is its declared position
counted in whole Device Pixels, a width the difference of two counts, and `DeclaredWidthPxOf` keeps
the declared width. The Row Headings' width is put on a Device Pixel before it. A column's rule and a
Fill's cover over it are `--ex-rule-dp`. A resize starts from, and the `####` decision reads, the
declared width (ADR-0090's change while building: an Auto column turned to `####` when its edge
rounded down). Layer 1: `ColumnGeometryTests` at 1, 1.25, 1.5 and 2.25. Layer 3: VZ-17 at 100% and a
real 150%.
