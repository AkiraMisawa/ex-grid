#!/bin/bash
# Run 19's DemoHosts in WSL, at the verified commit: WebAssembly 5318, Server 6317, the demo API 8318 and 8317.
cd ~/src/hobby/ex-grid-run19
L=~/src/hobby/run19-logs; mkdir -p $L
export EXGRID_DEMO_TRADES=20000
nohup nix develop --command dotnet run --no-build --project samples/ExGrid.DemoHost --urls http://localhost:5318 > $L/wasm.log 2>&1 &
EXGRID_HOST_LOG=/tmp/exgrid-server-host-6317.log nohup nix develop --command dotnet run --no-build --project samples/ExGrid.DemoHost.Server --urls http://localhost:6317 > $L/server.log 2>&1 &
nohup nix develop --command dotnet run --no-build --project samples/ExGrid.DemoApi --urls http://localhost:8318 > $L/api-8318.log 2>&1 &
nohup nix develop --command dotnet run --no-build --project samples/ExGrid.DemoApi --urls http://localhost:8317 > $L/api-8317.log 2>&1 &
