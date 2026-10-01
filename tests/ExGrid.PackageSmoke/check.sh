#!/usr/bin/env bash
# Packs ExGrid and ExGrid.MudBlazor, reads back what the packages declare, and builds and
# publishes an application that takes them from the packed files alone (ADR-0042). ExSheet,
# ExSheet.Engine and ExSheet.MudBlazor are packed and taken the same way, into a feed of their own:
# .feed holds exactly what the release publishes, and ExSheet is not part of that release yet
# (ADR-0046; ExSheet.MudBlazor, ADR-0019's note of 2026-09-30).
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
cache="$here/.nuget-packages"
out="$here/.publish"

fail() { echo "package check: $*" >&2; exit 1; }

rm -rf "$feed" "$sheetfeed" "$cache" "$out" "$here/bin" "$here/obj"
mkdir -p "$feed" "$sheetfeed"

echo "== pack $version"
for project in ExGrid ExGrid.MudBlazor; do
  dotnet pack "$root/src/$project" -c Release -o "$feed" -p:Version="$version" --nologo
done
for project in ExSheet.Engine ExSheet ExSheet.MudBlazor; do
  dotnet pack "$root/src/$project" -c Release -o "$sheetfeed" -p:Version="$version" --nologo
done

echo "== what the packages declare"
feedof() { case "$1" in ExSheet|ExSheet.*) echo "$sheetfeed" ;; *) echo "$feed" ;; esac; }
nuspec() { unzip -p "$(feedof "$1")/$1.$version.nupkg" "$1.nuspec"; }
entries() { unzip -Z1 "$(feedof "$1")/$1.$version.nupkg"; }
for id in ExGrid ExGrid.MudBlazor ExSheet.Engine ExSheet ExSheet.MudBlazor; do
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

# The release publishes .feed as it is, so nothing of ExSheet may be in it (ADR-0046).
if ls "$feed" | grep -qi '^exsheet'; then fail "the release feed $feed holds an ExSheet package"; fi

echo "== an application that takes them"
dotnet publish "$here" -c Release -o "$out" --nologo \
  -p:ExGridVersion="$version" -p:RestorePackagesPath="$cache"

# Restored from the packed files, not from anywhere else.
for id in exgrid exgrid.mudblazor exsheet.engine exsheet exsheet.mudblazor; do
  meta="$cache/$id/$version/.nupkg.metadata"
  from=$feed
  case "$id" in exsheet|exsheet.*) from=$sheetfeed ;; esac
  [ -f "$meta" ] || fail "$id $version was not restored"
  grep -qF "$from" "$meta" || fail "$id $version came from somewhere other than $from"
done

# The paths the README tells a Consumer to link, and the module the component imports.
for f in _content/ExGrid/ex-grid.css _content/ExGrid/ex-grid.js _content/ExGrid.MudBlazor/mud-ex-grid.css \
         _content/ExSheet/ex-sheet.css _content/ExSheet.MudBlazor/mud-ex-sheet.css; do
  [ -f "$out/wwwroot/$f" ] || fail "the published application has no $f"
done
grep -qF '"./_content/ExGrid/ex-grid.js"' "$root/src/ExGrid/Components/ExGrid.razor" \
  || fail "the component no longer imports ./_content/ExGrid/ex-grid.js; update this check with it"

echo "package check: $version passed"
