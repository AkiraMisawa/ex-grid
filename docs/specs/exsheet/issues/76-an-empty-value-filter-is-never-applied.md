# 76: An empty value filter is never applied

Status: ready-for-agent

**What to build:** a defect in ExGrid's filter, seen while ticket 75 was built (its Comments, "Seen, and
not this ticket's"). A WebAssembly run of `circuit.spec.mjs`'s SRV-5 failed once (it passed 8 of 8
alone), and the run threw `InvalidOperationException: Operator In on column 'Book' requires a
non-empty Values list` (`GridQueryEngine.ValidateOperands`) from a render on `/features`, when the
value filter was applied with nothing ticked. An exception in a render is a failure (CLAUDE.md, "What
counts as verified"), and a filter the engine refuses must never reach it.

The numbers from 74 up are the Pointing Scope's line; the Cell Format's line holds 44 to 69. This
ticket is ExGrid's, numbered here.

**Blocked by:** None.

- [ ] Find every way the value filter can hand the engine an `In` with no values: Apply, Enter (the
      panel's hidden default button, ADR-0039's note), a held key replayed on a circuit, and the
      MudBlazor Chrome's panel as well as the built-in one. Reproduce the SRV-5 failure's path first
      (WebAssembly, `/features`), with a test that throws before the fix
- [ ] With nothing ticked the filter is not applied, under both Chromes and on both hosts, and Apply
      shows it is unavailable, as WR-2 already asks. If the existing ADRs (ADR-0009, ADR-0039) do not
      already say what applying nothing means, stop and report the proposal: that is a decision
- [ ] The engine keeps refusing an empty `In`; the fix is in what reaches it, not in the refusal
      (ADR-0001: say it cannot be done rather than be quietly wrong)
- [ ] Layer 2 for the panel's Apply and Enter with nothing ticked; Layer 3 on `/features` under both
      Chromes, on both hosts: untick every value, Apply, and Enter; no exception reaches the console
      or the host log (CON-1, CON-6), and the rows are as they were

## Comments
