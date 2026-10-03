#!/usr/bin/env bash
# Packs the family's ten packages — ExGrid and ExGrid.MudBlazor; ExSheet.Engine, ExSheet and
# ExSheet.MudBlazor; ExGrid.Data and ExGrid.Data.Arrow; ExPivot.Engine, ExPivot and
# ExPivot.MudBlazor — reads back what each declares, and builds and publishes an application that
# takes them from the packed files alone (ADR-0042; the family ships at one version since its note
# of 2026-10-03). A Snapshot is written to an Arrow stream and read back through the packed data
# packages, and the check fails if it comes back different (DA-16).
#
#   tests/ExGrid.PackageSmoke/check.sh [version]
#
# The version defaults to a throwaway 0.0.0-smoke.<time>; CI passes its own, and the release
# workflow the tag's. The packages are left in tests/ExGrid.PackageSmoke/.feed, which is what
# the release publishes: the files this script tested.
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
root=$(cd "$here/../.." && pwd)
version=${1:-0.0.0-smoke.$(date +%s)}
feed="$here/.feed"
cache="$here/.nuget-packages"
out="$here/.publish"

fail() { echo "package check: $*" >&2; exit 1; }

rm -rf "$feed" "$cache" "$out" "$here/bin" "$here/obj" "$here/RoundTrip/bin" "$here/RoundTrip/obj"
mkdir -p "$feed"

echo "== pack $version"
packages="ExGrid ExGrid.MudBlazor ExSheet.Engine ExSheet ExSheet.MudBlazor ExGrid.Data ExGrid.Data.Arrow ExPivot.Engine ExPivot ExPivot.MudBlazor"
for project in $packages; do
  dotnet pack "$root/src/$project" -c Release -o "$feed" -p:Version="$version" --nologo
done

echo "== what the packages declare"
nuspec() { unzip -p "$feed/$1.$version.nupkg" "$1.nuspec"; }
entries() { unzip -Z1 "$feed/$1.$version.nupkg"; }
for id in $packages; do
  [ -f "$feed/$id.$version.snupkg" ] || fail "$id has no symbol package"
  spec=$(nuspec "$id")
  grep -q '<license type="expression">MIT</license>' <<<"$spec" || fail "$id does not declare MIT"
  grep -q '<readme>README.md</readme>' <<<"$spec" || fail "$id has no readme"
  grep -qE '<repository type="git" url="https://github.com/AkiraMisawa/ex-grid"[^>]* commit="[0-9a-f]{40}"' <<<"$spec" \
    || fail "$id does not name its repository and commit"
  files=$(entries "$id")
  for f in README.md "lib/net10.0/$id.dll" "lib/net10.0/$id.xml"; do
    grep -qxF "$f" <<<"$files" || fail "$id is missing $f"
  done
done
# The core's one dependency, at the floor ADR-0022 fixes.
grep -q '<dependency id="Microsoft.AspNetCore.Components.Web" version="10.0.0"' <<<"$(nuspec ExGrid)" \
  || fail "ExGrid's dependency on Microsoft.AspNetCore.Components.Web is not 10.0.0"
# The Wrapper takes exactly this core (ADR-0042) and MudBlazor from its floor.
grep -qF "<dependency id=\"ExGrid\" version=\"[$version]\"" <<<"$(nuspec ExGrid.MudBlazor)" \
  || fail "ExGrid.MudBlazor does not depend on exactly ExGrid $version"
grep -q '<dependency id="MudBlazor" version="9.0.0"' <<<"$(nuspec ExGrid.MudBlazor)" \
  || fail "ExGrid.MudBlazor's dependency on MudBlazor is not 9.0.0"
# The engine depends on nothing at all: a server computes a Sheet with it alone (ADR-0047, SH-1).
if grep -q '<dependency ' <<<"$(nuspec ExSheet.Engine)"; then fail "ExSheet.Engine declares a dependency"; fi
# ExSheet depends on exactly the core and the engine it was built with, and on nothing else: the
# one direction there is (ADR-0046, SH-1). Anything more comes to it through ExGrid.
sheetdeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExSheet)" | sort)
[ "$sheetdeps" = "$(printf '%s\n' "<dependency id=\"ExGrid\" version=\"[$version]\"" "<dependency id=\"ExSheet.Engine\" version=\"[$version]\"" | sort)" ] \
  || fail "ExSheet's dependencies are not exactly ExGrid $version and ExSheet.Engine $version: $sheetdeps"

# ExSheet.MudBlazor depends on exactly the ExSheet and the Wrapper it was built with, and on
# MudBlazor from the Wrapper's floor; nothing else (SH-47). And it ships no script of its own: its
# one static asset is its stylesheet (ADR-0021).
mudsheetdeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExSheet.MudBlazor)" | sort)
[ "$mudsheetdeps" = "$(printf '%s\n' "<dependency id=\"ExGrid.MudBlazor\" version=\"[$version]\"" "<dependency id=\"ExSheet\" version=\"[$version]\"" '<dependency id="MudBlazor" version="9.0.0"' | sort)" ] \
  || fail "ExSheet.MudBlazor's dependencies are not exactly ExGrid.MudBlazor $version, ExSheet $version and MudBlazor 9.0.0: $mudsheetdeps"
if entries ExSheet.MudBlazor | grep -qiE '\.(js|mjs|cjs)$'; then fail "ExSheet.MudBlazor ships a script"; fi
entries ExSheet.MudBlazor | grep -qxF 'staticwebassets/mud-ex-sheet.css' || fail "ExSheet.MudBlazor is missing staticwebassets/mud-ex-sheet.css"
# The Wrapper still takes no ExSheet package: the direction is one-way (SH-47).
if grep -q '<dependency id="ExSheet' <<<"$(nuspec ExGrid.MudBlazor)"; then fail "ExGrid.MudBlazor depends on an ExSheet package"; fi

# The family's data package depends on nothing at all, not even on the grid (ADR-0064, DA-1).
if grep -q '<dependency ' <<<"$(nuspec ExGrid.Data)"; then fail "ExGrid.Data declares a dependency"; fi
# Its Arrow package depends on exactly the ExGrid.Data it was built with, and on Apache.Arrow
# within the major version it was built and tested with, and on nothing else (ADR-0065, DA-1).
arrowdeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExGrid.Data.Arrow)" | sort)
[ "$arrowdeps" = "$(printf '%s\n' "<dependency id=\"Apache.Arrow\" version=\"[23.0.0, 24.0.0)\"" "<dependency id=\"ExGrid.Data\" version=\"[$version]\"" | sort)" ] \
  || fail "ExGrid.Data.Arrow's dependencies are not exactly ExGrid.Data $version and Apache.Arrow [23.0.0, 24.0.0): $arrowdeps"
# ExPivot's engine depends on exactly the ExGrid.Data it was built with, and on nothing else: it
# aggregates a Snapshot (ADR-0064, PV-1).
enginedeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExPivot.Engine)" | sort)
[ "$enginedeps" = "<dependency id=\"ExGrid.Data\" version=\"[$version]\"" ] \
  || fail "ExPivot.Engine's dependencies are not exactly ExGrid.Data $version: $enginedeps"
# Nothing else the family packs references a data package (DA-1).
for id in ExGrid ExGrid.MudBlazor ExSheet.Engine ExSheet ExSheet.MudBlazor ExPivot ExPivot.MudBlazor; do
  if grep -qE '<dependency id="ExGrid\.Data(\.Arrow)?"' <<<"$(nuspec "$id")"; then fail "$id references a data package"; fi
done
# ExPivot depends on exactly the core and the engine it was built with, and on nothing else.
pivotdeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExPivot)" | sort)
[ "$pivotdeps" = "$(printf '%s\n' "<dependency id=\"ExGrid\" version=\"[$version]\"" "<dependency id=\"ExPivot.Engine\" version=\"[$version]\"" | sort)" ] \
  || fail "ExPivot's dependencies are not exactly ExGrid $version and ExPivot.Engine $version: $pivotdeps"
# Its Wrapper depends on exactly ExPivot and the grid's Wrapper it was built with, and on MudBlazor
# from the floor the grid's Wrapper takes (ADR-0062).
muddeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExPivot.MudBlazor)" | sort)
[ "$muddeps" = "$(printf '%s\n' "<dependency id=\"ExGrid.MudBlazor\" version=\"[$version]\"" "<dependency id=\"ExPivot\" version=\"[$version]\"" "<dependency id=\"MudBlazor\" version=\"9.0.0\"" | sort)" ] \
  || fail "ExPivot.MudBlazor's dependencies are not exactly ExPivot and ExGrid.MudBlazor $version and MudBlazor 9.0.0: $muddeps"

# The release publishes .feed as it is, so it holds the ten packages and their symbol packages, and
# nothing else (ADR-0042).
expected=$(for id in $packages; do printf '%s\n' "$id.$version.nupkg" "$id.$version.snupkg"; done | sort)
[ "$(ls "$feed" | sort)" = "$expected" ] || fail "the release feed $feed does not hold exactly the ten packages: $(ls "$feed" | tr '\n' ' ')"

echo "== an application that takes them"
dotnet publish "$here" -c Release -o "$out" --nologo \
  -p:ExGridVersion="$version" -p:RestorePackagesPath="$cache"

# Restored from the packed files, not from anywhere else.
for id in $packages; do
  id=${id,,}
  meta="$cache/$id/$version/.nupkg.metadata"
  [ -f "$meta" ] || fail "$id $version was not restored"
  grep -qF "$feed" "$meta" || fail "$id $version came from somewhere other than $feed"
done

# The paths the README tells a Consumer to link, and the module the component imports.
for f in _content/ExGrid/ex-grid.css _content/ExGrid/ex-grid.js _content/ExGrid.MudBlazor/mud-ex-grid.css \
         _content/ExSheet/ex-sheet.css _content/ExSheet.MudBlazor/mud-ex-sheet.css \
         _content/ExPivot/ex-pivot.css _content/ExPivot.MudBlazor/mud-ex-pivot.css; do
  [ -f "$out/wwwroot/$f" ] || fail "the published application has no $f"
done
grep -qF '"./_content/ExGrid/ex-grid.js"' "$root/src/ExGrid/Components/ExGrid.razor" \
  || fail "the component no longer imports ./_content/ExGrid/ex-grid.js; update this check with it"

echo "== a Snapshot through an Arrow stream and back, through the packed packages"
# RoundTrip runs what the application above compiled for a browser: it writes a Snapshot of every
# kind, with Blanks, captions, a Record Key and a version, and reads it back (ADR-0065, DA-16).
dotnet build "$here/RoundTrip" -c Release --nologo \
  -p:ExGridVersion="$version" -p:RestorePackagesPath="$cache"
for id in exgrid.data exgrid.data.arrow; do
  grep -qF "$feed" "$cache/$id/$version/.nupkg.metadata" || fail "the round trip took $id $version from somewhere other than $feed"
done
dotnet "$here/RoundTrip/bin/Release/net10.0/PackageSmoke.RoundTrip.dll" \
  || fail "a Snapshot written to an Arrow stream and read back through the packed packages is not the one written"

echo "package check: $version passed"
