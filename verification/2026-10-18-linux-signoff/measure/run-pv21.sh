B=/home/user/ex-grid/.claude/worktrees/rel/tests/ExGrid.Browser
O=$SCRATCH/measure-rc3/pv21
fuser -k 5440/tcp 2>/dev/null; fuser -k -TERM 8440/tcp 2>/dev/null; sleep 2
(cd $B && setsid node static-host.mjs $SCRATCH/measure-rc3/publish/wwwroot 5440 > $O/static.log 2>&1 &)
rm -rf $O/api-data; mkdir -p $O/api-data
(cd /home/user/ex-grid/.claude/worktrees/rel/samples/ExGrid.DemoApi/bin/Release/net10.0 && ASPNETCORE_ENVIRONMENT=Development EXGRID_DEMO_TRADES=1000000 EXGRID_DEMO_DATA=$O/api-data setsid nix develop /home/user/ex-grid -c dotnet ExGrid.DemoApi.dll --urls http://localhost:8440 > $O/api.log 2>&1 &)
for i in $(seq 1 150); do curl -sf http://localhost:5440/wide >/dev/null && curl -sf http://localhost:8440/api/status >/dev/null && break; sleep 2; done
echo "ready after $i polls $(date +%T) load $(cat /proc/loadavg)"
cd $B && EXGRID_BASE_URL=http://localhost:5440 EXGRID_MEASURE=pivot EXGRID_MEASURE_CSV=$O/trades-1000000.csv timeout 3600 xvfb-run -a -s "-screen 0 2560x1600x24" nix develop '/home/user/ex-grid#browser' -c npx playwright test --project=chrome measure-pivot.spec.mjs > $O/run.log 2>&1
echo "exit $? $(date +%T) load $(cat /proc/loadavg)"
fuser -k 5440/tcp; fuser -k -TERM 8440/tcp; sleep 5; fuser -k 8440/tcp
