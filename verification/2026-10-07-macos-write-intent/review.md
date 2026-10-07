# ADR-0154 review

Two independent read-only reviews compared
`1383cf8bd86325d157ff77e0336d545169440090...26bf6171e7d890d42fc2196127884e5812fd9006`.
The preceding live-report implementation already has a separate review record. This review
covers the accepted write-policy revision and its integrations.

## Standards

One documented-contract violation, P2: Action target resolution used reference identity for the
Row Key delegate even though the source contract permits a new equivalent delegate instance on
each read. A value-only Window replacement could therefore reject a delayed Action. Preserve
the detached declaration identity for equal method/target delegates; invalidate it when the
declaration actually changes. No additional heuristic smell findings.

## Spec

Three findings against ADR-0154's original-row/command requirement:

1. **P1: a delayed Space could invoke a replacement command.** Keyboard engagement read the
   current sole Action name and combined it with the old paint. Replacing Approve with Cancel
   could invoke Cancel. Resolve the original command from the captured declaration.
2. **P1: a row moving while its Action button was held could redirect the Action.** JavaScript
   captured the old paint at mousedown but read row/column/command indices at mouseup. A keyed
   row moving between those events mixed two address versions. Capture the complete address
   at mousedown and carry it unchanged.
3. **P1: a streamed paste could follow a later Selection.** The asynchronous clipboard read
   preserved only the paint token, then planned the paste from the current Selection. Capture
   the original Selection and address context before reading. Reject changed order/columns,
   rather than retargeting.

The reviewers performed static analysis, not reproduction. Component regressions reproduced
each finding before its fix. The follow-up review of `25cee5d4` confirmed the delegate,
pointer-address and streamed-paste fixes. It found one remaining Space case: removing the last
Action clears its paint addresses, after which an old Space could fall back to a same-named
Editable or Mark Column. `76f28f10` corrects this using only each current column's Space
operation and first valid paint. An unknown Action is refused, while an unchanged Editable
column still accepts Space after 100 updates have evicted unrelated Action addresses. A refusal
also ends Interactive, so a later Space cannot revive its old command. The additional component
regressions were RED before the fix; all 231 focused cases then passed.

Both reviewers rechecked `25cee5d4...76f28f10` independently and reported **zero remaining
Standards findings and zero remaining Spec findings**. These are static reviews; the executable
verification is recorded separately in the README and raw logs.

The pointer regression was reproduced in Chrome through the Server latency proxy. It first
proved that the same DOM button moved, that the updated order was painted, and that only one
mousedown occurred. The old implementation delivered no Action (`Action: —`), rather than
the reviewer's predicted wrong-row notification. Both outcomes violate the required delivery
of the original Action exactly once; the raw RED log and context preserve the observed outcome.

The source-binding audit extended the same original-target rule. Two different Sources can
both report sequence zero and equal keys. Pending paste, Delete, fill keys, fill-handle drags
and Actions now carry detached Source identity. An open Cell Editor or Formula Bar is discarded
as `SourceChanged` when that binding is replaced, with Selection preserved. Same-source value
updates and new operations on the replacement remain accepted. The focused component run passed
366 tests, including RED-to-GREEN binding cases and collection of obsolete Source graphs.
Both reviewers found this consistent with ADR-0154's implementation clarification. The follow-up
Standards review reported zero remaining documented-standard violations or additional smells.

## Separate full-suite observation

The first full layer 1/2 run failed the existing
`AskingTests.An_answer_to_a_superseded_question_is_never_painted` at its expected count of two
layout notifications. Its two queued renderer no-ops did not establish that an asynchronous
held-report request had completed. The test now waits for the public layout notification it
asserts, retaining every original value/order assertion. The 18-test class passes with that
explicit completion condition. The final full run at `76f28f10` passed 9,116 tests with eight
existing skips and no failures.
