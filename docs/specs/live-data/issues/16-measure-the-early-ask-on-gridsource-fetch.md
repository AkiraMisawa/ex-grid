# 16: Measure the early ask and the covering question on GridSource.Fetch's own path

Status: ready-for-agent
Roadmap: ExGrid — the bundled fetching source may ask for rows early and keep a covering fetch in flight, once measured

**What to do:** measure what [ADR-0182](../../../adr/0182-a-remote-reports-window-is-read-ahead-sixty-rows-either-side.md)'s
covering rule and [ADR-0183](../../../adr/0183-the-grid-may-ask-for-rows-before-the-viewport-runs-out-of-them.md)'s
Range Request Margin would buy ExGrid's bundled fetching source, `GridSource.Fetch`. Both ADRs leave that path as
[ADR-0025](../../../adr/0025-what-the-bundled-fetching-source-promises.md) has it until it is measured on its own.
Then, once the user has decided, build what was decided.

**Blocked by:** none. It follows tickets 14 and 15, which measured and built the same two rules on ExPivot's remote
report path.

## Where it stands

*(Read on `claude/remote-read-ahead`, 2026-10-11.)*

- **`GridSource.Fetch` reads 60 rows either side by default** (`readAheadRows`), clamped to the total, and asks
  nothing while its Window covers the range the grid names (ADR-0025).
- **Any different range cancels the fetch in flight**, whether or not that fetch's range covers the new one. The
  answer to the cancelled fetch is discarded by generation.
- **It sets no Range Request Margin** (FN-3a). A Consumer can still give its grid one. The early request then finds
  the rows on screen in the source's Window and fetches nothing, so the margin changes nothing there
  (`RangeRequestMarginTests.ADR0183_GridSource_Fetch_fetches_nothing_for_an_early_request`).
- **On ExPivot's path, the covering rule decided the outcome.** With 60 rows read ahead at 150 ms a round trip,
  cancelling at every new range left Placeholders for 85–88% of a held scroll. Keeping a covering question brought
  that to 19–23% (wheel) and 8–10% (ArrowDown), and asking 10 rows early brought it to 8–13% and 0%
  (`verification/2026-10-10-linux-remote-read-ahead/`, `verification/2026-10-11-linux-remote-read-ahead-blocks/`).
  Nothing has measured `GridSource.Fetch`, whose fetches, rows and Window are its own.

## What to measure first

The numbers are observational and never gated, and they are recorded in `verification/`. Add a mode and measure
rather than assert from reasoning. Use the bundled fetching source over a remote fetch in a published WebAssembly
build, with 150 ms added to each round trip, as the two earlier records did. The lab branch `lab/remote-read-ahead`
(21d7a94, on origin, never merged) holds their harness. Measure four builds at `readAheadRows` = 60:

- ADR-0025 as it is;
- with a fetch in flight kept while its range covers the rows on screen (ADR-0182's rule);
- with the source acting on an early request at a margin of 10, and asking when the rows on screen plus the margin
  are not all in its Window (ADR-0183's rule);
- with both.

For each, record:

- **How long Placeholders are on screen** while scrolling by the wheel, by a held ArrowDown, by ArrowDown one press
  at a time, and by a held PageDown;
- **How many fetches** each scroll asks, and how many of them are cancelled before they answer;
- **The rows fetched per row scrolled.**

## Decisions it needs

These are the user's, once the measurement is in.

- **Whether `GridSource.Fetch` keeps a covering fetch in flight**, departing from ADR-0025's rule as ExPivot does.
- **Whether it sets its grid's margin, and to what**: ExPivot's "the smaller of 10 and the read-ahead", or none.
  - That would change FN-3a's "`GridSource.Fetch` sets no margin" and ADR-0025's row "The Window already covers it:
    nothing".
- **Where it is recorded:** a section of ADR-0025 and a note in ADR-0183, or an ADR of its own from the deciding
  branch's block (`docs/agents/numbering.md`).

## Done when

- [ ] The measurement is recorded, and the user has decided
- [ ] The decision is in an ADR, and the Definition of Done says what holds
- [ ] Built, if anything is to be built, with layers 1 and 2 green
- [ ] Layer 3 counts the fetches while it scrolls, and never reads a time
- [ ] The Docs Site's Data sources page says what `GridSource.Fetch` does with a margin

## Comments

2026-10-11: Written on `claude/remote-read-ahead` from ticket 15's last box, which asked for this measurement as a
ticket of its own.
