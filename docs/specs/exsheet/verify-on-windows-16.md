# What to verify on Windows, sixteenth run

Status: ready-for-human — ticket 80 is built (PR #44, CI green at 9951ee0)

*(Numbered sixteenth from `docs/agents/numbering.md`. Written as a proposal with ticket 79's prototype,
`verify-on-windows-PROPOSED-ime-prototype.md`, and taken over for the Keyboard Field as decided,
ADR-0080.)*

For the Claude Code session on the Windows desktop of the earlier runs. Read
[`verify-on-windows-15.md`](verify-on-windows-15.md) and the fifteenth run's report
(`verification/2026-10-01-windows-15/report.md`) first: its method, its probe
(`ime-probe.mjs`, `input-server.ps1`) and its Excel script (`excel/ask-excel.ps1`) apply unchanged, and
this run asks the same questions of a different build. **Decide nothing. Record everything.** Do not
change any ADR, `CONTEXT.md` or `docs/definition-of-done.md`.

What only a Windows desktop can answer:

- **Part A: whether a real Japanese IME composes on a selected cell, from the first key, as it does in
  Excel** (ADR-0080, ED-30).
- **Part B: whether everything the fifteenth run found matching still matches.**
- **Part C: the keyboard and the clipboard with the IME off**, where the keyboard now rests in the
  Keyboard Field.
- **Part D: the cases the fifteenth run left**: the Name Box's first key under a real IME (ticket
  78), a drag on the press that gives the Name Box the keyboard, and Escape's release of Tab after a
  press elsewhere on the page (ticket 77, ADR-0012).

The build (ADR-0080, ticket 80): while a grid has an editable column and no edit is open, DOM focus is
on the Keyboard Field (`input.ex-key-field`), a text field of the grid's own inside the root, standing
over the Focus cell and unseen. It is the grid's one tab stop, and it carries `aria-activedescendant`.
The capture-phase listener on the root still hears every key first. An IME composes in the field;
while it does, the field is drawn over the cell as the Cell Editor is (a class on the field; record
its name from the build). When the composition ends, its text opens the Cell Editor in Overwrite
holding it, and the keyboard moves to the editor. DOM focus never moves while a composition lasts. A
press during a composition ends it first, and the composition's text goes to the cell it was composed
on.

## Setup

- Fetch **`claude/exsheet-keyboard-field`** at the commit the request names, and record it as the
  verified commit. Branch **`claude/exsheet-windows-verify-16`** from it. If the tip moves during the
  run, do not merge it in.
- **Ask the user before the first input**, unless the request that hands this run over says the user
  has authorised it in advance. Once it is authorised: real keys and mouse to Excel and the browsers,
  and the IME switched on and off by its keys, as in the fifteenth run. Say "starting" before the first
  input and "finished" after the last.
- The environment as in the fifteenth run: the Microsoft Japanese IME, romaji input, Hiragana mode,
  switched by `VK_IME_ON`/`VK_IME_OFF`; keys as virtual-key and scan codes, never as Unicode
  characters; the Japanese keyboard for Parts A, B and D, English (UK) otherwise. Record the IME's
  version and mode, Chrome's, Edge's and Excel's versions, Windows' mode and the display scale.
- Pages: `/sheet` and `/sheet?chrome=mud`, under Chrome and Edge, on WebAssembly, on the Server host,
  and on the Server host behind 150 ms (the latency proxy's `setRoundTrip`). `/features` and
  `/sheets` where a case says so.
- **The probe reads what it read in the fifteenth run, and the Keyboard Field besides**:
  `document.activeElement` (is it `input.ex-key-field`, the Cell Editor, the Formula Bar, the Name
  Box, the root or `body`), the field's value, whether it wears the composing class, its computed
  opacity, and its box beside the Focus cell's box. And after every step the IME's open status and
  conversion mode, read as the fifteenth run read them (`WM_IME_CONTROL`).
- Excel as in the fifteenth run's pass b: no UI Automation between keys (it moved the composition
  to the Name Box), the places of the Formula Bar, the Name Box and the status bar taken once.

## Part A — the IME on a selected cell

D10 selected by a press, no edit open, unless the case says otherwise. "Expected of the build" is
what ADR-0080 decided; whether it holds is what is asked. Excel is asked every case where its column
is not "—".

| # | Where | Keys | Expected of the build | Excel |
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
| k10 | D10 | IME on; a press on the column header D while `kana` is composed | Record what is sorted or selected, and where `かな` goes. ADR-0080: a primary press anywhere in the root ends the composition first, so `かな` goes to D10 | the same |
| k11 | `/features`, a Book cell (not Editable), then a Trader cell | IME on; `kana` on each | Book: the field is read-only, the IME stays off (open status off), `kana` types nothing and opens nothing, as on the root before. Trader: composes as k2 | — |
| k12 | D10 | IME on, no composition: Space | Record the keydown (`Process`, or `' '`) and what is typed. A Space the listener sees is the grid's, and opens Overwrite holding a space (ADR-0037); text the IME inserts without composing is carried into an edit as a composition's text is | the same |
| k13 | D10 | IME on, no composition: ↓, →, Tab, Enter, Delete, Ctrl+C, then a press on D12 and Ctrl+V | Each is the grid's as with the IME off: the Focus moves, Delete clears, the copy and the paste land. Nothing is typed into the field | — |
| k14 | `/sheets` | IME on; a press on the left Sheet's B2, `kana` composed and committed (Enter); a press on the right Sheet's B2; `kana` | The left edit stands with `かな` (ADR-0018 section 6); the right Sheet composes in its own field; nothing reaches the left | — |
| k15 | D10 | IME on; `kana` composed; Alt+Tab to another window and back | Record whether the composition survives, and where its text goes | the same |
| k16 | D10 | IME on; `kana` composed; a press on the composition's own text | Record what happens. (The field takes no pointer: the press is on D10's row, and ends the composition.) | the same |

## Part B — the fifteenth run's cases, again

Run the fifteenth run's i4 to i10 and i1x to i3x on the build, with the same keys and the same
readings, and record any state that reads differently from the fifteenth run's record. Nothing there
is expected to change: the Formula Bar, the Name Box, Find and an edit opened by F2 hold the keyboard
themselves, as before.

## Part C — the keyboard and the clipboard with the IME off

With English (UK) and the IME off, on `/sheet` (both Chromes) and `/features`, Chrome, WebAssembly
and the Server host: `150`, Enter, `200`, Enter typed at full speed; the arrows, Ctrl+arrows, Home,
End, PageDown; F2; Ctrl+C and Ctrl+V of a cell; Tab into the grid from the element before it, and
Shift+Tab from the element after it. Record what each does and where the keyboard is after each.
Expected: as at the base, but for where the keyboard is: the Keyboard Field instead of the root on a
grid that edits (`/sheet`, `/features`), the root on a display-only grid. The root's focus ring shows
after Tab into the grid and not after a click (KB-12). Shift+Tab from the element after the grid lands
in the grid, and Shift+Tab again leaves it (A11Y-4).

## Part D — the cases the fifteenth run left

`/sheet`, under both Chromes, on WebAssembly, on the Server host and behind 150 ms. Excel is asked d1
and d2.

| # | Where | Keys | Expected of the build | Excel |
|---|---|---|---|---|
| d1 | the Name Box | A press on D10, a press on the Name Box, IME on, `kana`, Enter, Escape | The press selected `D10` (ticket 78). The first composing key replaced the selection: the Name Box reads `かな` while composing, not `D10かな`. The first Enter ends the composition and goes nowhere | the same keys (the fifteenth run's i7-b) |
| d2 | the Name Box, Server behind 150 ms | A press on F8, then at once a press on the Name Box, IME on, `kana` | The render naming F8 lands after the press; the first composing key selects the name again, so the Name Box reads `かな`, not `F8かな` | — |
| d3 | the Name Box | With the keyboard elsewhere, a press inside `D10` dragged across part of it, then released | Record the selection after the release. The build selects the whole text at the release of the press that gave the Name Box the keyboard, a drag included | the same gesture |
| d4 | D10 | IME off. Escape, Escape (no edit open), a press on a control of the page outside the grid, Shift+Tab or Tab back into the grid, then Tab | The release ended when the keyboard left the grid: the last Tab moves the Focus inside the selection, and does not leave the grid (KB-8) | — |
| d5 | `/sheet`, if a screen reader is installed | NVDA with its Speech Viewer, or Narrator, whichever is there; Tab into the grid, ↓, → | Record what it says at each step, as text where the reader shows it. Do not install anything to do this; if none is there, record that | — |

## Finishing

Commit everything to the run's branch and push. The results go to `verification/<date>-windows-16/`:
the scripts, the records, the logs and the pictures, and one report with a row per case: what was
read, and, where a reading is given, whether it matches. The last message lists every difference,
each with the ticket, ADR paragraph or DoD row it belongs to. It proposes nothing on the user's
behalf.
