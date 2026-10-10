#!/bin/bash
D=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad/ld13
bash $D/run-core.sh pivot-1000x9 pivot 1000 9 1,1000 3 15
bash $D/run-core.sh pivot-1000x100 pivot 1000 100 1,1000 3 15
bash $D/run-core.sh pivot-1000x400 pivot 1000 400 1,1000 3 15
bash $D/run-core.sh grid-1e5 grid 100000 1,100,1000 3 15
bash $D/run-core.sh grid-1e6 grid 1000000 1,100,1000 3 15
bash $D/run-core.sh vouch vouch 100000
echo "batch1 done $(date +%T)"
