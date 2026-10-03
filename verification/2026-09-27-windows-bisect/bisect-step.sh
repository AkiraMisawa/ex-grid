#!/usr/bin/env bash
# One bisect step for Part E: the worktree's current commit is built, its WebAssembly DemoHost
# started on 5399, its tests/ExGrid.Browser copied to Windows, and scrollbar.spec.mjs and
# virtualisation.spec.mjs run on Chrome at the machine's scale. Prints the six criteria's
# outcomes and a verdict line: "verdict <commit> scrollbar=<good|bad> big=<good|bad>".
set -u
WT=/home/akira/src/hobby/ex-grid/.claude/worktrees/bisect
WIN=/mnt/c/Users/amisa/AppData/Local/exgrid-layer3/bisect
REC=/home/akira/src/hobby/ex-grid/verification/2026-09-27-windows-bisect
SP=/tmp/claude-1000/-home-akira-src-hobby-ex-grid/3f3091f7-2a61-49e5-8414-79b7a126faa0/scratchpad
mkdir -p "$REC"
c=$(git -C "$WT" rev-parse --short=7 HEAD)
echo "step $c $(git -C "$WT" log -1 --format='%ad %s' --date=format:'%Y-%m-%d %H:%M')"
fuser -k 5399/tcp >/dev/null 2>&1; sleep 1
if ! (cd "$WT" && nix develop -c dotnet build samples/ExGrid.DemoHost > "$SP/bisect-build-$c.log" 2>&1); then
    echo "build failed at $c (see $SP/bisect-build-$c.log)"; tail -5 "$SP/bisect-build-$c.log"; echo "verdict $c skip"; exit 0
fi
(cd "$WT" && setsid nix develop -c dotnet run --no-build --project samples/ExGrid.DemoHost --urls http://localhost:5399 > "$SP/bisect-host-$c.log" 2>&1 &)
for i in $(seq 1 90); do [ "$(curl -s -o /dev/null -w '%{http_code}' http://localhost:5399/wide)" = 200 ] && break; sleep 2; done
rsync -a --delete --exclude node_modules --exclude test-results "$WT/tests/ExGrid.Browser/" "$WIN/tests/ExGrid.Browser/"
L="$REC/step-$c.log"
{ echo "commit $(git -C "$WT" rev-parse HEAD)"; echo "start $(date '+%F %T %Z')"
  (cd "$WIN/.." && cmd.exe /c "$(wslpath -w "$WIN/../bisect-run.cmd")" 2>&1 | tr -d '\r')
  echo "end $(date '+%F %T %Z')"; } > "$L"
grep -E "^\s+(ok|x|-)\s+[0-9]+ " "$L" | cut -c1-170
grep -E "^\s+[0-9]+ (passed|failed|skipped|flaky)" "$L"
sb=good; big=good
grep -qE "^\s+x .*scrollbar\.spec\.mjs.*(never behind a scrollbar|VZ-14)" "$L" && sb=bad
grep -qE "scrollbar\.spec\.mjs.*(never behind a scrollbar|VZ-14)" "$L" || sb=absent
# BIG-1 and BIG-5 at 10^6 rows exist only from some commit on; before it, "absent".
grep -qE "virtualisation\.spec\.mjs.*\((BIG-1|BIG-5)\)" "$L" || big=absent
grep -qE "^\s+x .*virtualisation\.spec\.mjs.*\((BIG-1|BIG-5)\)" "$L" && big=bad
echo "verdict $c scrollbar=$sb big=$big"
fuser -k 5399/tcp >/dev/null 2>&1
exit 0
