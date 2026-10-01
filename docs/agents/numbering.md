# Numbers reserved per branch

Several branches are written at once, each by its own session. An ADR, a ticket, a Windows run or a
Sheet Document version takes a number. Two branches that take the same number never conflict in git,
because their files have different names. The collision only shows after both have merged, as two
ADR-0063s. So **a branch takes numbers only from a block reserved for it here.** It reserves the
block on `claude/exsheet-start-8cx3v1` before it uses the first number.

- **To reserve:** add a row below, commit, and push to `claude/exsheet-start-8cx3v1`. Take the next
  free block. Don't take a number between blocks.
- **When a branch merges,** its row stays. Its unused numbers stay unused.
- **A number already used on two branches** is settled by moving the later branch's into its own
  block. The ADR says what it was numbered before.

## ADRs (`docs/adr/`)

| Numbers | Branch | Note |
|---|---|---|
| 0001–0058 | `claude/exsheet-start-8cx3v1` and earlier | in |
| 0059–0070 | `claude/expivot-mudblazor-wrapper-j25225` | ExPivot, PR #39 |
| 0071–0079 | `claude/exsheet-cell-format` | Cell Format. Its ADR was numbered 0063 until 2026-10-01 |
| 0080– | free | reserve a block of ten |

## ExSheet tickets (`docs/specs/exsheet/issues/`)

| Numbers | Branch | Note |
|---|---|---|
| 01–43, 59–75 | `claude/exsheet-start-8cx3v1` and the branches merged into it | in |
| 44–58 | `claude/exsheet-cell-format` | Cell Format |
| 76–80 | `claude/exsheet-after-run-13` and its successors | Pointing Scope's line. 76 is PR #41 |
| 81–99 | `claude/exsheet-cell-format` | Cell Format, continued |
| 100– | free | reserve a block of twenty |

Another spec's tickets (`docs/specs/<feature>/issues/`) are numbered within that spec, and are not
reserved here.

## Windows runs (`docs/specs/exsheet/verify-on-windows-N.md`, branch `claude/exsheet-windows-verify-N`)

| Numbers | Branch | Note |
|---|---|---|
| 1–13 | written and run | the thirteenth is Pointing Scope's |
| 14 | `claude/exsheet-cell-format` | Cell Format, Excel only |
| 15 | `claude/exsheet-ime-and-scaling` | a real Japanese IME, VZ-14 at the current base, and ticket 74's readings asked of Excel |
| 16– | free | reserve one at a time |

## Sheet Document versions (`SheetDocument.CurrentVersion`)

| Version | Branch | What it added |
|---|---|---|
| 1–6 | in | — |
| 7 | in (Pointing Scope) | a Linked Table's key (ADR-0049) |
| 8 | `claude/exsheet-cell-format` | Font, Fill and Borders (ADR-0071, numbered 0063 before) |
| 9– | free | reserve one at a time |
