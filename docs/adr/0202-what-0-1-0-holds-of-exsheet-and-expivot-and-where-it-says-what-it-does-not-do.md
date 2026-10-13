# What 0.1.0 holds of ExSheet and ExPivot, and where it says what it does not do

*(Decided with the user on 2026-10-11, after
[ADR-0200](./0200-the-first-stable-version-is-0-1-0-and-carries-the-whole-family.md) put ExSheet,
ExPivot and the Snapshot's packages into `0.1.0`. It amends
[ADR-0190](./0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md),
the Definition of Done's §27 and §29.)*

Once `0.1.0` carries ExSheet and ExPivot stable, their own sections gate the tag, and several things
those sections allowed while the products were built are no longer enough:

- **Five of ExSheet's functions are decided and not built**: `LET` (ADR-0181), `RAND` and
  `RANDBETWEEN` (ADR-0180), `SUBTOTAL` and `AGGREGATE`, `ERROR.TYPE` and `INDIRECT` — tickets 14 to 18
  of `docs/specs/exsheet-functions`. SH-66 to SH-68 are MUSTs. Today each name is `#NAME?`.
- **264 of ExSheet's Excel cases are the engine's own answer, never asked of Excel** (`source:
  "uncertain"`, in 98 of the 157 files under `tests/ExSheet.Engine.Tests/ExcelCases/`), and 394 more were
  read from Microsoft's documentation. SH-7 asks for "Excel's observed results". About 228 of the
  uncertain cases are functions' (`TEXT`, `DATE`, `INDEX`, `PMT`, `MOD` among them); 36 are input,
  format inference, column widths and undo, which no Error Value can refuse.
- **Two MUSTs of §27 need Windows by hand and have never had it**: SH-28's edge cases of F4, which were
  to be read until the seventh Windows run, a run that was superseded and never made; and SH-51 and
  SH-52's keys reaching the page and the Home tab's KeyTips (ticket 160).
- **ExPivot's 28 "as Excel" readings have never been run beside Excel** (ticket 07,
  `docs/specs/expivot/excel-behaviours.md`). §29 lets "as Excel" mean "as read here" until a run settles
  it. Twelve of them change a number on screen: items 2 to 8 and 15 to 17 (grouping text without regard
  to case, `(blank)`, Count, the edges of Average, StdDev and Var, Product, totals computed from the
  records, empty cells, % of Total, fifteen digits, hidden items).
- **Some things are decided not to be done, and nothing says so where a reader would look**:
  `IRR`, `XIRR` and `RATE` (ticket 13: no iteration found matches Excel to fifteen digits, decided
  2026-10-10); dragging a Reference Outline (ticket 31, held by ADR-0057); and ExPivot's speed near its
  combination cap, which misses PV-21's targets (a new question takes seconds, the page can stop for
  0.1 to 0.2 s). The Release Notes' fixed headings
  ([ADR-0190](./0190-release-notes-are-written-with-each-change-and-published-from-the-repository.md))
  have no place for any of it.

## The decision

### What `0.1.0` holds

- **The five functions are built before the tag**, as their tickets say. SH-66 to SH-68 stand.
- **`IRR`, `XIRR` and `RATE` are not in `0.1.0`.** They stay `#NAME?`, as decided on 2026-10-10:
  a financial function that answers close to Excel is a plausible wrong number, which is worse than no
  answer. Ticket 13 stays `needs-info`.
- **Dragging a Reference Outline is not in `0.1.0`.** The outline is drawn and no corner squares are, so
  no gesture is offered that does nothing (ADR-0057). Ticket 31 stays `needs-triage`.
- **ExPivot's speed near the cap is recorded, not changed.** PV-21 is OBSERVATIONAL and never gates; its
  numbers are recorded in Step 6, and what a user meets is said (below).

### The Windows run before the tag

The run ExGrid's own by-hand rows need anyway (VZ-14, VZ-16, FN-12e, ED-30, SM-7) also carries:

- **ExSheet's whole case corpus, put to Excel**: `oracle.ps1 -Update` over every `uncertain` and
  `documented` case, which turns each one that matches into `observed`. A case in a culture other than
  the machine's is run under that regional format.
  - **A function that disagrees is corrected before the tag, or the arguments where it disagrees are
    refused with an Error Value**, as `MOD` refuses a quotient of 2^27 or more (ADR-0047). A width, a
    format or an undo that disagrees is corrected.
  - SH-7 is then met as written: every case observed.
- **SH-28's edge cases, and SH-51 and SH-52 by hand** (Part A and B of the seventh run's script, and
  ticket 160's readings). A disagreement is settled by correcting ADR-0051 or ADR-0100 first, and the
  code after it, before the tag.
- **The questions tickets 15 to 18 leave for Excel**: what an undo does to a draw, what counts as nested
  under `SUBTOTAL`, `ERROR.TYPE` of the values outside its table, and each form `INDIRECT` refuses.
- **All 28 of ExPivot's readings, beside Excel** (ticket 07).
  - **The twelve that change a number gate the tag**: a disagreement is settled by correcting the ADR
    first and the code after it.
  - **A disagreement in layout or wording is a known limitation of `0.1.0`** (below), corrected after it.

### Where `0.1.0` says what it does not do

- **On the product's Docs Site page, where a reader meets the feature.** The functions page says which
  of Excel's functions are `#NAME?` and why; ExPivot's pages say what has not been run beside Excel and
  where it slows down; ExSheet's Formulas page says that a Reference Outline cannot be dragged. The
  Roadmap lists the tickets as it already does (ADR-0191).
- **In a stable version's Release Notes, under "Known limitations"** — a heading ADR-0190 did not have.
  It is required of a version without a suffix, after "Behaviour changes", and says "None." when empty.
  Each line names the limitation in a sentence and links the page that says it in full. This amends
  ADR-0190's fixed headings and the Docs Site's check of them.

## Considered options

- **Leave the five functions out, `#NAME?`.** Honest, but it would undo MUSTs decided the day before,
  and the work needed no further decision.
- **Ship the unobserved cases, and say which functions have them.** It would relax SH-7, and a `PMT`
  edge case would remain the engine's guess in a stable financial function.
- **Refuse every unobserved case with an Error Value before asking Excel.** It removes answers that may
  be right, and 36 of the cases cannot be refused that way.
- **Accept SH-28, SH-51 and SH-52 as read, and say they were not checked.** Whether Alt opens the
  browser's menu on Windows is known only on Windows, which is where Excel's users are.
- **Gate all 28 of ExPivot's readings, or none.** All would hold the tag on a label's wording; none would
  ship a pivot whose totals were never compared with Excel's.
- **`IRR`, `XIRR` and `RATE` within Microsoft's documented tolerance.** It would rewrite the
  fifteen-digit rule for a financial function, and answer differently from Excel.
- **Known limitations kept only on the pages and the Roadmap.** A reader of the Release Notes, who is
  deciding whether to upgrade, would not see them.

## Consequences

- The Definition of Done's §27 and §29 carry dated notes: the corpus observed, SH-28's edge cases and
  SH-51/52 run by hand, and ticket 07's twelve numeric readings, before `0.1.0`.
- ADR-0190 gains the "Known limitations" heading for stable versions, and the Docs Site's notes parser
  requires it of an unsuffixed version.
- The sign-off's Windows run is one script, numbered from the Windows-run block when it is written, and
  holds ExGrid's by-hand rows, the corpus, the seventh run's Parts A and B, ticket 160's readings,
  tickets 15 to 18's questions and ticket 07.

  *(2026-10-11, decided with the user: **the run is split in two.** The questions put to Excel, and the
  screen readers, are asked before the release candidate, by the seventeenth Windows run
  (`docs/specs/exsheet/verify-on-windows-17.md`). That run covers the corpus, the seventh run's Parts A
  and B, ticket 160's readings, tickets 15 to 18's questions, ticket 07 and SM-7, and it dry-runs
  ExGrid's by-hand rows. A disagreement found there can then be corrected before the tag, not after a
  candidate. Excel's answers do not depend on the build. What the corpus observes is held at every
  commit after it by layer 1. The eighteenth run runs ExGrid's by-hand rows (VZ-14, VZ-16, FN-12e,
  ED-30), on `0.1.0-rc.1` itself. Those rows count only there.)*

  *(Corrected the same day, decided with the user,
  [ADR-0208](./0208-a-question-to-excel-is-asked-when-its-feature-is-built-and-a-check-by-hand-is-repeated-only-when-its-code-changes.md):
  the seventeenth run's Part G is the record of those rows, not a dry run. The eighteenth runs only the
  rows whose code changes between the seventeenth's commit and `0.1.0-rc.1`. If none changes, there is
  no eighteenth run.)*
- *(2026-10-12, decided with the user.)* **`NPV`, and `PMT` at small rates, are asked of Excel before
  they are corrected or refused.** The seventeenth run found `NPV` four and thirty-two units in the last
  place from Excel on two observed cases, and no evaluation searched gives all three observed values.
  `PMT` gives every observed value to the bit, but at a rate of 10^-7 the evaluations that fit the
  observations part from the thirteenth significant digit, and no run has observed such a rate. Both go
  to the one `oracle.ps1 -Area` pass before `0.1.0-rc.1` (ADR-0208's note of this date), and the answers
  decide. If no such pass can be made before the tag, `NPV` is refused for `0.1.0` as `IRR` is, listed
  under Known limitations, and `PMT` stays as it is.
- `0.1.0`'s Known limitations will hold at least: `IRR`, `XIRR` and `RATE`; dragging a Reference Outline;
  ExPivot's speed near its cap; and whatever of ExPivot's layout and wording the run finds different.
  *(2026-10-12, decided with the user: also a whole-column `SUM` on every row of a large Sheet. SH-19 is the
  case it decided, a 10,000 × 20 block with its twenty whole-column totals beside it, where an edit takes
  tens of milliseconds; with such a `SUM` on every row instead, an edit was measured at about 40 to 48
  seconds and holds the circuit that long. `verification/2026-10-11-linux-sh19/` has both.)*
  *(2026-10-12, decided with the user: also ExGrid's VZ-18 on the remote-isolation PC. The
  repaint after a reveal is in, and its layer-3 test gates; the check on that PC is not run for `0.1.0`,
  and the observation of 2026-10-08, which names no commit, is not taken as its record.)*
  *(2026-10-12, decided with the user: also the Number and Date widths under a Windows display language
  other than English and Japanese. `system-ui` is the face Windows sets for its display language, and only
  those two were measured (FN-12e, ADR-0016); under another, a glyph its face paints wider than the table
  charges is charged under what it paints.)*
  *(2026-10-12, decided with the user: also how far ExSheet widens a column for an italic number over `####`. Over
  123456789.12 in `0.00` at the standard width, Excel's Format Cells' Font tab widened column A to
  12.45 for Italic alone, and to 11.73 for a single underline or a colour alone (the readings of
  2026-10-12 (R6)). ExSheet widens an italic number to the width the upright one takes, because
  ADR-0071 judges italic by the regular widths. It is a difference that follows ADR-0071, not a
  change.)*
