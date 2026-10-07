# PR #66: related browser checks before the follow-up push

This follows the request to run the related layer-3 tests locally before pushing.
The product revision is `b8823f7b48489db5356ba624e8dba8ff785c9b7a` (the first PR head).
The only test changes for this follow-up make two existing component assertions await
the public result they read. No product code or browser test was changed.

Environment: Apple M4 Pro, 24 GiB, macOS 26.6.2 (25G83), .NET SDK 10.0.203,
Node 24.14.1, installed Chrome 154.0.8037.98. Browser runs use **headed Chrome**,
one worker and the Release-published **Server** host. The latency proxy starts at
zero delay; tests that exercise a delayed circuit select their own round trip.
Ports 5798, 6798, 7798 and 8798 were free before each run. The host and API were
published into a fresh directory outside the repository and were not rebuilt during a run.

## Component failures found in CI

The [first PR run](https://github.com/AkiraMisawa/ex-grid/actions/runs/37552302291)
passed every layer-3 job, including the changed specs repeated three times against
both hosts and both browsers. Its layers-1/2 job failed two existing ExPivot tests:

- `The_search_asks_the_source_beyond_the_list` read an empty item list immediately
  after opening Filter. The click starts asynchronous item loading; it does not await
  that loading. The test now waits for the same expected 10,000 displayed Items before
  searching and asserting the source query's version and results.
- `A_gesture_during_the_build_supersedes_the_question` read `LayoutChanged` before
  it arrived. Releasing sliced work until `IsLoading` clears is not a wait for the
  later callback. The test now awaits the expected single callback, retaining its
  cancellation, complete-report and exact-layout assertions.

Neither expected outcome, timeout nor product behavior was changed. There is no fixed
delay or retry. The original failure log and completed CI job summary are in
[`raw/ci-layers12-initial.log`](raw/ci-layers12-initial.log) and
[`raw/ci-initial-summary.json`](raw/ci-initial-summary.json).

## Local results

| Check | Result | Evidence |
| --- | --- | --- |
| ExPivot component suite after the two wait corrections | 224 passed | [Log](raw/pivot-components.log) |
| All layers 1 and 2 after the corrections | 9,116 passed, 8 existing skips, 0 failed | [Log](raw/layers12.log) |
| `pivot-db`, `pivot-live`, `write-intent`, each repeated three times | 93 passed, 0 skipped, 0 failed | [Log](raw/layer3-changed-server.log), [console](raw/console-changed-server.json) |
| Related existing browser cases across seven spec files | 71 passed, 0 skipped, 0 failed | [Log](raw/layer3-related-server.log), [console](raw/console-related-server.json) |

The two browser runs completed in 2.2 minutes and 1.2 minutes respectively:
**164 passed in total**, with no browser console or Server runtime errors.

The additional selection is recorded by name in [the selection log](raw/related-selection.log).
It covers grid live updates and Window identity, CSV report construction, report
collapse/filter/layout/Details, edit commit and cancellation, validation and paste/fill,
Action dispatch, Sheet editing, and queued input across a circuit round trip.

These are targeted local checks. The full local browser matrix was not run;
the initial PR CI completed the full Linux matrix successfully on the same product code.
The new push must also satisfy its own CI checks.

## Commands

From the worktree root, publish the host and API to a fresh temporary directory:

```sh
nix develop -c dotnet test ExGrid.slnx
hosts=$(mktemp -d /tmp/exgrid-pr66-browser.XXXXXX)
nix develop -c dotnet publish samples/ExGrid.DemoHost.Server -c Release -o "$hosts/server"
nix develop -c dotnet publish samples/ExGrid.DemoApi -c Release -o "$hosts/api"
export EXGRID_HOSTING=server EXGRID_HOSTS="$hosts" EXGRID_BASE_URL=http://localhost:5798
cd tests/ExGrid.Browser
nix develop .#browser -c npx playwright test \
  '/(pivot-db|pivot-live|write-intent)[.]spec[.]mjs$' --project=chrome --repeat-each=3
nix develop .#browser -c npx playwright test \
  '/(pivot|pivot-csv|grid-live|grid-live-local|features|sheet|circuit)[.]spec[.]mjs$' \
  --project=chrome \
  --grep 'grid-live|pivot-csv|collapses an Item|a double click on a value|Show Details opens|listens to Show Details|report filter band filters|a choice lays the report out again|while Defer Layout Update|ED-2|ED-3|CP-14|CP-16|ED-15|pasted value wears|KB-20|KB-22|KB-23|KB-26|KB-27|an Entry and a Formula|Name Box pressed with an edit|Formula Bar and the Cell Editor|SH-29|keys typed faster|message limit arrives whole|press held behind a page move'
```

`EXGRID_HEADLESS` is unset. Full publish output is retained in [the publish log](raw/publish.log).
Each run uses the suite's console and Server runtime-error checks. This follow-up makes
no new performance or memory claim; those observations remain in the
[write-policy record](../2026-10-07-macos-write-intent/README.md), with their stated revisions.
