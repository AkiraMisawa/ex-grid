#!/usr/bin/env bash
# A/B on one container: rc3 (28ab5e1b + disposable harness) against ref (554b3c20, the code PV-48/PV-21 were last recorded on, 2026-10-10).
S=$SCRATCH/measure-rc3; W=/home/user/ex-grid/.claude/worktrees; O=$S/ab; mkdir -p $O
log() { echo "$1 $(date +%T) load $(cut -d' ' -f1-3 /proc/loadavg)" >> $O/run.log; }
tree() { [ $1 = rc3 ] && echo $W/rel || echo $W/ref; }
hostdir() { [ $1 = rc3 ] && echo $S/publish-perf/wwwroot || echo $S/publish-ref/wwwroot; }
comp() { [ $1 = rc3 ] && echo $W/perf || echo $W/ref; }
stop() { fuser -k 5440/tcp 2>/dev/null; fuser -k -TERM 8440/tcp 2>/dev/null; sleep 5; fuser -k 8440/tcp 2>/dev/null; sleep 1; }
pv21() { # n side
  local B=$(tree $2)/tests/ExGrid.Browser; log "pv21 $1 $2 start"; stop
  (cd $B && setsid node static-host.mjs $(hostdir $2) 5440 > $O/pv21-$1-$2-static.log 2>&1 &)
  rm -rf $O/api-data; mkdir -p $O/api-data
  (cd $(tree $2)/samples/ExGrid.DemoApi/bin/Release/net10.0 && ASPNETCORE_ENVIRONMENT=Development EXGRID_DEMO_TRADES=1000000 EXGRID_DEMO_DATA=$O/api-data setsid nix develop /home/user/ex-grid -c dotnet ExGrid.DemoApi.dll --urls http://localhost:8440 > $O/pv21-$1-$2-api.log 2>&1 &)
  for i in $(seq 1 150); do curl -sf http://localhost:5440/wide >/dev/null && curl -sf http://localhost:8440/api/status >/dev/null && break; sleep 2; done
  (cd $B && EXGRID_BASE_URL=http://localhost:5440 EXGRID_MEASURE=pivot EXGRID_MEASURE_CSV=$S/pv21/trades-1000000.csv timeout 3600 xvfb-run -a -s "-screen 0 2560x1600x24" nix develop '/home/user/ex-grid#browser' -c npx playwright test --project=chrome measure-pivot.spec.mjs > $O/pv21-$1-$2.log 2>&1)
  log "pv21 $1 $2 end exit $?"
  local R=$(tree $2)/verification; local d=$(ls -d $R/*-linux 2>/dev/null | grep -v "2026-10-0\|2026-10-1[0-7]" | head -1)
  [ -n "$d" ] && cp $d/metrics.json $O/pv21-$1-$2-metrics.json && rm -rf $d
  stop
}
lv15() {
  local B=$(tree $2)/tests/ExGrid.Browser; log "lv15 $1 $2 start"; stop
  (cd $B && setsid node static-host.mjs $(hostdir $2) 5440 > /dev/null 2>&1 &); sleep 2
  (cd $B && EXGRID_BASE_URL=http://localhost:5440 EXGRID_MEASURE=live timeout 2400 xvfb-run -a -s "-screen 0 2560x1600x24" nix develop '/home/user/ex-grid#browser' -c npx playwright test --project=chrome measure-live.spec.mjs > $O/lv15-$1-$2.log 2>&1)
  log "lv15 $1 $2 end exit $?"
  local R=$(tree $2)/verification; local d=$(ls -d $R/*-linux 2>/dev/null | grep -v "2026-10-0\|2026-10-1[0-7]" | head -1)
  [ -n "$d" ] && cp $d/metrics.json $O/lv15-$1-$2-metrics.json && rm -rf $d
  stop
}
pv48() { # round side advance
  local out=$O/pv48-r$1-$2; mkdir -p $out; log "pv48 r$1 $2 advance $3 start"
  (cd $(comp $2)/tests/ExPivot.Components/bin/Release/net10.0 && DOTNET_TieredCompilation=0 PERF_OUT=$out PERF_ADVANCE_MS=$3 PERF_INNERS=10,100,400 PERF_BATCHES=1,1000 PERF_RUNS=9     timeout 3600 nix develop /home/user/ex-grid -c dotnet ExPivot.Components.Tests.dll -class ExPivot.Components.Tests.PerfWholeUpdate > $out/log 2>&1)
  log "pv48 r$1 $2 end exit $?"
}
lv23() { # b side
  cd $W/rel/tests/ExGrid.Browser; cp $S/lv23/perf-memory.spec.mjs $S/lv23/perf-memory.config.mjs .; stop
  log "lv23 b=$1 $2 start"
  PERF_WWWROOT=$(hostdir $2) EXGRID_BASE_URL=http://localhost:5440 PERF_OUT=$O/lv23-b$1-$2.json PERF_B=$1 PERF_N=20     timeout 1800 nix develop '/home/user/ex-grid#browser' -c npx playwright test -c perf-memory.config.mjs > $O/lv23-b$1-$2.log 2>&1
  log "lv23 b=$1 $2 end exit $?"
  rm -f perf-memory.spec.mjs perf-memory.config.mjs; stop
}
pv21 1 ref; pv21 2 rc3; pv21 3 rc3; pv21 4 ref
lv15 1 rc3; lv15 2 ref; lv15 3 ref; lv15 4 rc3
pv48 1 ref 1; pv48 1 rc3 1; pv48 2 rc3 1; pv48 2 ref 1; pv48 3 ref 5000; pv48 3 rc3 5000
for b in 100 10 400; do lv23 $b rc3; lv23 $b ref; done
echo "done $(date +%T)" >> $O/run.log
