# What to verify on Windows: where Excel's `=` stops counting two numbers equal

Status: done (2026-09-29, `verification/2026-09-29-windows-excel-equality/`). Excel's `=` matched
15 significant digits in every case. **A mistake in this procedure:** en-GB was not enough. The oracle
treats a case that names no culture as en-US, and blocks it elsewhere. The run asked through a copy
of the corpus marked en-GB instead.

For the Claude Code session on the Windows desktop of the earlier runs. The fourth run's method,
tools and advance authorisation still apply: read [`verify-on-windows-4.md`](verify-on-windows-4.md)
for the setup. **Decide nothing. Record everything.** Do not change any ADR, `CONTEXT.md` or
`docs/definition-of-done.md`.

## Why

The fourth run found Excel's `=` still counting `1+x` equal to 1 at x = 4.8E-15, and not at 5E-15
(ARITH-137..139, ARITH-107). Two readings fit every observation so far:

- **A relative threshold.** Two numbers are equal when their difference is below a fixed fraction
  of each, about 4.9E-15. This is how the engine works today, with the fraction at 4.44E-15.
- **Fifteen significant digits.** Each side is rounded to 15 significant digits, and the rounded
  values are compared.

The two readings disagree away from 1. The four new cases are chosen where they do:

| Case | Formula | Relative threshold | 15 significant digits |
|---|---|---|---|
| ARITH-146 | `=9+3E-14=9` | TRUE | FALSE |
| ARITH-147 | `=1+4E-15=1+6E-15` | TRUE | FALSE |
| ARITH-148 | `=1000+3.6E-12=1000` | TRUE | TRUE (a control) |
| ARITH-149 | `=9+5E-15=9` | TRUE | FALSE |

## Setup

- Fetch `claude/exsheet-start-8cx3v1`. Branch **`claude/exsheet-windows-verify-equality`** from its
  tip, and record that tip as the verified commit.
- Layers 1–2 need not be run.
- The user has authorised this run in advance: real keys to Excel, and no `Set-Culture` (en-GB is
  enough, since no case names a format). **Do not stop to ask.** Say "starting" before the first
  input and "finished" after the last.

## The run

`oracle.ps1 -Id` with ARITH-136 to ARITH-149, **through COM and with `-Keys`**, under the machine's
own en-GB. Then `-Update` for the same ids. Record each case's answer and Excel's value.

If ARITH-146, 147 and 149 are split between the two readings, or fit neither, also ask these by
hand, typed, and record what Excel shows:

- `=0.9+4E-16=0.9`
- `=99+4E-13=99`
- `=1+4.9E-15=1`

Results go to `verification/<date>-windows-excel-equality/results.md`, in the fourth run's shape.

## One more paste, by hand (two minutes)

This settles ADR-0014's amendment of 2026-09-29, which is unobserved on one point. Put the text `=A1`
on the clipboard from Notepad, as the fourth run's Part B item 3 did. Select B2:C3 **from C3** (click
C3, Shift+click B2), then press Ctrl+V. Record which cell gets `=A1`, and what the Selection and the
active cell are afterwards. Close only the Notepad tab you opened.

## Finishing

Commit everything to `claude/exsheet-windows-verify-equality` and push. The last message names
the reading that fits, or says that neither does, with each case's answer, and proposes nothing on
the user's behalf.
