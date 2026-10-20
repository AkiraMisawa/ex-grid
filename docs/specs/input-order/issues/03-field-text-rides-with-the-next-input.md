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
