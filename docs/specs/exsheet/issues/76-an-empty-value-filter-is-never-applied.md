# 76: An empty value filter is never applied

Status: done

**What to build:** a defect in ExGrid's filter, seen while ticket 75 was built (its Comments, "Seen, and
not this ticket's"). A WebAssembly run of `circuit.spec.mjs`'s SRV-5 failed once (it passed 8 of 8
alone), and the run threw `InvalidOperationException: Operator In on column 'Book' requires a
non-empty Values list` (`GridQueryEngine.ValidateOperands`) from a render on `/features`, when the
value filter was applied with nothing ticked. An exception in a render is a failure (CLAUDE.md, "What
counts as verified"), and a filter the engine refuses must never reach it.

The numbers from 74 up are the Pointing Scope's line; the Cell Format's line holds 44 to 69. This
ticket is ExGrid's, numbered here.

**Blocked by:** None.

- [x] Find every way the value filter can hand the engine an `In` with no values: Apply, Enter (the
      panel's hidden default button, ADR-0039's note), a held key replayed on a circuit, and the
      MudBlazor Chrome's panel as well as the built-in one. Reproduce the SRV-5 failure's path first
      (WebAssembly, `/features`), with a test that throws before the fix
- [x] With nothing ticked the filter is not applied, under both Chromes and on both hosts, and Apply
      shows it is unavailable, as WR-2 already asks. If the existing ADRs (ADR-0009, ADR-0039) do not
      already say what applying nothing means, stop and report the proposal: that is a decision
- [x] The engine keeps refusing an empty `In`; the fix is in what reaches it, not in the refusal
      (ADR-0001: say it cannot be done rather than be quietly wrong)
- [x] Layer 2 for the panel's Apply and Enter with nothing ticked; Layer 3 on `/features` under both
      Chromes, on both hosts: untick every value, Apply, and Enter; no exception reaches the console
      or the host log (CON-1, CON-6), and the rows are as they were

## Comments

2026-10-01, implemented on `agent/ps-76`.

- **The paths.** Every way ends in one of two calls. The built-in panel's `ApplyPanelAsync` takes OK,
  Enter in the search box (the form's implicit submission), and a held Enter that the key listener hands
  on with `requestSubmit`. The Wrapper's `Apply` takes Apply, Enter through its hidden default button,
  and the same held Enter, and passes the answer to the context's `Apply`. Both asked
  `FilterPanelChoices.FromValueList`, whose answer is an `In` with no values in three states:
  - nothing ticked;
  - a search with no ticked value among its matches, including a search that matches nothing (`Alpha `);
  - a list in force that the domain no longer shows (FL-9's intersection). The panel opens with nothing
    ticked, so OK pressed straight away was enough.

  The built-in OK was never disabled, and the Wrapper's `CanApply` said a value list can always be
  applied. The engine threw from `InMemoryGridSource.OnFilterChanged` inside the grid's event handler:
  "Unhandled exception rendering component" on WebAssembly, and on the Server host the circuit was
  terminated. Under the Wrapper the answer went through the context's `Apply`, which the core runs as a
  discarded task. The core closed the popover, the source threw, and nothing rendered or logged: the
  panel stayed on screen over a grid that held it closed.
- **SRV-5's failure.** The held-Tab test types `Alpha`, Tab, Space and Enter behind E. Read from
  `ex-grid.js`, not reproduced: the drain hands on `Alpha`, drops the Tab and ends the hold. A Space and
  an Enter typed after that are not held, so they act natively in the search box, which then reads
  `Alpha ` and submits. The race was not reproduced, but its end was: `Alpha ` and Enter in the search
  box on `/features` threw the ticket's exception on WebAssembly under the built-in Chrome before the fix.
- **The rule, read from the ADRs as they stand, not a new decision:** nothing ticked is not applied, and
  Apply shows it is unavailable.
  - ADR-0009's panel follows Excel, and Excel's OK is unavailable with nothing ticked.
  - ADR-0023's engine refuses an `In` without values (`FilterCombinationTests.A_missing_operand_throws`),
    and this ticket keeps that refusal.
  - ADR-0039 has Apply shown unavailable (WR-2) and refusing a submit that comes anyway.
  - No filter in its place would show every row, which `FromValueList` already refused to do ("never
    silently no filter").

  Only `FromValueList`'s doc comment read otherwise: it called the empty `In` "what was asked for". It
  now says that this answer cannot be applied.
- **The fix.** `FilterPanelChoices.CanApply(spec)` is false for an answer that holds an `In` with no
  values, and true for every other.
  - The built-in OK is `disabled` while the value list's answer cannot be applied. The condition form's
    OK is unchanged.
  - The Wrapper's `CanApply` asks it over a value list.
  - The core's one write path, `ApplyFilterSpecAsync`, refuses such an answer before it closes anything.
    OK, Enter, a held Enter and a substituted panel's `Apply` therefore all leave the panel standing, and
    the engine is never asked.

  Enter is still not gated on the button (ADR-0039). It submits, and the submission is refused on the
  circuit, where the ticks have already arrived. `FilterPanelContext`'s doc comment says that `Apply`
  refuses this answer. The engine's refusal is unchanged.
- **With "Add current selection to filter" on**, a search with nothing ticked among its matches answers
  the filter in force (FL-13's union), which is not empty. Apply stays available and applies that filter
  again. This follows from FL-13's rule; it has not been checked against Excel.
- **Tests.**
  - Layer 1, `FilterPanelChoicesTests`: `Nothing_chosen_is_a_filter_that_keeps_nothing` is now
    `Nothing_chosen_keeps_nothing_and_cannot_be_applied`. New:
    `A_search_with_nothing_chosen_among_its_matches_cannot_be_applied`,
    `Adding_nothing_to_the_filter_in_force_can_be_applied` and `Every_other_answer_can_be_applied`.
  - Layer 2, `FilterChromeTests` +4:
    - `Nothing_ticked_is_not_applied` and `A_search_with_nothing_chosen_among_its_matches_is_not_applied`,
      over `GridSource.From`: Enter, then OK. The panel stands, the source has no filter, three rows are
      shown, and OK is disabled. Both threw the ticket's `InvalidOperationException` before the fix.
    - `A_list_in_force_outside_the_domain_opens_with_nothing_to_apply`.
    - `The_core_refuses_an_in_with_no_values_from_any_chrome`, through a stub Chrome's `Apply`.
  - Layer 2, `MudFilterPanelTests` +3: `Nothing_ticked_cannot_be_applied` and
    `A_search_with_nothing_chosen_among_its_matches_cannot_be_applied` (Apply disabled, and the submit
    applies nothing), and `Nothing_ticked_is_applied_by_neither_chrome` (both Chromes in a grid over
    `GridSource.From`). The 7 new layer-2 tests failed before the fix, those over `GridSource.From` by
    the engine's exception. The layer-1 tests did not compile until `CanApply` existed.
  - Layers 1 and 2 after the fix: 829 + 2135 + 91 + 1206 (1 skipped, as before) + 390, all passing.

  Layer 3 has 4 new tests:
  - `popovers.spec.mjs`, per Chrome, "with nothing ticked the value filter is not applied". "(Select
    All)" is pressed down to none, then Enter is pressed in the search box. Apply is disabled, and a
    forced press on it does nothing. Each time the panel still stands 600 ms later, with the rows
    unchanged. Escape closes it, and the rows are as they were.
  - `circuit.spec.mjs`, per Chrome, "a search matching nothing, typed straight after E with its Enter,
    applies nothing": `Alpha ` and Enter typed with E, at a 150 ms round trip on the Server host.

  Runs: headless, macOS, `--project=chrome`, private ports. Edge, and the full run, are CI's.
  - Before the fix, the 4 new tests on WebAssembly: the built-in Chrome's two failed with "Unhandled
    exception rendering component: Operator In on column 'Book' requires a non-empty Values list" (CON-1,
    CON-6), and the popover was gone. The Wrapper's popovers test failed with Apply enabled. The Wrapper's
    circuit test passed, because the exception was discarded, as above.
  - Before the fix, on the Server host: the same three failed. The built-in Chrome's two also had the
    circuit terminated ("Unhandled exception in circuit"). The Wrapper's circuit test passed.
  - After the fix, `popovers.spec.mjs` and `circuit.spec.mjs` whole: WebAssembly 93 passed and 2 skipped
    (the Server-only tests); Server 95 passed.
- **Seen, and not this ticket's:**
  - The held-Tab race above remains. In it, the box now reads `Alpha ` and the panel stands, so the test
    can still fail on its `toHaveValue('Alpha')`, without an exception.
  - Read from `ex-grid.js`, not measured: an Enter in the filter's text field is taken as closing the
    popover, so the keys typed after a refused Enter are held until the two-second fallback. This was
    already so for WR-2's refused Enter in the Wrapper's condition form.
  - Any other exception from a substituted panel's `Apply` still goes into the discarded task, unlogged.
  - Under the Wrapper, a Space pressed on "(Select All)" straight after a test's `focus()` from the
    commands did nothing once. With 500 ms between them it ticks. Not investigated; the layer-3 test
    presses "(Select All)" with the pointer.
