# What to verify on Windows, fourth run

Status: ready-for-human — **A, B, C, D in order** (E is optional).

For the Claude Code session on the Windows desktop of the first three runs (Excel, Chrome, Edge,
WSL2 with nix). The third run's method, tools and advance authorisation all still apply: read
[`verify-on-windows-3.md`](verify-on-windows-3.md) first, and reuse
`verification/2026-09-28-windows-excel-3/` (`active-cell.ps1`, `main-keys.ps1`, `by-hand.ps1`,
`clipboard-probe.mjs`, `typing-probe-2.mjs`), `verification/2026-09-28-windows-3/` (`wide-probe.mjs`,
`os-input.ps1`, `window-shot.ps1`) and `tests/ExSheet.Engine.Tests/ExcelOracle/oracle.ps1` with its
dialog watcher. **Decide nothing. Record everything.** Do not change any ADR, `CONTEXT.md` or
`docs/definition-of-done.md`.

## Setup

- Fetch `claude/exsheet-start-8cx3v1`. Branch **`claude/exsheet-windows-verify-4`** from its tip and
  record that tip as the verified commit of every part. If the tip moves during the run, do not
  merge it in; finish on the commit you started from.
- Build and run layers 1–2 in WSL, as before. Record the counts.
- **The user has authorised this run in advance**, as for the third: real keys and mouse to Excel
  and the browsers, and `Set-Culture` for as long as the oracle needs, restored and checked byte for
  byte against an export taken first. **Do not stop to ask.** Say "starting" before the first input
  and "finished" after the last.
- The Japanese IME takes Ctrl+Space. Switch Excel's window to the English (UK) keyboard for any
  step that sends it, and back after.
- `oracle.ps1` gained a `widthAtLeast` expectation (CW-028). The third run's copy does not have it:
  use the verified commit's.

## Part A — the oracle, the cases the third run opened

`oracle.ps1`, en-US, **COM and `-Keys`**, then the formats the cases name, then `-Update`, as in
the third run. The corpus has **1163 cases, 48 of them `uncertain`**. The ones the third run's
decisions added (ADR-0047, "What the third observation settled"):

- **The two near-cancel boundaries**: ARITH-136..139 bracket where `=` stops counting `1+x` equal to
  1 (4.2E-15 to 4.8E-15), ARITH-140..142 where a final subtraction stops being 0 (1.3E-15 to 1.8E-15,
  the last exactly 2⁻⁴⁹).
- **The square root**: ARITH-143, `=3^0.5`. The engine takes the square root for an exponent of 0.5
  only; this asks whether Excel does so beyond `=2^0.5`.
- **Collation**: ARITH-144, 145 (an apostrophe in `>`, `ß` against `ss` in `<`).
- **Regular expressions**: XLOOKUP-156 (a negative lookbehind), XLOOKUP-157 (`(?<=a+)b`, refused
  by the engine).
- **Format codes**: FMT-078, `[Color3]0`. **Also ask FMT-076, 077 and 078 typed**: in Excel's Format
  Cells › Custom, type the code and press OK, and record whether Excel takes it or what it says.
  The oracle sets formats through COM only, and the refusal was observed only that way.
- **Result formats**, by keys only: FF-030..035.

Record the two-digit-year setting again only if it changed. Results go to
`verification/<date>-windows-excel-4/results.md`, in the third run's shape. Disagreements are listed,
never fixed.

## Part B — Excel's answers the implementation still reads

Record each as the third run's `active-cell.md` did (Selection, active cell, view, screenshots),
in `verification/<date>-windows-excel-4/active-cell.md`.

1. **A take-out of the whole range made last.** Select A1:B2, Ctrl+click F6, then Ctrl+click F6
   again. What is selected and which cell is active? Then Shift+↓. Repeat with A1:B2, Ctrl+drag
   D4:E5, then Ctrl+drag D4:E5 again over the same cells. ExSheet reads "the latest range still
   standing takes its place", so the active cell would be A1 in both (ADR-0052, the third run's
   section).
2. **Ctrl+Enter from a Focus that is not the top-left.** Select B2:C3 from C3 (click C3, Shift+click
   B2), type `=B2+$A$1`, Ctrl+Enter. Record the four Formulas. Then the same from B3 (reached by Enter
   inside B2:C3). ExSheet shifts from the Focus (ADR-0050, SH-27).
3. **A Formula pasted as plain text over a range.** Put the text `=A1` on the clipboard from Notepad,
   select B2:C3 in Excel, Ctrl+V. Record the four cells. ExSheet writes the same text into each cell
   (only a Ctrl+Enter shifts). This is for the record; nothing is expected.

## Part C — ExSheet beside Excel

As the third run's Part C, on both hosts and both browsers:

- `sheet-vs-excel.spec.mjs`. **Items 3 and 5 and active-cell cases 7 and 8 should now pass at the
  display's 150%**: the tests scroll through ADR-0053's mapping. Case 6's title now names the
  take-out rule.
- `main-keys-probe.mjs` and `main-keys.ps1` for **Ctrl+Enter only**: `=A1` over B2:C3 should now give
  `=A1`, `=B1`, `=A2`, `=B2` in ExSheet as in Excel (SH-27). Also the two Part B item 2 cases in
  ExSheet.
- **A typed entry the engine refuses**: type `-B2 C2` into a cell of `/sheet` and press Enter.
  ExSheet should keep the editor open and name the intersection operator. Record what shows.
- **Column widths** (SH-26): in `/sheet`, type `1234567890` in an empty column's cell, then
  `12345678901` below it. The column should widen twice. Then drag the column narrower, type
  `123456789012` below, and record that it no longer widens. Do the same in Excel for comparison.

## Part D — layer 3, both browsers, both hosts, at 150%

The whole suite on both hosts, as in the third run, at the OS's 150%, with `chrome-150`. **MK-6,
`sheet-vs-excel` items 3 and 5 and cases 7 and 8 should pass on every project; BIG-5 on Server's
`chrome-150` should pass, and so should the new BIG-5 race test**, whose failure was the product
race behind the third run's "rows 0–20". Record every failure with its criterion ID and rerun each
three times.

Then, for the three that came and went in every earlier run, measure how often:

- **FN-6a** (`sizing.spec.mjs`), **DC-19/DC-34** (`declarations.spec.mjs`, the mud Chrome case) and
  **WR-6** (`mud-app.spec.mjs`, Striped): each with `--repeat-each=10` on its failing host and
  project from the third run (FN-6a WebAssembly Chrome and Edge; DC-19/34 and WR-6 Server Chrome).
  Record the count and, for each failure, the trace's last screenshot and the assertion's received
  value.

And, with real input:

- **A scroll to the end before the grid knows its ceiling** (ADR-0053, "Settled after the third
  Windows run"). Open `/wide` in a fresh tab on each host and browser, and as soon as the first rows
  are painted drag the scrollbar's thumb to the bottom with the real mouse (or press Ctrl+End after
  one click in the grid). Is the last row painted, and where is the thumb? Five tries each.

## Part E — optional, needs the user: other display scales

As in the third run's Part E, unchanged, only if the user chooses to.

## Finishing

Commit everything to `claude/exsheet-windows-verify-4` and push. The last message lists every
disagreement and failure, with the ticket, ADR or criterion it belongs to, and proposes nothing
on the user's behalf.
