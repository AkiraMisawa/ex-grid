#!/bin/bash
# Follow-up: why V1's ExPivot live redraw has a slower median on CoreCLR. H1 (fragmentation and
# promotion into free lists), H2 (commit/decommit), H3 (a short-window artifact); then the browser.
S=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad/paint
set -x
bash $S/scripts/run-core.sh gc-full-1-v0 V0 pivot-gc 1000 100 3 15 full verbose
bash $S/scripts/run-core.sh gc-full-2-v1 V1 pivot-gc 1000 100 3 15 full verbose
bash $S/scripts/run-core.sh gc-full-3-v1 V1 pivot-gc 1000 100 3 15 full verbose
bash $S/scripts/run-core.sh gc-full-4-v0 V0 pivot-gc 1000 100 3 15 full verbose
bash $S/scripts/run-core.sh gc-compact-1-v0 V0 pivot-gc 1000 100 3 15 compact verbose
bash $S/scripts/run-core.sh gc-compact-2-v1 V1 pivot-gc 1000 100 3 15 compact verbose
DOTNET_GCRetainVM=1 bash $S/scripts/run-core.sh gc-retainvm-1-v0 V0 pivot-gc 1000 100 3 15 full verbose
DOTNET_GCRetainVM=1 bash $S/scripts/run-core.sh gc-retainvm-2-v1 V1 pivot-gc 1000 100 3 15 full verbose
bash $S/scripts/run-core.sh steady-1-v0 V0 pivot-steady 1000 100 60
bash $S/scripts/run-core.sh steady-2-v1 V1 pivot-steady 1000 100 60
i=0
for v in v0 v1 v1 v0; do i=$((i+1)); bash $S/scripts/run-browser.sh pivot-redraws-$i-$v $v browser-pivot.mjs 1000 100 15; done
bash $S/scripts/run-oom.sh v0-1000x100-15 v0 1000 100 15
bash $S/scripts/run-oom.sh v1-1000x100-15 v1 1000 100 15
echo done
