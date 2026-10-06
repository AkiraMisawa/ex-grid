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
| 0150–0159 | `claude/live-data-next` | Live data continued: a pushed Window's vouch and ExPivot's report across redraws |
| 0160–0169 | `claude/live-data-next-cc` | Live data continued, the Claude Code track run beside `claude/live-data-next` for comparison: the same tickets, decided separately |
| 0170– | free | reserve a block of ten |

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
| 180– | free | reserve a block of twenty |

Another spec's tickets (`docs/specs/<feature>/issues/`) are numbered within that spec, and are not
reserved here.

## Windows runs (`docs/specs/exsheet/verify-on-windows-N.md`, branch `claude/exsheet-windows-verify-N`)

| Numbers | Branch | Note |
|---|---|---|
| 1–13 | written and run | the thirteenth is Pointing Scope's |
| 14 | `claude/exsheet-cell-format` | Cell Format, Excel only |
| 15 | `claude/exsheet-ime-and-scaling` | a real Japanese IME, VZ-14 at the current base, and ticket 74's readings asked of Excel |
| 16 | `claude/exsheet-keyboard-field` | a real Japanese IME on a selected cell (the Keyboard Field), and the Name Box and Escape cases left by the fifteenth |
| 17– | free | reserve one at a time |

## Sheet Document versions (`SheetDocument.CurrentVersion`)

| Version | Branch | What it added |
|---|---|---|
| 1–6 | in | — |
| 7 | in (Pointing Scope) | a Linked Table's key (ADR-0049) |
| 8 | `claude/exsheet-cell-format` | Font, Fill and Borders (ADR-0071, numbered 0063 before) |
| 9 | `claude/excel-formulas-management-4clj80` | a Linked Table's key of several columns (ADR-0058, amended 2026-10-03) |
| 10– | free | reserve one at a time |
