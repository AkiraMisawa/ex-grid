#!/bin/bash
# After (this worktree) and before (41c8d8c8, same session) interleaved where they pair.
D=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad/ld13
V0C=$D/v0/spikes/live-update/Costs/bin/Release/net10.0/Costs.dll
V0P=$D/v0/spikes/paint-text/PaintCosts/bin/Release/net10.0/PaintCosts.dll
for size in 9 100 400; do
  bash $D/run-core.sh pivot-gc-1000x$size pivot-gc 1000 $size 3 15 full verbose
  DLL=$V0P bash $D/run-core.sh before-pivot-gc-1000x$size pivot-gc 1000 $size 3 15 full verbose
done
for size in 9 100 400; do
  DLL=$V0C bash $D/run-core.sh before-pivot-1000x$size pivot 1000 $size 1,1000 3 15
done
DLL=$V0C bash $D/run-core.sh before-grid-1e5 grid 100000 1,100,1000 3 15
DLL=$V0C bash $D/run-core.sh before-grid-1e6 grid 1000000 1,100,1000 3 15
bash $D/run-core.sh pivot-steady-1000x100 pivot-steady 1000 100 150
bash $D/run-core.sh pivot-steady-1000x9 pivot-steady 1000 9 60
bash $D/run-core.sh pivot-steady-1000x400 pivot-steady 1000 400 60
bash $D/run-core.sh pivot-memory-1000x100 pivot-memory 1000 100 1 20
bash $D/run-core.sh pivot-memory-1000x400 pivot-memory 1000 400 1 15
echo "batch2 done $(date +%T)"
