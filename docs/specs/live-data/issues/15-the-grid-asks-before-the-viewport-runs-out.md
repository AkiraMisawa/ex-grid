# 15: The grid asks for rows before the Viewport runs out of them

Status: done

**What to build:** the Range Request Margin of
[ADR-0183](../../../adr/0183-the-grid-may-ask-for-rows-before-the-viewport-runs-out-of-them.md).
- ExGrid takes a margin of M rows, 0 by default. At M, it raises its Range Request as soon as the
  rows on screen plus M either side, clamped into the total, are not all in the Window. The request
  still names the rows on screen.
- ExPivot sets its grid's margin to the smaller of 10 and its report source's read-ahead. It keeps a
  question in flight while that question's Window covers the rows on screen.

**Blocked by:** ticket 14 for ExPivot's half: the read-ahead the margin relies on. The grid's half can
start now.

- [x] ExGrid: the margin parameter, 0 by default. At 0 nothing changes (FN-3). At M, the early request
      names the rows on screen and is not repeated until the Window moves (FN-3a)
- [x] Layer 2 counting invocations: at M = 0 and M = 10, near both edges, at the total's ends, and
      with a Consumer that answers exactly the rows asked (it is asked again at the next move, which is
      why the default is 0)
- [x] ExPivot: the margin from its source's read-ahead, and the covering question kept in flight; the
      lab branch's 21d7a94 shows the condition, and its test fails under the first build's
- [x] Layer 3 over `/pivot-db`, or a page like the lab's `/lab-read-ahead`, behind the latency proxy:
      count the report source's questions while scrolling, never a time
- [x] The Docs Site: ExGrid's page on Range Requests and the Window says what the margin does and why
      it is 0 by default, with an Example; ExPivot's Sources page says a remote source asks early
- [x] `GridSource.Fetch` is left as ADR-0025 has it. Whether it takes the margin is measured first on
      its own path, as a ticket of its own:
      [16](16-measure-the-early-ask-on-gridsource-fetch.md)

## Comments

2026-10-11: written with the user's decision, ADR-0183, from the second record's numbers
(`verification/2026-10-11-linux-remote-read-ahead-blocks/`). The lab branch's `RangeMarginRows` and
`ReadAheadTriggerRows` are the measured prototype, not the product's names.

2026-10-11: Built on `claude/remote-read-ahead` after ticket 14, test-first. Every box is ticked but
layer 3's: the spec is written and has not run yet.
- **The parameter is `RangeRequestMargin`,** an `int` of rows on `ExGrid`. Neither ADR-0183 nor the
  glossary names the parameter. This one is the glossary's term, as `PageSize` and
  `PinnedColumnCount` are named, without a unit suffix. A negative margin is refused by name. It is not
  among the push parameters a Source replaces: it applies to both forms, by the one path (FN-4).
- **The grid** checks the rows on screen plus the margin, clamped into the total, against the Window,
  and still names the rows on screen. The existing dedupe is unchanged: the same rows are not asked
  for again until the Window moves. With a margin this is what asks again once a Window lands that
  holds the rows on screen and not the margin. A Consumer that answers exactly the rows asked is
  therefore asked twice at every move: once for the move, once more against the Window its answer
  made. That is one request per Window position, as FN-3 counts them. The theory in
  `RangeRequestMarginTests` pins it beside 0, 3 and 20 rows read ahead.
- **ExPivot** passes the smaller of 10 and its source's `ReadAheadRows` as its grid's margin. It keeps
  a question in flight whose Window covers the rows on screen. The test that keeps one,
  `ReportReadAheadTests.ADR0183_LV34_A_question_in_flight_that_covers_the_rows_on_screen_stays_in_flight`,
  was run against the first build's condition (a question kept only while it covers the margin too),
  and failed there.
- **`GridSource.Fetch` is untouched.** Its grid can still be given a margin. The early request then
  finds the rows on screen in the source's Window and fetches nothing, which
  `RangeRequestMarginTests.ADR0183_GridSource_Fetch_fetches_nothing_for_an_early_request` pins. The
  measurement on its own path has no ticket yet.
- **Docs Site:** ExGrid's Data sources page has a section, "Asking before the Viewport runs out". It
  says what the margin does, why it is 0 by default, and that a covering question should be kept. Its
  Example, "Asked ten rows early" (`AskEarly`, `AskEarlyMud`, `ReadAheadTrades.cs`), counts the
  questions asked with every row on screen held and those asked with rows waiting. ExPivot's Sources
  page says a server's report is asked for early. The package READMEs of ExGrid and ExPivot say it in
  a sentence each.
- **Layer 3, written and not run yet:** the same `pivot-db.spec.mjs` test as ticket 14's. Under the
  Server host it runs behind the latency proxy at 150 ms. The demo API is not behind a proxy in the
  suite, so on WebAssembly it counts questions without a round trip.

2026-10-11: Layer 3 ran, locally, on Playwright's bundled Chromium on both hosts. CI's Chrome and Edge
are still to judge it. Ticket 14's comment of today has the runs. With ExPivot's margin set to 0, the
test failed at row 555, where the next Window should have been asked for early.
- **The Docs Site's Example was used once, under each Chrome.** A cell was clicked and ↓ pressed 200
  times, about 30 a second, as a held key repeats. Without the margin, both questions were asked with
  rows on screen waiting. With it, all five were asked with every row on screen held. The console stayed
  clean.

2026-10-11, after the branch's review: the last box's ticket is written,
[16](16-measure-the-early-ask-on-gridsource-fetch.md). The layer-3 box is now met behind a held answer as well.
On WebAssembly, a route holds the early question while the report scrolls. Before, no browser run had a question
in flight while it scrolled: the proxy delays only the circuit, and the demo API answers at once. The early
question no longer dims the report while every row on screen is held. Ticket 14's comment of the same time has
the tests.
