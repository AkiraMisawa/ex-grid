#!/usr/bin/env bash
# Packs ExGrid and ExGrid.MudBlazor, reads back what the packages declare, and builds and
# publishes an application that takes them from the packed files alone (ADR-0042). ExSheet and
# ExSheet.Engine are packed and taken the same way, into a feed of their own, and so are
# ExPivot.Engine, ExPivot and ExPivot.MudBlazor, into another, with ExGrid.Data and
# ExGrid.Data.Arrow, which ship beside ExPivot: .feed holds exactly what the release publishes,
# and neither product, nor the family's data packages, is part of that release yet (ADR-0046,
# ADR-0058, ADR-0063, ADR-0064). A Snapshot is written to an Arrow stream and read back through
# the packed data packages, and the check fails if it comes back different (DA-16).
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
sheetfeed="$here/.sheet-feed"
pivotfeed="$here/.pivot-feed"
cache="$here/.nuget-packages"
out="$here/.publish"

fail() { echo "package check: $*" >&2; exit 1; }

rm -rf "$feed" "$sheetfeed" "$pivotfeed" "$cache" "$out" "$here/bin" "$here/obj" "$here/RoundTrip/bin" "$here/RoundTrip/obj"
mkdir -p "$feed" "$sheetfeed" "$pivotfeed"

echo "== pack $version"
for project in ExGrid ExGrid.MudBlazor; do
  dotnet pack "$root/src/$project" -c Release -o "$feed" -p:Version="$version" --nologo
done
for project in ExSheet.Engine ExSheet; do
  dotnet pack "$root/src/$project" -c Release -o "$sheetfeed" -p:Version="$version" --nologo
done
for project in ExGrid.Data ExGrid.Data.Arrow ExPivot.Engine ExPivot ExPivot.MudBlazor; do
  dotnet pack "$root/src/$project" -c Release -o "$pivotfeed" -p:Version="$version" --nologo
done

echo "== what the packages declare"
feedof() { case "$1" in ExSheet|ExSheet.*) echo "$sheetfeed" ;; ExPivot|ExPivot.*|ExGrid.Data|ExGrid.Data.*) echo "$pivotfeed" ;; *) echo "$feed" ;; esac; }
nuspec() { unzip -p "$(feedof "$1")/$1.$version.nupkg" "$1.nuspec"; }
entries() { unzip -Z1 "$(feedof "$1")/$1.$version.nupkg"; }
for id in ExGrid ExGrid.MudBlazor ExSheet.Engine ExSheet ExGrid.Data ExGrid.Data.Arrow ExPivot.Engine ExPivot ExPivot.MudBlazor; do
  [ -f "$(feedof "$id")/$id.$version.snupkg" ] || fail "$id has no symbol package"
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

# The family's data package depends on nothing at all, not even on the grid (ADR-0063, DA-1).
if grep -q '<dependency ' <<<"$(nuspec ExGrid.Data)"; then fail "ExGrid.Data declares a dependency"; fi
# Its Arrow package depends on exactly the ExGrid.Data it was built with, and on Apache.Arrow
# within the major version it was built and tested with, and on nothing else (ADR-0064, DA-1).
arrowdeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExGrid.Data.Arrow)" | sort)
[ "$arrowdeps" = "$(printf '%s\n' "<dependency id=\"Apache.Arrow\" version=\"[23.0.0, 24.0.0)\"" "<dependency id=\"ExGrid.Data\" version=\"[$version]\"" | sort)" ] \
  || fail "ExGrid.Data.Arrow's dependencies are not exactly ExGrid.Data $version and Apache.Arrow [23.0.0, 24.0.0): $arrowdeps"
# ExPivot's engine depends on exactly the ExGrid.Data it was built with, and on nothing else: it
# aggregates a Snapshot (ADR-0063, PV-1).
enginedeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExPivot.Engine)" | sort)
[ "$enginedeps" = "<dependency id=\"ExGrid.Data\" version=\"[$version]\"" ] \
  || fail "ExPivot.Engine's dependencies are not exactly ExGrid.Data $version: $enginedeps"
# Nothing else the family packs references a data package (DA-1).
for id in ExGrid ExGrid.MudBlazor ExSheet.Engine ExSheet ExPivot ExPivot.MudBlazor; do
  if grep -qE '<dependency id="ExGrid\.Data(\.Arrow)?"' <<<"$(nuspec "$id")"; then fail "$id references a data package"; fi
done
# ExPivot depends on exactly the core and the engine it was built with, and on nothing else.
pivotdeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExPivot)" | sort)
[ "$pivotdeps" = "$(printf '%s\n' "<dependency id=\"ExGrid\" version=\"[$version]\"" "<dependency id=\"ExPivot.Engine\" version=\"[$version]\"" | sort)" ] \
  || fail "ExPivot's dependencies are not exactly ExGrid $version and ExPivot.Engine $version: $pivotdeps"
# Its Wrapper depends on exactly ExPivot and the grid's Wrapper it was built with, and on MudBlazor
# from the floor the grid's Wrapper takes (ADR-0061).
muddeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExPivot.MudBlazor)" | sort)
[ "$muddeps" = "$(printf '%s\n' "<dependency id=\"ExGrid.MudBlazor\" version=\"[$version]\"" "<dependency id=\"ExPivot\" version=\"[$version]\"" "<dependency id=\"MudBlazor\" version=\"9.0.0\"" | sort)" ] \
  || fail "ExPivot.MudBlazor's dependencies are not exactly ExPivot and ExGrid.MudBlazor $version and MudBlazor 9.0.0: $muddeps"

# The release publishes .feed as it is, so nothing of ExSheet or ExPivot may be in it (ADR-0046,
# ADR-0058).
if ls "$feed" | grep -qi '^exsheet'; then fail "the release feed $feed holds an ExSheet package"; fi
if ls "$feed" | grep -qi '^expivot'; then fail "the release feed $feed holds an ExPivot package"; fi
if ls "$feed" | grep -qi '^exgrid\.data'; then fail "the release feed $feed holds an ExGrid.Data package"; fi

echo "== an application that takes them"
dotnet publish "$here" -c Release -o "$out" --nologo \
  -p:ExGridVersion="$version" -p:RestorePackagesPath="$cache"

# Restored from the packed files, not from anywhere else.
for id in exgrid exgrid.mudblazor exsheet.engine exsheet exgrid.data exgrid.data.arrow expivot.engine expivot expivot.mudblazor; do
  meta="$cache/$id/$version/.nupkg.metadata"
  from=$feed
  case "$id" in exsheet|exsheet.*) from=$sheetfeed ;; expivot|expivot.*|exgrid.data|exgrid.data.*) from=$pivotfeed ;; esac
  [ -f "$meta" ] || fail "$id $version was not restored"
  grep -qF "$from" "$meta" || fail "$id $version came from somewhere other than $from"
done

# The paths the README tells a Consumer to link, and the module the component imports.
for f in _content/ExGrid/ex-grid.css _content/ExGrid/ex-grid.js _content/ExGrid.MudBlazor/mud-ex-grid.css \
  _content/ExPivot/ex-pivot.css _content/ExPivot.MudBlazor/mud-ex-pivot.css; do
  [ -f "$out/wwwroot/$f" ] || fail "the published application has no $f"
done
grep -qF '"./_content/ExGrid/ex-grid.js"' "$root/src/ExGrid/Components/ExGrid.razor" \
  || fail "the component no longer imports ./_content/ExGrid/ex-grid.js; update this check with it"

echo "== a Snapshot through an Arrow stream and back, through the packed packages"
# RoundTrip runs what the application above compiled for a browser: it writes a Snapshot of every
# kind, with Blanks, captions, a Record Key and a version, and reads it back (ADR-0064, DA-16).
dotnet build "$here/RoundTrip" -c Release --nologo \
  -p:ExGridVersion="$version" -p:RestorePackagesPath="$cache"
for id in exgrid.data exgrid.data.arrow; do
  grep -qF "$pivotfeed" "$cache/$id/$version/.nupkg.metadata" || fail "the round trip took $id $version from somewhere other than $pivotfeed"
done
dotnet "$here/RoundTrip/bin/Release/net10.0/PackageSmoke.RoundTrip.dll" \
  || fail "a Snapshot written to an Arrow stream and read back through the packed packages is not the one written"

echo "package check: $version passed"
