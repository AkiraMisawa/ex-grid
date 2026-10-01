# What to verify on Windows: the Keyboard Field prototype with a real IME (PROPOSED)

Status: needs-triage

*(A proposal, written with ticket 79's prototype. It takes no run number: the user reserves one in
`docs/agents/numbering.md` if the run is wanted, and the file is renamed `verify-on-windows-<N>.md`
then.)*

For the Claude Code session on the Windows desktop of the earlier runs. Read
[`verify-on-windows-15.md`](verify-on-windows-15.md) and the fifteenth run's report
(`verification/2026-10-01-windows-15/report.md`) first: its method, its probe
(`ime-probe.mjs`, `input-server.ps1`) and its Excel script (`excel/ask-excel.ps1`) apply unchanged, and
this run asks the same questions of a different build. **Decide nothing. Record everything.** Do not
change any ADR, `CONTEXT.md` or `docs/definition-of-done.md`.

One thing only a Windows desktop can answer: **whether a real Japanese IME composes on a selected cell
of the prototype, from the first key, as it does in Excel**, and whether everything the fifteenth run
found matching still matches.

The prototype (ticket 79, branch `agent/ps-79-ime-prototype`, not for merging): while a grid has an
editable column and no edit is open, DOM focus is on a text field of the grid's own, the Keyboard Field
(`input.ex-key-field`), inside the root and standing over the Focus cell, unseen. The capture-phase
listener on the root still hears every key first. An IME composes in the field; while it does, the
field is drawn over the cell as the Cell Editor is (class `ex-key-field-composing`). When the
composition ends, its text opens the Cell Editor in Overwrite holding it, and the keyboard moves to
the editor. DOM focus never moves while a composition lasts. A press during a composition ends it
first, and the composition's text goes to the cell it was composed on.

## Setup

- Fetch **`agent/ps-79-ime-prototype`** and record its tip as the verified commit. Branch the run's
  own branch from it. Do not merge anything into it.
- **Ask the user before the first input.** Unlike the fifteenth run, this run has not been
  authorised in advance. Once it is: real keys and mouse to Excel and the browsers, and the IME
  switched on and off by its keys, as in the fifteenth run.
- The environment as in the fifteenth run: the Microsoft Japanese IME, romaji input, Hiragana mode,
  switched by `VK_IME_ON`/`VK_IME_OFF`; keys as virtual-key and scan codes, never as Unicode
  characters; the Japanese keyboard for Part B, English (UK) otherwise. Record the IME's version and
  mode, Chrome's, Edge's and Excel's versions, Windows' mode and the display scale.
- Pages: `/sheet` and `/sheet?chrome=mud`, under Chrome and Edge, on WebAssembly, on the Server host,
  and on the Server host behind 150 ms (the latency proxy's `setRoundTrip`). `/features` and
  `/sheets` where a case says so.
- **The probe reads what it read in the fifteenth run, and the Keyboard Field besides**:
  `document.activeElement` (is it `input.ex-key-field`, the Cell Editor, the Formula Bar, the Name
  Box, the root or `body`), the field's value, whether it wears `ex-key-field-composing`, its
  computed opacity, and its box beside the Focus cell's box. And after every step the IME's open
  status and conversion mode, read as the fifteenth run read them (`WM_IME_CONTROL`).
- Excel as in the fifteenth run's pass b: no UI Automation between keys (it moved the composition
  to the Name Box), the places of the Formula Bar, the Name Box and the status bar taken once.

## Part A — the IME on a selected cell

D10 selected by a press, no edit open, unless the case says otherwise. "Expected of the prototype" is
what the design intends; whether it holds is what is asked. Excel is asked every case where its column
is not "—".

| # | Where | Keys | Expected of the prototype | Excel |
|---|---|---|---|---|
| k1 | D10 | `VK_IME_ON` | The IME's open status reads on afterwards. (The fifteenth run: on the root it stayed off.) | the same |
| k2 | D10 | IME on; `kana`, Space, Enter, Enter | `ｋ`, `か`, `かｎ`, `かな` composed in the Keyboard Field drawn over D10 (every keydown `Process`, `isComposing` from the second); no edit open and the Focus on D10 while composing. Space: `かな`. The first Enter: `compositionend`, the Cell Editor open holding `かな`, with the keyboard; the field empty and unseen. The second Enter: D10 `かな`, the Focus D11, the keyboard back in the field | i1-b: D10 `かな`, D11 |
| k3 | D10 | IME on; `kana`, Escape, Escape | The first Escape ends the composition (`compositionend` with `''`); the Cell Editor open and empty. The second cancels it; D10 unchanged | i2-b |
| k4 | D10 | IME on; `kana`, Space, ↓, ↓, Enter, Enter | Each ↓ the IME's (`Process`/`ArrowDown` with `isComposing`); no Focus move, no outline, nothing written. Then as k2 | i3-b |
| k5 | D10 | the IME left on by an earlier edit (F2, IME on, Escape), then a press on D10, `kana`, Space, Enter, Enter | Composes from the first key: D10 `かな`, not `k穴` (the fifteenth run's i1y) | — |
| k6 | D10 | IME on; `kana`, Space, `kanji`, Space, Enter, Enter | One composition of two clauses, converted together; D10 holds both, as the IME converted them | the same keys |
| k7 | D10, Server behind 150 ms | IME on; at 150 ms between keys: `kana`, Space, Enter, `desu`, Space, Enter, Enter | Two compositions: the second may start in the Keyboard Field before the Cell Editor has the keyboard; it finishes there, and is appended. D10 `かなです`, D11. Nothing lost, doubled or reordered | the same keys at 0 ms |
| k8 | D10 | IME on; `kana` composed; a press on D12 | The composition ends where it was composed: D10 `かな`, committed by the press as Excel's click-away commits; the Focus D12; no edit open; the keyboard in the field | the same |
| k9 | D10 | IME on; `kana` composed; a press on the Formula Bar's text | Record where `かな` goes and where the keyboard is | the same |
| k10 | D10 | IME on; a press on the column header D while `kana` is composed | Record what is sorted or selected, and where `かな` goes. (The prototype holds no header press behind the composition: a known gap.) | the same |
| k11 | `/features`, a Book cell (not Editable), then a Trader cell | IME on; `kana` on each | Book: the field is read-only, the IME stays off (open status off), `kana` types nothing and opens nothing, as on the root before. Trader: composes as k2 | — |
| k12 | D10 | IME on, no composition: Space | Record the keydown (`Process`, or `' '`) and what is typed. A Space the listener sees is the grid's, and opens Overwrite holding a space (ADR-0037); text the IME inserts without composing is carried into an edit as a composition's text is | the same |
| k13 | D10 | IME on, no composition: ↓, →, Tab, Enter, Delete, Ctrl+C, then a press on D12 and Ctrl+V | Each is the grid's as with the IME off: the Focus moves, Delete clears, the copy and the paste land. Nothing is typed into the field | — |
| k14 | `/sheets` | IME on; a press on the left Sheet's B2, `kana` composed and committed (Enter); a press on the right Sheet's B2; `kana` | The left edit stands with `かな` (ADR-0018 section 6); the right Sheet composes in its own field; nothing reaches the left | — |
| k15 | D10 | IME on; `kana` composed; Alt+Tab to another window and back | Record whether the composition survives, and where its text goes | the same |
| k16 | D10 | IME on; `kana` composed; a press on the composition's own text | Record what happens. (The field takes no pointer: the press is on D10's row, and ends the composition.) | the same |

## Part B — the fifteenth run's cases, again

Run the fifteenth run's i4 to i10 and i1x to i3x on the prototype, with the same keys and the same
readings, and record any state that reads differently from the fifteenth run's record. Nothing there
is expected to change: the Formula Bar, the Name Box, Find and an edit opened by F2 hold the keyboard
themselves, as before.

## Part C — the keyboard and the clipboard with the IME off

With English (UK) and the IME off, on `/sheet` (both Chromes) and `/features`, Chrome, WebAssembly
and the Server host: `150`, Enter, `200`, Enter typed at full speed; the arrows, Ctrl+arrows, Home,
End, PageDown; F2; Ctrl+C and Ctrl+V of a cell; Escape twice with no edit open, then Tab back into the
grid. Record what each does and where the keyboard is after each. Expected: as at the base, but for
where the keyboard is (the Keyboard Field instead of the root), and for the root's focus ring, which
the prototype does not draw (`:focus-visible` is the root's, and the root no longer has focus).

## Finishing

Commit everything to the run's branch and push. The results go to `verification/<date>-windows-<N>/`:
the scripts, the records, the logs and the pictures, and one report with a row per case: what was
read, and, where a reading is given, whether it matches. The last message lists every difference,
each with the ticket, ADR paragraph or DoD row it belongs to. It proposes nothing on the user's
behalf.
