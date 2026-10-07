#!/bin/bash
S=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad/paint
set -x
bash $S/scripts/run-core.sh grid-v0 V0 grid 3 15
bash $S/scripts/run-core.sh grid-v1 V1 grid 3 15
bash $S/scripts/run-core.sh pivot-v0 V0 pivot 1000 100 3 15
bash $S/scripts/run-core.sh pivot-v1 V1 pivot 1000 100 3 15
bash $S/scripts/run-core.sh grid-v1-r2 V1 grid 3 15
bash $S/scripts/run-core.sh grid-v0-r2 V0 grid 3 15
echo done
