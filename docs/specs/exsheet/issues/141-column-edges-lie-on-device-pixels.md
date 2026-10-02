# 141: Column edges lie on Device Pixels

Status: ready-for-agent

**What to build:** ADR-0090's third part. Where the grid builds its `ColumnGeometry`, each column's left
edge is its position rounded to the nearest Device Pixel, the Row Headings' width included, and a
column's painted width is the distance to the next edge. A column's rule is drawn in whole Device
Pixels, as a row's is. Every grid.

**Blocked by:** 140

- [ ] **Edges are summed first and rounded once**, so the total never drifts (VZ-17).
- [ ] **The View State, a resize's report and Auto width keep the declared widths.**
- [ ] **A change of resolution rebuilds the geometry once**; rows skip afterwards as before (ADR-0003).
- [ ] **Layer 1** at 1, 1.25, 1.5 and 2.25; **layer 3** at 150%: a vertical gridline is one Device
      Pixel of `#E0E0E0` on a Sheet.
