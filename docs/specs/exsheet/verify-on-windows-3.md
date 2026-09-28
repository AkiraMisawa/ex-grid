# What to verify on Windows, third run

Status: ready-for-human — **Parts A and B can run now. Parts C and D wait for the gate below.**

For a Claude Code session on the Windows desktop used for the first two runs (Excel, Chrome, Edge,
WSL2 with nix). The second run's method, tools and advance authorisation all still apply: read
[`verify-on-windows-2.md`](verify-on-windows-2.md) first, and reuse
`verification/2026-09-27-windows-excel-2/` (`active-cell.ps1`, `by-hand.ps1`, `case-guard.ps1`,
the probes) and `tests/ExSheet.Engine.Tests/ExcelOracle/oracle.ps1` with its dialog watcher.
**Decide nothing. Record everything.** Do not change any ADR, `CONTEXT.md` or
`docs/definition-of-done.md`.

## Setup

- Fetch `claude/exsheet-start-8cx3v1`. Branch **`claude/exsheet-windows-verify-3`** from its tip, and
  record that tip as the verified commit of each part. Parts C and D may be verified at a later tip
  than A and B. Merge the branch's newer tip into `claude/exsheet-windows-verify-3` before them,
  and record both commits.
- Build and run layers 1–2 in WSL, as before. Record the counts.
- **The user has authorised this run in advance**, as for the second run: real keys and mouse to
  Excel and the browsers, and `Set-Culture` for as long as the oracle needs, restored and checked
  byte for byte against an export taken first. **Do not stop to ask.** Say "starting" before the
  first input and "finished" after the last.
- The Japanese IME takes Ctrl+Space. Switch Excel's window to the English (UK) keyboard for any
  step that sends it, and back after, as the second run's case 9 did.

## The gate for Parts C and D

Parts C and D test code that is still being merged. **Start them only when `git log
origin/claude/exsheet-start-8cx3v1` contains all four of these**. Search with `git log --grep`:

1. `ADR-0052` in a commit that implements it. Look for a merge of the active-cell work, and for
   the `test.fail(true, 'ADR-0052` markers being gone from `tests/ExGrid.Browser/sheet-vs-excel.spec.mjs`
2. `ADR-0053`, the compressed scroll height, with a `chrome-150` or similar project in
   `tests/ExGrid.Browser/playwright.config.mjs`
3. `GridPasteIntent.Refuse`
4. The Server Formula Bar fix (DC-19/DC-22)

If they are not all there when A and B finish, stop, push A and B, and say which are missing.
Part D's run is only worth its 40 minutes on the finished code.

## Part A — the oracle, new and uncertain cases

`oracle.ps1`, en-US, **COM and `-Keys`** as in the second run. Then the formats the cases name.
Then `-Update`.

- The corpus has about 1145 cases, with **more than 85 new `uncertain` ones**. They include:
  - the near-cancel boundary: ARITH-099..121 bracket where a final `+`/`-` goes to 0 and where
    `=` counts two numbers as equal;
  - more collation, number-to-text, Error Values, IFERROR, XLOOKUP regular expressions, the General
    format, typed constants, two-digit years, result formats, percent entry and format codes;
  - column widths: whether a typed **date** sets `customWidth`, and **whether a column a number
    widened is widened again by a longer number**.
- Cases marked "ask by keys" in `oracleSkip` are asked by keys only. That is where COM and the
  keyboard were seen to differ.
- Record the machine's **two-digit-year setting**. `1/1/30` typed was 2030 in the second run. Take
  the Windows value from `HKCU\Control Panel\International\Calendars\TwoDigitYearMax` (or the
  Region › Additional settings › Date dialog), and Excel's own under File › Options, if it has one.
- Results go to `verification/<date>-windows-excel-3/results.md`, in the second run's shape.
  Disagreements are listed, never fixed.

## Part B — what ADR-0052 still extrapolates

The second run's cases 1–15 settled ADR-0052. It still reads several things that were not observed.
Record them in the second run's shape: Selection, active cell, view, screenshots. Write them to
`verification/<date>-windows-excel-3/active-cell.md`.

1. **Shift+↓ after Enter has cycled into an earlier range.** Select A1:B2, Ctrl+drag D4:E5, then
   press Enter until the active cell is back in A1:B2. Then Shift+↓. Which range extends, and
   from which corner?
2. **The cycling order through disjoint ranges.** With A1:B2 and D4:E5 (created in that order),
   press Enter ten times, then Tab ten times, then Shift+Enter. Repeat with the ranges created in
   the other order. Record every active cell.
3. **Ctrl+click on the active cell when it is not the top-left**, in A1:C3:
   - the active cell B2 (reached by Enter), then Shift+↓;
   - C3 (reached by Shift+Enter);
   - C1 and A3.

   Also Ctrl+click on a cell of the range that does *not* hold the active cell, with two ranges.
   Where does the active cell go, and which fragment extends with Shift+↓?
4. **What the implementation read without Excel** (ADR-0052, "What the implementation settled"):
   - C3, Ctrl+Space, then Shift+↓ and Shift+→. Does anything change?
   - In A1:C3, Enter to A2 (on no corner), then Ctrl+. five times. Also B2 (inside), the same.
   - The order Excel lists the fragments in after a take-out (`Selection.Address`), and the order
     Enter then visits them.
   - Select B4:B2 from B4, fill down by the handle to B6, then read the active cell.
5. **Scrolling, for ADR-0053.** At 100% zoom, in an empty sheet, how many rows does one mouse-wheel
   notch move, and does one click on the scrollbar's arrow or track move by rows or by a page?
   Use real input and record the scroll row before and after.

## Part C — ExSheet beside Excel (after the gate)

As the second run's Part C, on both hosts and both browsers:

- `sheet-vs-excel.spec.mjs`. Items 2–5 and item 20's two fill probes should now pass
  without `test.fail`, and item 22 (rows inserted over a selected range) should pass.
- `clipboard-probe.mjs` in both directions. **The too-narrow Excel column must now be refused by
  name, and the Name Box must stay where it was** (F7, not F7:F9). Also check the
  `data-ex-grid="invariant"` marker again.
- `typing-probe-2.mjs` on the Server host at 0, 30 and 60 ms, twelve trials each. **Wait for the
  circuit to be connected** before the first click, the way `tests/ExGrid.Browser`'s fixtures do.
  The second run's "nothing landed" may have been the page not yet being interactive, so record
  which wait was used.

## Part D — layer 3, both browsers, both hosts, at 150% (after the gate)

The whole suite on both hosts, as in the second run, at the OS's 150%. At that scale the OS
already applies the Layout Ceiling. So the ordinary `chrome` and `msedge` projects exercise
ADR-0053, and **VZ-14, BIG-1, BIG-5, SH-2 and SH-18/DC-2/DC-3/DC-7 should now pass**. Also run the
new scale-forcing project (for example `chrome-150`) if the config has one, and say what it
does on Windows. Record every failure with its criterion ID. Rerun each failure three times, and
say whether it comes and goes.

Also do this:

- **The mouse wheel on `/wide` at 10⁶ rows.** Take one real wheel notch at the top and one near the
  end. Record the rows moved and whether the rows jitter on the Server host. ADR-0053 accepts a
  wheel step about 1.25 times Excel's at 150%. Record what it is.
- **Ctrl+Plus to 200% page zoom** on `/wide`, then Ctrl+End. Is the last row painted, and is the
  Focus whole on screen?

## Part E — optional, needs the user: other display scales

The bisect could not run at 100% and 125%. Changing the display scale needs a Windows sign-out,
which stops this session. **Only if the user chooses to**, after A–D are pushed:

1. The user sets 125% by hand and signs back in.
2. They start a new session with this page.
3. The session runs `scrollbar.spec.mjs`, `virtualisation.spec.mjs` and `sheet.spec.mjs` on both
   browsers against the WebAssembly host.
4. The same again at 100%.
5. The user restores 150%.

Record the results in `verification/<date>-windows-scales/results.md`.

## Finishing

Commit everything to `claude/exsheet-windows-verify-3` and push. The last message lists every
disagreement and failure, with the ticket, ADR or criterion it belongs to, and proposes nothing
on the user's behalf.
