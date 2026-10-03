# ExSheet observed-function implementation: local acceptance

Date: 2026-10-03. macOS 26.6.2, native headed Chrome 154.0.8037.93, Blazor Server. Branch:
`codex/exsheet-observed-functions`, based on
`fbbe7b1d4a358b8d5e5dcf0dd0a289fbcd74a071` (the Windows observation branch).
The implementation is uncommitted at this verification point.

## Results

- The catalogue and declaration tests agree on 142 functions, 21 added.
- All 957 imported cases preserve the observation inputs, their kinds, and exact numeric
  expectations. Numeric `roundTrip` strings are checked against recorded bits. 127 cases keep
  Excel's answer in `excelExpect` beside an explicit ADR-0047 refusal; those are not claimed
  numerical matches. The source Windows evidence was not modified.
- `nix develop -c dotnet test ExGrid.slnx`: 8,761 passed, 8 pre-existing skips, 0 failed across
  12 suites. No build warnings or errors. A standalone solution build also passed with none.
- Targeted headed Chrome: 24 passed, 0 failed, in 1.3 minutes. The new spec and the existing
  completion acceptance case run under both built-in and MudBlazor Chrome, three repetitions
  each. This covers completion, the repeating range/criterion hint, mode selection with Tab,
  extended SUMIF dependency recalculation and representative lookup/rounding/date/text results.
- `console.json` contains no unexpected messages; the Server fixture also checked its host log.
- PowerShell parsed the modified Oracle script with no syntax errors. Its new typed Value2
  fixture writer has not been rerun through Windows COM here.
- The financial solver experiment was reproduced: Newton, secant and tighter Newton still
  disagree with the recorded IRR/XIRR/RATE results. No financial function was admitted.

The exact result lines are retained in `exsheet-function-tests.log`. The successful browser
command, from `tests/ExGrid.Browser`, was:

```sh
EXGRID_HOSTING=server EXGRID_BASE_URL=http://localhost:5398 \
  nix develop .#browser -c npx playwright test \
  sheet-observed-functions.spec.mjs declarations.spec.mjs \
  --project=chrome --grep 'ADR-0047|ADR-0058|DC-17: the candidate list' --repeat-each=3
```

An earlier new-spec run passed 9/12 and failed all three MudBlazor hint assertions: the test
used the built-in Chrome's `.ex-completion-hint` class. It was changed to the shared completion
container; the next run passed 12/12. Argument-choice acceptance was then added, producing the
final 24/24 above. No timeout, retry or fixed wait was added.

## Remaining verification and work

- Full layer 3 on both hosts and browsers belongs to CI; no push or CI run was performed.
- Windows popup captions and boolean mode typing remain a follow-up in function tickets 09
  and 11. Choice meanings come from Microsoft's documentation; the COM function observation
  run did not record those popups.
- Partial numeric and character domains are documented in the engine README and ADR-0047.
  In particular, fractional MROUND, ambiguous directional-rounding boundaries, DATEDIF MD
  and longer YD intervals, and unadmitted Unicode/collation remain explicit refusals.
- Financial iteration, LET bindings and reproducible random input remain tickets 13–15.
  Existing Supported functions' uncertain Oracle cases were outside the October 3 run.
