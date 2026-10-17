# Numbers reserved per branch

Several branches are written at once, each by its own session. An ADR, a ticket, a Windows run or a
Sheet Document version takes a number. Two branches that take the same number never conflict in git,
because their files have different names. The collision only shows after both have merged, as two
ADR-0063s. So **a branch takes numbers only from a block reserved for it here.** It reserves the
block on `main` before it uses the first number.

- **To reserve:** add a row below, commit with `[skip ci]` in the message, and push to `main`. Take
  the next free block. Don't take a number between blocks. *(Until 2026-10-05 the reservations were
  pushed to `claude/exsheet-start-8cx3v1`, which is no longer worked on.)*
- **When a branch merges,** its row stays. Its unused numbers stay unused.
- **A number already used on two branches** is settled by moving the later branch's into its own
  block. The ADR says what it was numbered before.

## ADRs (`docs/adr/`)

| Numbers | Branch | Note |
|---|---|---|
| 0001–0058 | `claude/exsheet-start-8cx3v1` and earlier | in |
| 0059–0070 | `claude/expivot-mudblazor-wrapper-j25225` | ExPivot, PR #39 |
| 0071–0079 | `claude/exsheet-cell-format` | Cell Format. Its ADR was numbered 0063 until 2026-10-01 |
| 0080–0089 | `claude/exsheet-keyboard-field` | Pointing Scope's line: the Keyboard Field (ticket 79's way, decided 2026-10-02) |
| 0090–0099 | `claude/exsheet-part-c` | Part C of the eleventh Windows run: what ADR-0071 left to the next PR |
| 0100–0109 | `claude/trusting-babbage-1tpuqo` | The Sheet Toolbar: ticket 54 shipped as part of ExSheet (grilled 2026-10-02) |
| 0110–0119 | `claude/update-repository-description-40up8q` | The documentation site on GitHub Pages, and how its code examples are shown |
| 0120–0129 | `claude/excel-formulas-management-4clj80` | ExSheet's function catalogue: `TODAY` and `TEXT`, the P1 functions that need a decision |
| 0130–0139 | `claude/selection-summary` | The Selection Summary: Excel's status-bar figures over a selection (grilled 2026-10-05) |
| 0140–0149 | `claude/exgrid-live-data` | ExGrid's live data: the Row Key, live Grid Sources, and writes refused when what the user saw changed (grilled 2026-10-05) |
| 0150–0159 | `claude/live-data-next` | Live data continued: a pushed Window's vouch and ExPivot's report across redraws. ADR-0151 to ADR-0153 continue on `claude/live-data-best`; 0150 and 0154 were not taken there and stay unused |
| 0160–0169 | `claude/live-data-next-cc` | Live data continued, the Claude Code track run beside `claude/live-data-next` for comparison: the same tickets, decided separately. `claude/live-data-best`, the two merged on 2026-10-08, carries ADR-0160 and ADR-0161 and continues this block: 0162 is next |
| 0170–0179 | `claude/modified-mark-and-copy-range` | The Copied Range: a copy outlined with dashes while the clipboard still holds it (decided 2026-10-07) |
| 0180–0189 | `claude/backlog-cleanup` | ExSheet's last Decide functions: the random functions (ADR-0180) and `LET` (ADR-0181), decided with the user 2026-10-10; a remote report's read-ahead (ADR-0182) and the Range Request Margin (ADR-0183), decided 2026-10-11 |
| 0190–0199 | `claude/docs-site-releases-roadmap` | The Docs Site restructured after mudblazor.com and ag-grid.com: Release Notes, the Roadmap, and how the site is published (decided with the user 2026-10-11) |
| 0200–0209 | `claude/first-stable-release` | What the first stable version needs: its scope, the public C# surface, the Blazor Server declaration, and ADR-0042 rewritten at sign-off (decided with the user from 2026-10-11) |
| 0210–0219 | `codex/handoff-toolbar` | More Colours opening keyboard and committing held text (decided with the user, 2026-10-14) |
| 0220–0229 | `claude/settled-signal` | A test reads once the grid says it has settled, and a red is never settled by a re-run (decided with the user, 2026-10-16) |
| 0230–0239 | `claude/races-220-224` | Tickets 220 to 222: a key typed at once after a change of state means what it means typed slowly (decided with the user, 2026-10-17) |
| 0240– | free | reserve a block of ten |

## ExSheet tickets (`docs/specs/exsheet/issues/`)

| Numbers | Branch | Note |
|---|---|---|
| 01–43, 59–75 | `claude/exsheet-start-8cx3v1` and the branches merged into it | in |
| 44–58 | `claude/exsheet-cell-format` | Cell Format |
| 76–80 | `claude/exsheet-after-run-13` and its successors | Pointing Scope's line. 76 is PR #41 |
| 81–99 | `claude/exsheet-cell-format` | Cell Format, continued |
| 100–119 | `claude/exsheet-cell-format` | Cell Format, the fourteenth Windows run's answers |
| 120–139 | `claude/exsheet-after-run-16` and its successors | Pointing Scope's line, continued: the sixteenth Windows run's answers |
| 140–159 | `claude/exsheet-part-c` | Part C of the eleventh Windows run: what ADR-0071 left to the next PR |
| 160–179 | `claude/trusting-babbage-1tpuqo` | The Sheet Toolbar |
| 180–199 | `claude/dod-gaps-sheet-pivot-tests` | Closing the Definition of Done's unproven ExSheet rows before 0.1.0: 180 is a structured reference written in its table's declared case (decided with the user 2026-10-13, after 0.1.0) |
| 200–219 | `claude/settled-races` | Two Server-host races found by run 18a's F4 spec (#128): keys typed after the Name Box names the current cell, and a press right after Enter (2026-10-16) |
| 220–239 | `claude/spec-lint-races` | The waits between inputs the spec lint kept (#137), each a suspected race, made tickets with the user (ADR-0220 §2, 2026-10-17) |
| 240– | free | reserve a block of twenty |

Another spec's tickets (`docs/specs/<feature>/issues/`) are numbered within that spec, and are not
reserved here.

## Windows runs (`docs/specs/exsheet/verify-on-windows-N.md`, branch `claude/exsheet-windows-verify-N`)

| Numbers | Branch | Note |
|---|---|---|
| 1–13 | written and run | the thirteenth is Pointing Scope's |
| 14 | `claude/exsheet-cell-format` | Cell Format, Excel only |
| 15 | `claude/exsheet-ime-and-scaling` | a real Japanese IME, VZ-14 at the current base, and ticket 74's readings asked of Excel |
| 16 | `claude/exsheet-keyboard-field` | a real Japanese IME on a selected cell (the Keyboard Field), and the Name Box and Escape cases left by the fifteenth |
| 17 | `claude/windows-run-17` | Before the release candidate: ExSheet's corpus, SH-28, SH-51/52, tickets 15 to 18's questions and ExPivot's 28 readings put to Excel, the screen readers, and the record of ExGrid's by-hand rows (decided with the user 2026-10-11; ADR-0208) |
| 18 | `claude/run-18-spec` (spec), `claude/exsheet-windows-verify-18` (the run) | The release candidate's run: ExGrid's by-hand rows renewed on `0.1.0-rc.1`, and what run 18a and its corrections left open (2026-10-17) |
| 19– | free | reserve one at a time |

## Sheet Document versions (`SheetDocument.CurrentVersion`)

| Version | Branch | What it added |
|---|---|---|
| 1–6 | in | — |
| 7 | in (Pointing Scope) | a Linked Table's key (ADR-0049) |
| 8 | `claude/exsheet-cell-format` | Font, Fill and Borders (ADR-0071, numbered 0063 before) |
| 9 | `claude/excel-formulas-management-4clj80` | a Linked Table's key of several columns (ADR-0058, amended 2026-10-03) |
| 10– | free | reserve one at a time |
