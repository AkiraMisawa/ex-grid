#!/bin/bash
# The browser batch: scrolling on two pages and the live update, each variant twice, interleaved
# (V0, V1, V1, V0) so that a drift in the machine falls on both; then the out-of-memory loop.
S=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad/paint
set -x
i=0
for v in v0 v1 v1 v0; do i=$((i+1)); bash $S/scripts/run-browser.sh scroll-live-$i-$v $v browser-scroll.mjs 5 1 live; done
i=0
for v in v0 v1 v1 v0; do i=$((i+1)); bash $S/scripts/run-browser.sh scroll-wide-$i-$v $v browser-scroll.mjs 5 1 wide; done
i=0
for v in v0 v1 v1 v0; do i=$((i+1)); bash $S/scripts/run-browser.sh live-$i-$v $v browser-live.mjs 1000000 1000 15 3; done
bash $S/scripts/run-oom.sh v1-1000x100 v1 1000 100 12
bash $S/scripts/run-oom.sh v1-1000x400 v1 1000 400 15
bash $S/scripts/run-oom.sh v0-1000x100 v0 1000 100 12
echo done
