# 01: Board Inputs and the Shaped Grid

Status: done

ADR-0341 in `ExGrid.Data`: `BoardInput` (declaration read from JSON, `Check`/`Bind` against a
`DataShape`, `FromJson` by the declared kinds), `BoardRefusedException` with a path. ADR-0342 in
`ExGrid`: `GridViewSpec` reads a grid's view, `ShapedGrid` builds its columns and source over a
Snapshot, a decimal or double column refused without an Excel format code.
