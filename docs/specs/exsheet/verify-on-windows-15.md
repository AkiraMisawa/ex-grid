# What to verify on Windows, fifteenth run

Status: ready-for-human

For the Claude Code session on the Windows desktop of the earlier runs. Read
[`verify-on-windows-9.md`](verify-on-windows-9.md) and [`verify-on-windows-13.md`](verify-on-windows-13.md)
first: their method and tools apply unchanged, and the thirteenth run
(`verification/2026-10-01-windows-13/`) is the model. **Decide nothing. Record everything.** Do not
change any ADR, `CONTEXT.md` or `docs/definition-of-done.md`.

*(Numbered fifteenth from `docs/agents/numbering.md`: 14 is the Cell Format's.)*

Three things only a Windows desktop can answer:

- **Part A: VZ-14 at the current base.** The ninth and earlier runs passed it (2026-09-23,
  2026-09-27). A great deal has changed since, and sign-off needs it at the code that ships.
- **Part B: a real Japanese IME.** Every run so far kept the IME off. ED-11 ("an IME composition is
  never taken by the core", ADR-0010) is checked in CI with synthesised composition events only, and
  ADR-0057's coloured layer keeps a composition apart (`composingIn`). Excel is asked the same keys.
- **Part C: ticket 74's readings, asked of Excel.** ADR-0058, "What the thirteenth Windows run
  settled", the sub-bullet "What counts as a number, and where the rule stops": decided with the user
  and built, not asked of Excel.

## Setup

- Fetch **`claude/exsheet-ime-and-scaling`**. Branch **`claude/exsheet-windows-verify-15`** from its
  tip, and record the tip as the verified commit. If the tip moves during the run, do not merge it in.
- **The user has authorised this run in advance**: real keys and mouse to Excel and the browsers,
  and the IME switched on and off by the keyboard. **Do not stop to ask.** Say "starting" before the
  first input and "finished" after the last.
- Drive Excel as the thirteenth run did (`verification/2026-10-01-windows-13/excel/ask-excel.ps1`).
  Write this run's script as `verification/<date>-windows-15/excel/ask-excel.ps1`.
- **Keys go as virtual-key codes or scan codes, never as Unicode characters** (`KEYEVENTF_UNICODE`
  bypasses the IME). For Part B the IME is the Microsoft Japanese IME, romaji input, Hiragana mode,
  switched on and off by its key (`VK_IME_ON`/`VK_IME_OFF`, or 半角/全角), as a user does. Record the
  IME's version and mode. For Parts A and C the IME is off, English (UK) for Excel's window, as before.
- Record the Excel version, Chrome and Edge versions, Windows' mode and the display scale.

## Part A — VZ-14 at the current base

Run `tests/ExGrid.Browser/scrollbar.spec.mjs`, headed, under both browsers, on WebAssembly and on the
Server host, once at **125%** display scaling and once at **150%**, as the run of 2026-09-23 did
(`verification/2026-09-23-windows/results.md` has the commands and what it recorded). Tee each run to
`verification/<date>-windows-15/scrollbar-<scale>-<host>.log`. Record the gutter as the grid is told
it, and VZ-14's verdict.

## Part B — a real IME, ExSheet beside Excel

Pages: `/sheet` and `/sheet?chrome=mud` (the MudBlazor Chrome), under Chrome and Edge, on
WebAssembly, on the Server host, and on the Server host behind 150 ms (the latency proxy's
`setRoundTrip`, as the earlier runs used it). The probe reads the DOM after each key: the Cell Editor's
and the Formula Bar's value and selection, `document.activeElement`, the Name Box, which cell holds
the Focus, any completion list, the coloured layer's text, and every `compositionstart`,
`compositionupdate`, `compositionend` and `keydown` (with `isComposing` and `keyCode`) on the page.
Take a picture after each step that changes the screen.

**Excel, the same keys:** Book1 as in the thirteenth run (D10 selected), the IME switched on the same
way. Record the cell's and the Formula Bar's text, the IME's composition and candidate window (UI
Automation, or the picture), the status bar's mode and the active cell, after each step.

| # | Where | Keys (IME on unless said) | What is asked |
|---|---|---|---|
| i1 | D10 | `kana`, Space, Enter, Enter | After each key: the composition, the edit, the Focus. Expected of ExSheet (ED-11): nothing commits or moves while composing; the first Enter ends the composition, the second commits the cell and moves to D11 |
| i2 | D10 | `kana`, Escape, Escape | Does the first Escape end only the composition, leaving the edit open? The second cancels the edit, and D10 is unchanged |
| i3 | D10 | `kana`, Space, ↓, ↓, Enter, Enter | The arrows choose among the candidates; the Focus does not move and nothing points |
| i4 | the Formula Bar | press D10, press into the bar's end, `kana`, Space, Enter, Enter | As i1, in the bar; D10 holds what was chosen |
| i5 | D10 | IME off `=A1+`; IME on `a`; Escape; IME off `B1`, Enter | While `あ` is composed after `=A1+`, `A1` keeps its colour and the composition is shown once, not doubled or hidden (ADR-0057). After Enter, D10 holds `=A1+B1` |
| i6 | D10 | IME off `=`; IME on `a`; ↓; Escape; IME off ↓ | While composing, ↓ is the IME's: no outline, nothing written. After the composition ends, ↓ points at D11 (`=D11`) |
| i7 | the Name Box | press it, `kana`, Enter, then Escape | The first Enter ends the composition and goes nowhere |
| i8 | Find | Ctrl+F on `/sheet`, `kana`, Enter, then Escape | The first Enter ends the composition and finds nothing yet |
| i9 | D10 | IME off `=SUM(A2:A4,B2:B4,C2:C4,`; IME on `a`; Escape; IME off ↓ | After ↓, `=SUM(A2:A4,B2:B4,C2:C4,D11` is written and the caret is inside the Cell Editor's visible width (ticket 75) |
| i10 | D10, Server behind 150 ms | i1 and i4 typed at a typist's speed | The same reading as at 0 ms; no key lost or doubled |

Excel is asked i1–i4 and i7. For i5, i6 and i9, record Excel's screen too, where the keys mean the
same.

## Part C — ticket 74's readings, asked of Excel

Book1 as in the thirteenth run (the Table `Positions` in A1:B4), D10 selected, the IME off. Type each
into D10 and record any list under the edit (every item, in order, which is selected), any tip, and a
picture. Then type the same into ExSheet's D10 on `/sheet` (Chrome, WebAssembly) and record the same.

| # | Keys | ExSheet as decided (ADR-0058) |
|---|---|---|
| c1 | `=XLOOKUP(1,A2:A4,B2:B4,,1.0` | `1 - Exact match or next larger item` alone |
| c2 | `=XLOOKUP(1,A2:A4,B2:B4,,+1` | the same |
| c3 | `=XLOOKUP(1,A2:A4,B2:B4,,1 ` (a space after it) | the same |
| c4 | `=XLOOKUP(1,A2:A4,B2:B4,,-0` | `0 - Exact match` alone |
| c5 | `=XLOOKUP(1,A2:A4,B2:B4,,--1` | every value, the first selected |
| c6 | `=XLOOKUP(1,A2:A4,B2:B4,,50%` | every value, the first selected |
| c7 | `=XLOOKUP(1,A2:A4,B2:B4,,(` | nothing |
| c8 | `=XLOOKUP(1,A2:A4,B2:B4,,(A` | the functions beginning with A |
| c9 | `=XLOOKUP(1,A2:A4,B2:B4,,Positions[` | the table's columns |
| c10 | `=XLOOKUP(1,A2:A4,B2:B4,,Positions[Id]` | every value, the first selected |
| c11 | `=XLOOKUP(1,A2:A4,B2:B4,,(1)` | every value, the first selected |

## Finishing

Commit everything to `claude/exsheet-windows-verify-15` and push. The results go to
`verification/<date>-windows-15/`: the scripts, the `.jsonl`, the logs, `excel/shots/` and the
page's pictures, and one report with a row per case: what was read, and, where a reading is given,
whether it matches. The last message lists every difference, each with the ADR paragraph, DoD row
or ticket it belongs to. It proposes nothing on the user's behalf.
