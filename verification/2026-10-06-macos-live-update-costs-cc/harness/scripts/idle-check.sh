#!/bin/bash
# Is the other track (the Codex session's worktrees) idle? Prints its busy processes and recently
# modified result files, and exits 0 only when there are none. The patterns are written so that
# they cannot match this script's own processes (the [c] trick, CLAUDE.md's pgrep trap).
OTHER="$HOME/.codex-workspaces/worktrees/33609b58-5b7d-4f08-b904-bcaa95f6f60e/ex-grid"
busy=0
procs=$(ps -axo pid,pcpu,etime,command | grep '[c]odex-workspaces/worktrees/33609b58' | grep -v 'idle-check')
if [ -n "$procs" ]; then
  echo "other-track processes:"; echo "$procs" | cut -c1-220
  # Any of them using CPU, or any dotnet/testhost/node/chrome of theirs at all, counts as busy.
  if echo "$procs" | awk '$2 > 1.0' | grep -q .; then busy=1; fi
  if echo "$procs" | grep -Eq 'dotnet|testhost|playwright|static-host|node '; then busy=1; fi
fi
# A raw result written lately means a run; the README or the harness copy being edited does not.
recent=$(find "$OTHER/.claude/worktrees" -path '*/verification/*' -newermt '-4 minutes' -type f 2>/dev/null | grep -v -e '/README.md$' -e '/harness/' | head -50)
if [ -n "$recent" ]; then echo "other track: $(echo "$recent" | wc -l | tr -d ' ') raw result file(s) modified in the last 4 minutes"; busy=1; fi
edits=$(find "$OTHER/.claude/worktrees" -path '*/verification/*' -newermt '-4 minutes' -type f 2>/dev/null | grep -e '/README.md$' -e '/harness/' | wc -l | tr -d ' ')
[ "$edits" != "0" ] && echo "other track: $edits README/harness file(s) edited lately (not a run)"
# MSBuild / dotnet processes that are not ours and not the known DemoApi background.
others=$(ps -axo pid,pcpu,command | grep -E '[d]otnet|[M]SBuild|[t]esthost' | grep -v 'exgrid-cds-aot-bench' | grep -v 'live-data-next-cc' | awk '$2 > 20.0')
if [ -n "$others" ]; then echo "other busy dotnet:"; echo "$others" | cut -c1-200; busy=1; fi
echo "load $(sysctl -n vm.loadavg)"
exit $busy
