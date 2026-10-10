#!/bin/bash
# Ticket 13: is the machine free of the other tracks' work? Exits 0 only when none of these is busy:
# - the Codex track's worktrees (~/.codex-workspaces/worktrees/33609b58…): any dotnet, testhost,
#   node, Playwright or Chrome-driving process of theirs;
# - the integration worktree (.claude/worktrees/live-data-next-cc) and every other checkout: a
#   Playwright test runner, a dotnet test/build or MSBuild using CPU, a Playwright-driven Chrome;
# - the machine-wide layer-3 lock held by someone else.
# This agent's own processes (ld-measure-after, scratchpad/ld13) do not count. The [x] patterns
# cannot match this script's own command line (CLAUDE.md's pgrep trap).
busy=0
L=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad/layer3.lock
if [ -d "$L" ] && [ "${HOLDING_LAYER3_LOCK:-0}" != "1" ]; then echo "layer-3 lock held ($(/usr/bin/stat -f %Sm "$L"))"; busy=1; fi
codex=$(ps -axo pid,pcpu,etime,command | grep '[c]odex-workspaces/worktrees/33609b58' | grep -v idle-check)
if [ -n "$codex" ]; then
  if echo "$codex" | grep -Eq 'dotnet|testhost|playwright|static-host|node |MSBuild|chrome'; then echo "codex track:"; echo "$codex" | cut -c1-200; busy=1; fi
fi
runners=$(ps -axo pid,pcpu,etime,command | grep -E '[p]laywright(\.js|/cli\.js)? test|[p]laywright/lib/worker' | grep -v 'ld-measure-after' | while read -r pid rest; do
  # A runner started from this worktree (its working directory) is this agent's own.
  cwd=$(lsof -a -p "$pid" -d cwd -Fn 2>/dev/null | sed -n 's/^n//p')
  case "$cwd" in *ld-measure-after*) ;; *) echo "$pid $rest" ;; esac
done)
if [ -n "$runners" ]; then echo "playwright runners:"; echo "$runners" | cut -c1-200; busy=1; fi
chromes=$(ps -axo pid,pcpu,command | grep -E '[p]laywright_chromiumdev_profile|[m]s-playwright|[r]emote-debugging-pipe' | awk '$2 > 5.0')
if [ -n "$chromes" ] && [ "${OURS_RUNNING:-0}" != "1" ]; then echo "busy playwright chrome:"; echo "$chromes" | cut -c1-160; busy=1; fi
dotnets=$(ps -axo pid,pcpu,etime,command | grep -E '[d]otnet|[M]SBuild|[t]esthost|[V]BCSCompiler' | grep -v -e 'ld-measure-after' -e 'scratchpad/ld13' -e 'localhost:6798' -e 'localhost:8798' -e 'localhost:8799' | awk '$2 > 15.0')
if [ -n "$dotnets" ]; then echo "busy dotnet:"; echo "$dotnets" | cut -c1-200; busy=1; fi
tests=$(ps -axo pid,pcpu,etime,command | grep -E '[d]otnet (test|build|publish)|[d]otnet exec .*testhost' | grep -v -e 'ld-measure-after' -e 'scratchpad/ld13')
if [ -n "$tests" ]; then echo "dotnet test/build running:"; echo "$tests" | cut -c1-200; busy=1; fi
echo "load $(sysctl -n vm.loadavg)"
exit $busy
