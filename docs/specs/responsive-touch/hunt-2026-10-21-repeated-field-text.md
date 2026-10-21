# Repeated field text must not undo Point

Status: ready-for-agent
Roadmap: internal

Observed in PR #213's related-input run 39509384816 on `6c4b5ede`: widened Server
Chrome job 118927648932 and Edge job 118927648976 failed three Pointing Scope cases.
The further-press case expected `=1+B2` and retained `=1+Positions[Id]` instead.
The same browser case reproduced locally on Server Chrome with a 150 ms round-trip floor
and fourfold CPU throttling.
Its [local Input Journal](evidence/pointing-before-field-revision-input-journal.json) records
the press, acknowledgement and release as inputs 4, 5 and 6 on the Sheet, all answered in order.

## Cause and regression

The press's numbered input inserted the Reference. Its acknowledgement and release had
already captured the browser's old text before that answer rendered. Each adopted that text
as fresh typing and undid the insertion. The Input Order itself completed in order.

`PointModeTests.ADR0240_a_press_answer_or_release_cannot_undo_the_reference_its_press_wrote`
pins that order without a clock. Both cases failed before the fix: the press wrote `=1+B2`,
then acknowledgement or release restored `=1+`. A companion case covers a key and subsequent
inputs carrying the same reading. The test also checks that a fresh typing of the original
text still replaces the Reference; deduplicating by string equality alone would be wrong.

The field reading carries a revision. Several inputs may carry the same revision; they
cannot reapply its old text after an earlier answer rewrites it. Existing native input
handling advances the revision for fresh typing, including typing away and back. This adds
no per-character interop message, listener, or queue. Keys reuse their existing text argument.

The browser regression in `input-text-order.spec.mjs` types and presses without waiting
between inputs, then reads after the Settled Mark. It repeats with the original text and a
different target, under Built-in and MudBlazor. The three original Pointing Scope cases stay
in the stage's preselected gate.

## Verification

The focused component reproduction failed both cases before the fix; after it, all 37
Point Mode component cases passed. The new browser cases, all three original Pointing Scope
failures, and adjacent CI failures in edit-stands, sheet-vs-excel, sheets and input-journal
passed 63 executions: three repetitions on Server Chrome, 150 ms round-trip floor, CPU
throttled by four. Inputs remain uninterrupted and new assertions read after the Settled Mark.

The solution built with zero warnings/errors. All 16 layer 1/2 suites were run: the first
pass found four script-shape assertions still naming the earlier argument layout or the
secondary-press predicate. Their text, caret, repeat, paint and action-address requirements
were preserved and updated to the new shape. The full component suite then passed all 2,472
cases with one skip; combined with the other unchanged suites, 14,500 passed and 11 skipped.
The Docs Site Formula example was exercised under Built-in and MudBlazor with no browser
complaints. Both review axes found no actionable issues. CI remains required.
