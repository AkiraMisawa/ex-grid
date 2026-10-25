#!/bin/bash
# WR-10 at 1e85052e, by the Definition of Done's §22 Step 4, under xvfb in WSL.
set -u
W=~/src/hobby/ex-grid-wr10
cd $W
git checkout -- . ; git clean -fdq samples tests/ExGrid.Browser/test-results 2>/dev/null
git fetch -q origin
git checkout -q --detach 1e85052e3449dca2541b96a8a33712e7c8d98b8f
C=$(git rev-parse HEAD)
R=verification/2026-10-25-linux-mudblazor-floor-20
mkdir -p "$R"
sed -i -E 's|(Include="MudBlazor" Version=")[^"]*"|\19.6.0"|' samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj samples/ExChart.Showcases/ExChart.Showcases.csproj
rm -rf samples/ExGrid.DemoHost/bin samples/ExGrid.DemoHost/obj/Debug \
       samples/ExGrid.DemoHost.Server/bin samples/ExGrid.DemoHost.Server/obj/Debug
nix develop -c dotnet build samples/ExGrid.DemoHost > $R/build.log 2>&1
nix develop -c dotnet build samples/ExGrid.DemoHost.Server >> $R/build.log 2>&1
nix develop -c dotnet build samples/ExGrid.DemoApi >> $R/build.log 2>&1
{ echo "WebAssembly:"; strings -el samples/ExGrid.DemoHost/bin/Debug/net10.0/wwwroot/_framework/MudBlazor.*.wasm | grep -A1 '^FileVersion'
  echo "Server:"; strings -el samples/ExGrid.DemoHost.Server/bin/Debug/net10.0/MudBlazor.dll | grep -A1 '^FileVersion'; } > $R/mudblazor-versions.txt
{ google-chrome --version; microsoft-edge --version; } > $R/browsers.txt 2>&1
XR=$(nix build --no-link --print-out-paths nixpkgs#xvfb-run)/bin/xvfb-run
cd tests/ExGrid.Browser
nix develop -c npm ci > /dev/null 2>&1
G="[Mm]ud|is not the grid's \(KB-35|apply the condition \(KB-31, SRV-5\)|Escape, Escape straight after an Inner Popup|a menu taking the keyboard a round trip late|with and without a Wrapper|SH-46/DC-59: the Selection's outline|the palette reaches the pane|RI-3: every page opens|inspectors\.spec\.mjs|WR-7a|DC-58: a bold number that fits"
EXGRID_BASE_URL=http://localhost:5338 nix develop -c $XR -a -s "-screen 0 2560x1600x24" npx playwright test --grep "$G" 2>&1 | tee "../../$R/mudblazor-floor.log" > /dev/null
EXGRID_BASE_URL=http://localhost:5337 EXGRID_HOSTING=server nix develop -c $XR -a -s "-screen 0 2560x1600x24" npx playwright test --grep "$G" 2>&1 | tee "../../$R/mudblazor-floor-server.log" > /dev/null
cd ../..
git checkout -- samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj samples/ExChart.Showcases/ExChart.Showcases.csproj
curl -s https://api.nuget.org/v3/registration5-gz-semver2/mudblazor/index.json --compressed > /tmp/mud-reg.json
versions=$(curl -s https://api.nuget.org/v3-flatcontainer/mudblazor/index.json \
  | grep -oE '"9\.[0-9]+\.[0-9]+"' | tr -d '"' | awk -F. '$2 >= 6' | sort -V)
echo "$versions" > $R/versions-listed.txt
for v in $versions; do
  for t in ExGrid.MudBlazor.Tests ExSheet.MudBlazor.Tests ExPivot.MudBlazor.Tests; do
    sed -i "s|</Project>|  <ItemGroup><PackageReference Include=\"MudBlazor\" Version=\"$v\" /></ItemGroup>\n</Project>|" \
      tests/$t/$t.csproj
    echo "== $t, MudBlazor $v, at $(git rev-parse HEAD)"
    nix develop -c dotnet test tests/$t
    strings -el tests/$t/bin/Debug/net10.0/MudBlazor.dll | grep -A1 '^FileVersion'
    git checkout -- tests/$t/$t.csproj
  done 2>&1 | tee "$R/mudblazor-layer2-$v.log" > /dev/null
done
echo WR10-DONE > $R/done.txt
