# 03: TODAY, as ADR-0121 and ADR-0122 decide it

Status: done

**What to build:** `TODAY()` answers the Sheet Day (ADR-0121). The engine is given the day as data
(`Sheet.SetToday`). The component resolves it, from a fixed `Today`, else the Consumer's `TimeZone`,
else the browser's zone, and moves it on at midnight. The grid reads the browser's zone once at
attach, the ninth allowlisted use of JavaScript (ADR-0122).

**Blocked by:** ADR-0121 and ADR-0122 (decided with the user, 2026-10-03)

- [x] Engine: `Sheet.Today` and `SetToday`; `TODAY` waits with `#GETTING_DATA` until the day is
      known; only its readers recalculate; the day is not in a Sheet Document (SH-56)
- [x] `TODAY` gives a General cell the short date format (FF-038)
- [x] ExSheet: `Today` and `TimeZone` parameters; the day moves at midnight on the registered
      `TimeProvider`, reading at most an hour apart (SH-57)
- [x] ExGrid: `OnBrowserTimeZone`, the zone read once at attach only when asked; ADR-0021's table and
      `AGENTS.md` count nine uses (SH-58)
- [x] Layer 3: `sheet-today.spec.mjs` on both hosts, two browser zones 25 hours apart, and the
      Consumer's zone and fixed day
- [x] Catalogue row Supported; ADR-0047's additions point to ADR-0121; READMEs and glossary
      (**Sheet Day**)

## Comments

2026-10-03: done. Layers 1 and 2 pass. `sheet-today.spec.mjs` passes on both hosts, repeated three
times on the Server host, under Chromium locally; CI's run under Chrome and Edge is the full one.
`NOW` stays in the catalogue: the Sheet Day holds no time of day.
