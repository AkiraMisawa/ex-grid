#!/bin/bash
# Round 2 of the runs that round 1 found contended: both scroll pages, and ExPivot on CoreCLR.
S=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad/paint
set -x
i=4
for v in v0 v1 v1 v0; do i=$((i+1)); bash $S/scripts/run-browser.sh scroll-live-$i-$v $v browser-scroll.mjs 5 1 live; done
i=4
for v in v0 v1 v1 v0; do i=$((i+1)); bash $S/scripts/run-browser.sh scroll-wide-$i-$v $v browser-scroll.mjs 5 1 wide; done
bash $S/scripts/run-core.sh pivot-v1-r2 V1 pivot 1000 100 3 15
bash $S/scripts/run-core.sh pivot-v0-r2 V0 pivot 1000 100 3 15
echo done
