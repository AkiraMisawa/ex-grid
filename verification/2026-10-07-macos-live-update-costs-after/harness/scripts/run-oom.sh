#!/bin/bash
# The out-of-memory loop of 2026-10-06-macos-pivot-oom (harness/loop.mjs, its Playwright path moved to
# this worktree), against this worktree's published WebAssembly host served on 5799, under the
# layer-3 lock, after the machine is idle.   run-oom.sh <name> <a> <b> <redraws>
set -u
S=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad
D=$S/ld13
W=/Users/akira338/github/ex-grid/.claude/worktrees/ld-measure-after
R=$W/verification/2026-10-07-macos-live-update-costs-after/raw
L=$S/layer3.lock
name=$1; a=$2; b=$3; n=$4
echo "=== oom $name: waiting for the layer-3 lock, $(date '+%F %T')" >> $R/contention.log
until mkdir "$L" 2>/dev/null; do sleep 20; done
host=""
cleanup() { [ -n "$host" ] && kill $host 2>/dev/null; rmdir "$L" 2>/dev/null; }
trap cleanup EXIT INT TERM HUP
export HOLDING_LAYER3_LOCK=1
{
  echo "=== oom $name: lock taken, $(date '+%F %T')"
  waited=$(bash $D/wait-idle.sh 60); rc=$?; echo "$waited" | tail -2
  if [ $rc -ne 0 ]; then echo "not idle within 60 minutes: run abandoned"; fi
} >> $R/contention.log 2>&1
tail -3 $R/contention.log | grep -q "run abandoned" && exit 9
[ -n "$(lsof -ti tcp:5799)" ] && { echo "port 5799 in use: run abandoned" >> $R/contention.log; exit 8; }
cd $W/tests/ExGrid.Browser
nix develop ../..#browser -c node static-host.mjs $D/hosts/wasm/wwwroot 5799 > $D/static-host-$name.log 2>&1 &
host=$!
until curl -s -o /dev/null http://localhost:5799/; do sleep 1; done
start=$(date +%s)
nix develop ../..#browser -c node $D/scripts/loop.mjs http://localhost:5799 $a $b $n "" 1 2>&1 \
  | grep -v -e zoxide -e "_ZO_DOCTOR" -e "Please ensure that zoxide" -e "If the issue persists" -e "github.com/ajeetdsouza" -e "uncommitted" -e "nix-managed" > $R/$name.log
status=${PIPESTATUS[0]}
# The static host was started through nix develop: stop the node it runs, found by its port.
for pid in $(lsof -ti tcp:5799 -sTCP:LISTEN); do kill $pid 2>/dev/null; done
kill $host 2>/dev/null; host=""
echo "=== oom $name: after, $(date '+%F %T'), $(( $(date +%s) - start )) s, exit $status; $(sysctl -n vm.loadavg)" >> $R/contention.log
exit $status
