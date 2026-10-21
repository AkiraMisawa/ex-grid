# 03: Text typed into a field rides with the next numbered input, and the Settled Mark covers typing

Status: ready-for-agent
Roadmap: ExGrid, ExSheet — Text typed into the Cell Editor, Formula Bar or Name Box at machine speed is never overtaken by the key, press or click that follows it.

**Why:** [ADR-0240](../../../adr/0240-input-to-one-product-instance-is-taken-in-one-order.md) §7, stage 2. Every numbered input carries the text of the field that holds the keyboard; the core adopts it first and drops the older input events (§5), generalising `InputOvertaken`. The Settled Mark waits until a field's text is the text its surface last rendered; ASY-7's exclusion of typed text goes.

- [x] Every numbered input carries the field's text, and the core adopts it
- [x] The Settled Mark covers typing; ASY-7 rewritten
- [ ] Layer 3: typing followed at once by a press, a heading click and a menu command, on both hosts

**Blocked by:** 02.

## Start record, 2026-10-20

Started from `03053917`; resumed from verified integration `8027b9f5` after the related input gate recorded in
[the responsive/touch checkpoint](../../responsive-touch/README.md#stage-gate-passed-2026-10-20).
The full hunt continues asynchronously under ADR-0240 section 7.

Tests use the existing component's interop/event boundary and Consumer callbacks, and the
browser's user input and Settled Mark. The first case commits text carried by a row press before
Blazor's older typing event has arrived.

## Verification scope selected before stage 03 runs

The related gate includes `input-text-order`, `settled-mark`, `circuit`, `keyboard-return`,
`row-command-order`, `write-lands`, `edit-stands`, `pointing-scope`, `headings`, `features` and
`sheet` (each `.spec.mjs`). These cover text followed by commands, all three owned fields,
editor/pointing transitions, the shared hold, lost input and claimed heading/row paths.
Run ten repetitions and the widened settings on both hosts and browsers. Normal CI remains
required. The baseline for the before/after input measurement is `8027b9f5`; main integration
changed the earlier baseline, so `03053917` cannot isolate this stage's cost.

## Local implementation evidence

- Carried row/heading text reproduces an edit committing `a` instead of `all the text`; the
  two component cases now take the full text. Name Box submission similarly reproduced `A`
  instead of `A123`, and now reads its carried text before the old typing arrives.
- A later Name Box acknowledgement could restore a submission already finished; a separate
  component regression reproduces the duplicate notification and now prevents it.
- Typing alone let the Settled Mark return while the rendered editor still said `Ishikawa`;
  all three local repetitions reproduced it. The mark now waits for the rendered field text.
- A Formula Bar persists after commit but loses its editing text mark. Keeping that closed
  surface owed left the mark off after a successful commit (both Chromes reproduced it).
  Releasing that ended edit preserves the command's numbered answer as the completion boundary.
- Typing, right-click and Enter sent together left the Context Menu open: Enter had reached
  the old grid instead. The context-menu claim now holds following keys for its keyboard
  destination, with or without an open edit. An older render could also consume that menu's
  focus request before its controls had rendered; the request now belongs to its opening's
  render. The regression reads the copied cell text, not merely a closed menu.
- The edited circuit tests exposed a Chrome-only gap: without a Formula Bar, completion or
  references, the substituted Cell Editor reported text but did not render the root's text
  acknowledgement. Its callback now takes the same input handler and render path as the
  built-in editor, including rejection of overtaken input events.

Browser preparation corrections: the initial interop integration omitted the new argument from
one multiline key call; that was corrected before the settled-typing reproduction. The heading
case initially selected a second header band rather than its second cell, so it never reached
its input sequence; the locator now names the column header cell. Clipboard assertions include
the exported row's terminating CRLF; omitting it failed after the copy itself had succeeded.

## Local verification, 2026-10-20

- Solution build: zero warnings and errors. Full layers 1 and 2: 14,497 passed, 11 skipped.
  Existing script-shape assertions were updated to include the new carried-text argument;
  their paint, caret, numbering and refusal checks remain intact.
- All 18 new browser cases and five edited circuit cases ran three times on Chrome Server,
  with a 150 ms round-trip floor and CPU throttled by four. The adjacent disconnected-row
  Action case was selected by the same grep: 72 executions, all passed.
- The Docs Site's new field-text example was built and exercised under Built-in and MudBlazor;
  no browser warning or exception. ExGrid, ExSheet and ExPivot prose and product-specific
  Change Notes cover the visible changes.
- Standards review: no actionable findings. Spec review: no actionable findings.
- [Input measurements](../../../../spikes/render-bench/results/20261020-field-text/README.md):
  seven rounds per configuration before and after, WebAssembly and Server at 0/60 ms;
  all phases correct and no browser complaints. Results are retained, not timing gates.

Normal CI and the preselected related-input gate remain required before stage 04 starts.

## Follow-up from the asynchronous Hunt

The optional full Hunt on `8027b9f5` was cancelled after its 27 active jobs left the required
stage03 runs queued. Its observed paired click/key failure is retained in
[the reading defect record](../../responsive-touch/hunt-2026-10-20-paired-input-read.md).
The old regression still used a deadline tied to a fallback ADR-0240 removed; the original
journal shows all inputs answered but the last render still owed. Its reading now uses the
Settled Mark. The next gate runs the corrected test in the same preselected scope.

The widened WebAssembly run also exposed a context menu that recalculated a secondary press's
cell after the commit moved the grid on the page. The [defect record](../../responsive-touch/hunt-2026-10-20-context-menu-target.md)
keeps the original evidence and deterministic red/green reproduction. The menu now reuses the
existing press descriptor, preserving ADR-0036's original target without another message.

The widened Server run exposed repeated carried text undoing Point after a press; the
[field-text record](../../responsive-touch/hunt-2026-10-21-repeated-field-text.md) names the
component and browser regressions. Normal CI also found the Input Journal assertion still
reading the last argument as the input number after the field payload was appended. It now
reads the number's actual position and requires positive safe integers. Its original checks
of count, order, replies and the answered stamp remain. `input-journal.spec.mjs` is added to
the next related gate; none of the original eleven specs is removed.

Normal CI then exposed a cell-to-Formula-Bar transfer adopting the bar's older rendered value
as new typing. The [surface-transfer record](../../responsive-touch/hunt-2026-10-21-field-surface-transfer.md)
records the red/green reproduction and distinguishes native input from a rendered reading.
`declarations.spec.mjs`, covering Formula Bar transfers and F4, is added to the existing twelve
related specs. Omitting it from the earlier scope was a coverage gap; no selected spec is removed.

## Main integration, 2026-10-21

PR #187 now integrates main `96101164` at `ab1613c0`. This branch takes that integration,
including the Workbook API, Sheet-switch Input Order handling and shaped grids. Its public
API-list conflict keeps main's new members alongside the updated carried-text signatures.
The responsive fixture now uses the Workbook API; its entries, formats and widths are unchanged.

Add `workbook.spec.mjs` to the thirteen related specs above: main's Sheet switching shares the
Input Order and must preserve immediate typing after a switch with the carried-text changes.
The replacement gate therefore has fourteen specs; none of the earlier scope is removed.
Earlier CI results apply to their named revisions and do not verify this integration.

The integrated solution builds without warnings or errors. All sixteen logic/component suites
pass: 14,613 passed, 11 skipped. Thirty targeted browser executions pass on Chrome Server at
150 ms RTT floor and fourfold CPU throttling, three repetitions each: cell-to-bar transfer,
Point preservation, queued row commands and immediate typing after a Sheet switch, under both
Chromes. The Docs Formula example's Point and cell-to-bar paths were exercised under both
Chromes with no browser warnings or exceptions. Standards and Spec integration reviews found
no actionable findings. Replacement CI and the fourteen-spec gate remain required.

## Further main integration and CI failures, 2026-10-21

Main advanced by nine commits to `bdea73f4`. Local integration `78cbbe1a` brings that main
into the responsive preparation branch; `f9a283df` brings that branch into stage 03. The API
list retains main's new members and the carried-text overloads, removing only the obsolete
paste signatures that main reintroduced during the textual merge.

The preceding PR #187 revision `ab1613c0` passed normal CI 39520753250. Stage 03 revision
`430cc823` failed normal CI 39521503735 in the closing-menu test, and related-input run
39521512192 in the Row Heading edge drag. The [menu preparation record](../../responsive-touch/hunt-2026-10-21-menu-opening-preparation.md)
records a six-failure/six-pass reproduction with unchanged behavior assertions. The
[Row Heading record](../../responsive-touch/hunt-2026-10-21-row-heading-renderer-stop.md)
remains open; isolated local passes do not resolve its stopped renderer.

Add `popovers.spec.mjs` to the fourteen selected related specs. The next gate has fifteen
specs; none is removed. Current-integration verification and the gate remain required before
stage 04 starts.

The combined solution builds with zero warnings or errors. All eighteen logic/component
suites pass: 14,865 passed, 11 skipped. The menu preparation correction passed all six local
executions described in its record. Both integration reviews found no actionable findings.
The unchanged heading file passed ten full repetitions locally under widened settings (140
executions) and ten without additional latency or CPU throttling (140 executions); neither
run reproduced the renderer stop, so those passes do not close that defect. Eighteen further
Chrome Server executions passed at 150 ms / CPU4: three repetitions under both Chromes of
cell-to-bar transfer, Point preservation and main's new cross-Sheet Point/commit case (WB-6).


## Main follow-up, 2026-10-21

Main `fcd295ba` (PR #229) is integrated through responsive branch `898400a7` and stage 03
`6cec63ee`. Both merges were clean. Its browser fixture addition prints the already-captured
Input Journal in failure output; it does not change the input or settling boundary. The
combined solution builds with zero warnings/errors, and all eighteen logic/component suites
pass: 14,895 passed, 11 skipped. The corrected closing-menu test passes six more executions
on Chrome Server at 150 ms RTT floor and fourfold CPU throttling, three under each Chrome.

The heading investigation remains pinned to `9a4ed03a` so this unrelated main update cannot
change its reproduction. These integration passes do not resolve the heading stop or replace
the required fifteen-spec gate.


## IME main integration, 2026-10-21

Main `eb025772` (PR #230) is integrated through responsive branch `6248c835` and stage 03
`d926cd48`. The focus listener now passes the scroller's native focus to the Keyboard Field
immediately, as main decided. Only generated assets conflicted; both merges regenerated
them from their integrated Assets sources. Integration review found no actionable findings.

All eighteen logic/component suites pass: 14,895 passed, 11 skipped. Fifteen targeted
Chrome Server executions pass with a 150 ms RTT floor and fourfold CPU throttling: the
new composition-on-a-just-pressed-cell regression and Point preservation under both Chromes,
and text committed by a row press, three repetitions each. This integration adds no new
Consumer behavior beyond main's documented IME correction; no additional Docs Site example
or Change Note is needed for the merge itself. Latest-revision CI and the fifteen-spec
related-input gate remain required; the Heading renderer investigation remains open.
