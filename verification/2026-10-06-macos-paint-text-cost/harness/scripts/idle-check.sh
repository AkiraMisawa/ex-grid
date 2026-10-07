#!/bin/bash
# Are the Codex track and the Fluxor spike agent idle? Exit 0 only when neither has a dotnet,
# testhost, node or Chrome-driving process using CPU. The [x] patterns cannot match this script.
busy=0
procs=$(ps -axo pid,pcpu,etime,command | grep -E '[c]odex-workspaces/worktrees/33609b58|[l]ive-data-fluxor' | grep -v idle-check)
if [ -n "$procs" ]; then
  echo "their processes:"; echo "$procs" | cut -c1-200
  if echo "$procs" | grep -E 'dotnet|testhost|node|playwright|chrome|static-host|MSBuild' | awk '$2 > 1.0' | grep -q .; then busy=1; fi
fi
# Any Playwright-driven Chrome on the machine that is not ours (ours run with the paint-text profile env).
chromes=$(ps -axo pid,pcpu,command | grep -E '[p]laywright_chromiumdev_profile|[m]s-playwright' | awk '$2 > 5.0')
if [ -n "$chromes" ] && [ "${OURS_RUNNING:-0}" != "1" ]; then echo "busy playwright chrome:"; echo "$chromes" | cut -c1-160; busy=1; fi
# Other heavy dotnet work that is not this agent's.
others=$(ps -axo pid,pcpu,command | grep -E '[d]otnet|[M]SBuild|[t]esthost' | grep -v 'live-data-paint-text' | grep -v 'scratchpad/paint' | awk '$2 > 20.0')
if [ -n "$others" ]; then echo "other busy dotnet:"; echo "$others" | cut -c1-200; busy=1; fi
echo "load $(sysctl -n vm.loadavg)"
exit $busy
