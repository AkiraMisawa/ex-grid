# PR #66: serialize test DOM access with rendering

The [CI run for `9bafd309`](https://github.com/AkiraMisawa/ex-grid/actions/runs/37553676057)
passed every layer-3 job and package checks, but failed one component test:
`SlicedBuildTests.A_long_layout_of_the_answer_held_is_sliced_and_superseded`.
Opening the next field menu dispatched retired event ID 220.
The [CI log](raw/ci-layers12-initial.log) and [job summary](raw/ci-initial-summary.json)
preserve that result. Trailing whitespace in the downloaded log was removed.

## Diagnosis and correction

The test releases asynchronous report work, reads the displayed rows, then opens a
field menu. Moving only lookup and event dispatch onto the renderer was insufficient:
the full solution passed locally, but the third repetition of the seven sliced-build
tests reproduced the same retired event in the same test. See the
[intermediate suite](raw/layers12-dispatch-only.log) and
[failing repetition](raw/sliced-build-dispatch-only-failed.log).

There was also an unsynchronized DOM read before the click. `RowTexts` parsed bUnit's
cached DOM from the test thread while an asynchronous report could update its markup.
bUnit 2.9.0's [node cache and markup update](https://github.com/bUnit-dev/bUnit/blob/v2.9.0/src/bunit/Rendering/RenderedComponent.cs)
are separate from the component's public loading state. This supports the diagnosis of
a test DOM/render race; it does not establish a product event-dispatch defect.

The shared helper now reads row text on the renderer and keeps element lookup and
gesture dispatch on that same context for field toggles and menu operations. It uses
the existing component/DOM seams, changes no expected result and introduces no delay,
retry or ignored event. Product code, browser specs and dependencies are unchanged.
Temporary diagnostic logging was removed before final verification.

## Final local verification, before push

Same machine and toolchain as the [preceding record](../2026-10-07-macos-pr66-follow-up/README.md).

| Check | Result | Evidence |
| --- | --- | --- |
| All layers 1 and 2, with CI's coverage configuration | 9,116 passed, 8 existing skips, 0 failed | [Log](raw/layers12-final-coverage.log) |
| Seven sliced-build cases, 20 consecutive runs after the final correction | 140 passed, 0 failed | [Log](raw/sliced-build-final-repeat20.log) |
| Targeted layer 3: collapse, filter, layout choice and deferred layout, Built-in and MudBlazor | 8 passed, 0 skipped, 0 failed | [Log](raw/layer3-pivot-server.log), [console](raw/console-pivot-server.json) |

The browser run used headed Chrome on the Server host, started by the suite through
`dotnet run`, on unused ports 5798/6798/7798/8798. Its console record is empty and the
Server runtime-error checks passed. No rebuild ran under an active host. The preceding
164 targeted browser checks and the full CI browser matrix also passed on this
unchanged product code. The final push must satisfy its own CI checks.

From the worktree root:

```sh
nix develop -c dotnet test ExGrid.slnx \
  "-p:TestingPlatformCommandLineArguments=--coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml --coverage-settings $PWD/tests/coverage.settings.xml"
nix develop -c bash -c 'set -e; for iteration in {1..20}; do
  dotnet tests/ExPivot.Components/bin/Debug/net10.0/ExPivot.Components.Tests.dll \
    -class ExPivot.Components.Tests.SlicedBuildTests -noLogo -noColor
done'
cd tests/ExGrid.Browser
EXGRID_HOSTING=server EXGRID_BASE_URL=http://localhost:5798 \
  nix develop .#browser -c npx playwright test '/pivot[.]spec[.]mjs$' --project=chrome \
  --grep 'collapses an Item|report filter band filters|a choice lays the report out again|while Defer Layout Update'
```

The repetition loop stops on any failure. The direct xUnit class selector is deliberate:
the solution uses Microsoft.Testing.Platform, so a VSTest-style `--filter` is not used
to claim a narrowed run. The coverage settings path is absolute, as in CI.
