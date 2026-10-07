#!/bin/bash
D=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad/ld13
for s in 9 100 400; do bash $D/run-core.sh pivot-1000x$s-r2 pivot 1000 $s 1,1000 3 15; done
bash $D/run-core.sh pivot-steady-1000x400-70 pivot-steady 1000 400 70
bash $D/run-core.sh pivot-steady-1000x9-70 pivot-steady 1000 9 70
echo "batch3 done $(date +%T)"
