# 01: The mark and its timer

Status: done

**What to build:** `CellChangedAt`, `ChangeHighlightDuration` and `Clock` on `ExGrid`, passed to
the rows.

- **Painting:** `ex-changed` is interned into the cell classes and painted on value cells while the
  mark lasts.
- **Removal:** one timer, for the earliest end among the marks painted. It re-renders only the rows
  whose marks have ended.
- **Stylesheets:** the token and its forced-colors rule go in `ex-grid.css`, and the Wrapper maps
  the token in `mud-ex-grid.css`.

**Blocked by:** None

- [x] DC-53: layer 2 with a fake `TimeProvider`, with render counts
- [x] DC-1 still holds: no declaration means no call, no class and no timer
- [x] DC-55: no transition in either stylesheet; the forced-colors rule present; the live region
  unchanged
- [ ] The token and the class listed in ADR-0029's tables (done with the ADR) and in
  `docs/implementation-status.md`

## Comments

2026-10-01: Built with 42 tests naming ADR-0067 (layers 1 and 2, and the Wrapper's stylesheet).
The default colour was changed to a 40% tint of `Mark` after Chromium was seen to keep `Mark`
yellow on a dark page; ADR-0067 records it. The `docs/implementation-status.md` entry is written
with the rest of the ExPivot status.
