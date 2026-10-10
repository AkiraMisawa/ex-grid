#!/bin/bash
S=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad/paint
set -x
i=0
for v in v0b v1b v1b v0b; do i=$((i+1)); bash $S/scripts/run-browser.sh paintcost-$i-$v $v browser-paintcost.mjs 15 3; done
bash $S/scripts/run-core.sh pivot-memory-v0 V0 pivot-memory 1000 100 1 20
bash $S/scripts/run-core.sh pivot-memory-v1 V1 pivot-memory 1000 100 1 20
echo done
