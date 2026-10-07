# PR #66: merge ADR-0170 with asynchronous Copy and user writes

This records the merge of `origin/main` at `3907e053` (PR #68, ADR-0170)
into `claude/live-data-next` at `a820f319`. Main added the Copied Range outline
and the Modified corner marker while this branch added asynchronous Consumer
Copy and replaced the displayed-value write-conflict policy with ADR-0154.

## Resolution

The conflicts were in `ExGrid.razor`, the generated grid JavaScript and its
source map, and the browser-test README. The merge preserves both changes:
the copied-range landing contract, edit-opening cleanup and Escape behavior
from main, and this branch's asynchronous Copy and original-target checks.
The minified assets were regenerated from the merged `Assets/` sources.

An asynchronous Consumer copy captures its coordinates, Row Sequence Version
and available painted-text fingerprints before awaiting the answer. When the
clipboard write lands, it is checked against the current Window. A late
answer therefore cannot outline changed coordinates or text as if they were
what was copied. ADR-0170 records this integration clarification and explains
that its text check governs the outline, not acceptance of a user write.

Four component cases cover asynchronous landing and changes to the row order,
columns or copied text while the answer is held. Before integration, the
positive asynchronous-landing case failed because its payload had no landing
number: [red run](raw/copy-outline-red.log). After integration, all 16
Copied Range cases passed: [green run](raw/copy-outline-green.log).

## Local verification before push

Same macOS machine and Nix toolchain as the
[preceding record](../2026-10-07-macos-pr66-dispatch/README.md).

| Check | Result | Evidence |
| --- | --- | --- |
| Component project build | 0 warnings, 0 errors | [Log](raw/component-build.log) |
| Copied Range component cases | 16 passed, 0 failed | [Log](raw/copy-outline-green.log) |
| Full layers 1 and 2 | 9,132 passed, 8 existing skips, 0 failed | [Log](raw/layers12.log) |
| Targeted layer 3, headed Chrome on Server, three repetitions | 99 passed, 0 skipped, 0 failed | [Log](raw/layer3-server.log), [console](raw/console-server.json) |

The browser run covers all six Copied Range cases, all 17 write-intent cases
(including the 150 ms paths), all nine database-pivot cases (including remote
Copy and Summary), and the Modified marker's painted geometry. Each ran three
times. The suite's console record is empty and its Server runtime-error checks
passed. The Server host and DemoApi were published in Release mode to a fresh
temporary directory: [publish log](raw/publish.log). The suite started and
stopped those hosts on unused ports 5798/6798/7798/8798; no rebuild ran under
the active hosts. Full layer 3 remains CI's responsibility.

From the worktree root:

```sh
nix develop .#browser -c bash -c 'cd tools/assets && npm ci && npm run minify'
nix develop -c dotnet build tests/ExGrid.Components
nix develop -c dotnet tests/ExGrid.Components/bin/Debug/net10.0/ExGrid.Components.dll \
  -class ExGrid.Components.Tests.CopiedRangeTests -noLogo -noColor
nix develop -c dotnet test ExGrid.slnx
nix develop -c dotnet publish samples/ExGrid.DemoHost.Server -c Release \
  -o /tmp/exgrid-pr66-merge-hosts.btkEri/server
nix develop -c dotnet publish samples/ExGrid.DemoApi -c Release \
  -o /tmp/exgrid-pr66-merge-hosts.btkEri/api
cd tests/ExGrid.Browser
EXGRID_HOSTING=server EXGRID_HOSTS=/tmp/exgrid-pr66-merge-hosts.btkEri \
  EXGRID_BASE_URL=http://localhost:5798 \
  nix develop .#browser -c npx playwright test \
  '/(copied-range|write-intent|pivot-db|presentation)[.]spec[.]mjs$' \
  --project=chrome --repeat-each=3 \
  --grep 'copied-range|write-intent|pivot-db|Modified mark'
```

The direct xUnit class selector is intentional: this solution uses
Microsoft.Testing.Platform. The temporary hosts were removed after the run.
No performance or memory measurement is claimed by this integration check.
