set -e
cd /home/user/ex-grid/.claude/worktrees/rel
nix develop /home/user/ex-grid -c dotnet publish samples/ExGrid.DemoHost -c Release -o $SCRATCH/measure-rc3/publish
nix develop /home/user/ex-grid -c dotnet build samples/ExGrid.DemoApi -c Release
nix develop /home/user/ex-grid -c dotnet run -c Release --project $SCRATCH/measure-rc3/csvgen -- $SCRATCH/measure-rc3/pv21/trades-1000000.csv 1000000
