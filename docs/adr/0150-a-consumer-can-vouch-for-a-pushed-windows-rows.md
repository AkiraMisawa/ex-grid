# A Consumer can vouch for a pushed Window's rows

*(Decided with the user, 2026-10-06, Q1 of the live-data continuation on
`claude/live-data-next`. This branch reserved ADR-0150 to ADR-0159 on `main` before this
decision. It extends [ADR-0141](./0141-exgrids-bundled-sources-take-live-data-by-row-key-on-expivots-rules.md);
implementation is still pending.)*

The grid checks every new Window it has not been told it can trust. A keyed source already
can vouch for its Window through the public `IGridSource.VouchesDistinctRows` contract, including
a Consumer's own source. A Consumer that pushes its Window directly cannot make the same promise.
ExPivot is such a Consumer, although its engine constructs distinct report rows by their Items,
role and Value Field.

**A Consumer may explicitly vouch for a pushed Window with a Row Key.** The default remains
false, and an unvouched Window is checked as before. The promise covers every row in that Window:
no null row, no null key, and no repeated key under the equality used for Row Keys. It is made
about the Row Key in force, not a different key the Consumer checked elsewhere.

## Where the responsibility goes

- The grid skips its whole-Window validation when the promise applies. The Consumer keeps it
  true by construction or by validating the changes it accepts. This is a correctness contract,
  not a request to trade correctness for speed.
- A source may vouch only for its own Row Key. A Consumer overriding that key does not inherit
  the source's guarantee. Without a Row Key, the existing instance check still applies.
- Withdrawing the promise restores validation, even when the Window instance has not changed.
  An unvouched new Window or a change of the key in force is checked as before.
- **A false vouch is a Consumer defect that the grid does not promise to detect.** Blazor's
  duplicate-key exception is not a fallback validator: two repeated keys that never coexist in
  the painted slice need not trigger it. Null rows and null keys are likewise the Consumer's
  responsibility under the promise. The parameter's XML documentation states this explicitly.
- ExPivot vouches for its report, backed by the engine's construction of its keys. This does
  not decide how a report is retained between live redraws; ticket 03 is a separate decision.

## Why

[Ticket 01's isolated measurements](../../verification/2026-10-06-macos-live-update-costs/README.md)
put the report's key check at 0.191, 1.909 and 9.045 ms for 11,001, 101,001 and 401,001 rows
on CoreCLR. The check repeats a guarantee the engine already makes. Allowing the pushed form
to express that guarantee also makes it consistent with the public source form.

The alternative was to keep validating every pushed Window. It keeps responsibility in the grid
but repeats the whole pass on every update. The user chose the explicit promise, with validation
remaining the default. Removing this pass does not address the larger Cube and Report construction
costs measured by ticket 01, or establish a cause or fix for its WebAssembly out-of-memory failure.

## Verification

Section 32's LV-10 covers the promise in source and pushed forms, including an overridden source
key, no Row Key, and withdrawal on the same Window. LV-19 covers ExPivot's use of it. Layer 2
counts the key function's calls to distinguish a whole-Window pass from the keys read for painted
rows. Unvouched invalid Windows retain their named refusals. Performance remains observational;
PV-43 is repeated after implementation. Consumer documentation and running examples must explain
who keeps the promise before this API is complete.
