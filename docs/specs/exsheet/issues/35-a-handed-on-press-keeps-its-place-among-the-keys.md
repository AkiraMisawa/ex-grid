# 35: A press handed on keeps its place among the pointing Sheet's keys

Status: ready-for-agent

**What to build:** ADR-0058, "On a circuit", the second bullet, and ADR-0021's note of 2026-09-30.
On a circuit, the text a press on a registered grid writes reaches the Sheet's field a round trip
later, and a key typed meanwhile would replace it. The pressed grid tells the Sheet's root, in
script, that a press was handed on, and the Sheet holds its keys behind it.

**Blocked by:** Ticket 34 (the hand-over). Its layer 3 needs ticket 37 (the Scope on `/sheet`).

- [ ] While a grid is pointed at, its render names the root to tell. The declaration of ticket 34
      carries it (DC-54)
- [ ] In `ex-grid.js`, `onPress`: for a primary press on the rows or a header while pointed at,
      dispatch one event on that root. The module keeps no registry of instances, and nothing is
      added on `document` or `window` (DC-54)
- [ ] The listener on each root hears that event and starts the hold it starts for a press on its own
      rows (`holdBehindPress` / `askAboutPress`). The hold ends when the core has answered the press.
      The Sheet's core answers once the Scope has written or refused; a press the Scope did not take
      is answered too, so no hold waits for the two-second fallback (DC-54, ADR-0010)
- [ ] The comments on the allowlist at the head of `ex-grid.js` name the event
- [ ] Script-shape tests: no layout read, no listener on `document` or `window`, no module-level
      state for another instance (DC-54)
- [ ] Layer 3 on `/sheet`, Server host, 150 ms injected: `=`, a press on a PV cell and `*` at once
      gives `=XLOOKUP("R-4471", Positions[Id], Positions[PV])*`; `=SUM(1,`, a press, then `)` and Enter
      at once, commits `=SUM(1,XLOOKUP(…))`, never `=SUM(1,)` (DC-54)
