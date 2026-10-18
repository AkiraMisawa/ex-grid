cd /home/user/ex-grid/.claude/worktrees/ref
nix develop /home/user/ex-grid -c dotnet publish samples/ExGrid.DemoHost -c Release -o $SCRATCH/measure-rc3/publish-ref > $SCRATCH/measure-rc3/build-ref.log 2>&1; echo "ref publish $?"
nix develop /home/user/ex-grid -c dotnet build samples/ExGrid.DemoApi -c Release >> $SCRATCH/measure-rc3/build-ref.log 2>&1; echo "ref api $?"
cd /home/user/ex-grid/.claude/worktrees/perf
nix develop /home/user/ex-grid -c dotnet publish samples/ExGrid.DemoHost -c Release -o $SCRATCH/measure-rc3/publish-perf > $SCRATCH/measure-rc3/build-perf.log 2>&1; echo "perf publish $?"
nix develop /home/user/ex-grid -c dotnet build tests/ExPivot.Components -c Release >> $SCRATCH/measure-rc3/build-perf.log 2>&1; echo "perf components $?"
