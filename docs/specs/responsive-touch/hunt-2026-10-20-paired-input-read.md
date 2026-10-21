# The paired click/key regression reads after the Settled Mark

Status: ready-for-agent
Roadmap: internal

The asynchronous [full Hunt 39502796479](https://github.com/AkiraMisawa/ex-grid/actions/runs/39502796479)
on `8027b9f5` exposed one failure before it was cancelled to release runner capacity for the
required CI and related-input gate. Its completed widened Server/Edge shard 2 failed the
paired click/key case in `circuit.spec.mjs`: the first pair expected `F3` but read `F2` at
its 1,500 ms deadline. This is an observed defect, not a result settled by re-running.

## Cause, from the original trace

[The retained Input Journal](evidence/hunt-39502796479-ed22-input-journal.json) shows all ten
core inputs entering, finishing and being answered in order. The final Enter was sent at
21:49:31.095 UTC; core input 10 finished at 21:49:31.175 UTC. The script heard its reply and
ended its hold at 21:49:31.288 UTC, but its last rendered answer in the journal was still
input 8. No Settled Mark had returned for the burst when the assertion failed.

The test's 1.5-second bound was justified by an older two-second fallback. ADR-0240 removed
that fallback and requires completion by numbered answer and rendered marks; ADR-0220
requires reading after the Settled Mark and forbids a timing gate on performance. The test
was still judging elapsed time while a correct final render was owed.

## Correction

Keep the same two-click/key pairs and all twelve expected cell values. After each pair,
wait for the Settled Mark, then read its Name Box once. Read the final cell values once too.
There is no pause within a pair, no longer arbitrary deadline, and no retrying value assertion.
The product code is unchanged by this correction, so it has no Docs Site or Change Note.

The corrected case passed all three local Chrome Server repetitions at 150 ms/CPU four.
The spec-lint suite passed 137 tests, and both follow-up reviews found no actionable issue.
The required CI and related-input results remain pending. The stage03 related gate still includes the entire `circuit.spec.mjs`;
no failed test was removed from its scope.
