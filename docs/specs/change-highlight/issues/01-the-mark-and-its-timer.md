# 01: The mark and its timer

Status: ready-for-agent

**What to build:** `CellChangedAt`, `ChangeHighlightDuration` and `Clock` on `ExGrid`, passed to
the rows.

- **Painting:** `ex-changed` is interned into the cell classes and painted on value cells while the
  mark lasts.
- **Removal:** one timer, for the earliest end among the marks painted. It re-renders only the rows
  whose marks have ended.
- **Stylesheets:** the token and its forced-colors rule go in `ex-grid.css`, and the Wrapper maps
  the token in `mud-ex-grid.css`.

**Blocked by:** None

- [ ] DC-53: layer 2 with a fake `TimeProvider`, with render counts
- [ ] DC-1 still holds: no declaration means no call, no class and no timer
- [ ] DC-55: no transition in either stylesheet; the forced-colors rule present; the live region
  unchanged
- [ ] The token and the class listed in ADR-0029's tables (done with the ADR) and in
  `docs/implementation-status.md`

## Comments
