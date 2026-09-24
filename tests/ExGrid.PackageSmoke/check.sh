#!/usr/bin/env bash
# Packs ExGrid and ExGrid.MudBlazor, reads back what the packages declare, and builds and
# publishes an application that takes them from the packed files alone (ADR-0042).
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

rm -rf "$feed" "$cache" "$out" "$here/bin" "$here/obj"
mkdir -p "$feed"

echo "== pack $version"
for project in ExGrid ExGrid.MudBlazor; do
  dotnet pack "$root/src/$project" -c Release -o "$feed" -p:Version="$version" --nologo
done

echo "== what the packages declare"
nuspec() { unzip -p "$feed/$1.$version.nupkg" "$1.nuspec"; }
entries() { unzip -Z1 "$feed/$1.$version.nupkg"; }
for id in ExGrid ExGrid.MudBlazor; do
  [ -f "$feed/$id.$version.snupkg" ] || fail "$id has no symbol package"
  spec=$(nuspec "$id")
  grep -q '<license type="expression">MIT</license>' <<<"$spec" || fail "$id does not declare MIT"
  grep -q '<readme>README.md</readme>' <<<"$spec" || fail "$id has no readme"
  grep -qE '<repository type="git" url="https://github.com/AkiraMisawa/ex-grid"[^>]* commit="[0-9a-f]{40}"' <<<"$spec" \
    || fail "$id does not name its repository and commit"
  files=$(entries "$id")
  for f in README.md "lib/net8.0/$id.dll" "lib/net8.0/$id.xml"; do
    grep -qxF "$f" <<<"$files" || fail "$id is missing $f"
  done
done
# The core's one dependency, at the floor ADR-0022 fixes.
grep -q '<dependency id="Microsoft.AspNetCore.Components.Web" version="8.0.0"' <<<"$(nuspec ExGrid)" \
  || fail "ExGrid's dependency on Microsoft.AspNetCore.Components.Web is not 8.0.0"
# The Wrapper takes exactly this core (ADR-0042) and MudBlazor from its floor.
grep -qF "<dependency id=\"ExGrid\" version=\"[$version]\"" <<<"$(nuspec ExGrid.MudBlazor)" \
  || fail "ExGrid.MudBlazor does not depend on exactly ExGrid $version"
grep -q '<dependency id="MudBlazor" version="9.0.0"' <<<"$(nuspec ExGrid.MudBlazor)" \
  || fail "ExGrid.MudBlazor's dependency on MudBlazor is not 9.0.0"

echo "== an application that takes them"
dotnet publish "$here" -c Release -o "$out" --nologo \
  -p:ExGridVersion="$version" -p:RestorePackagesPath="$cache"

# Restored from the packed files, not from anywhere else.
for id in exgrid exgrid.mudblazor; do
  meta="$cache/$id/$version/.nupkg.metadata"
  [ -f "$meta" ] || fail "$id $version was not restored"
  grep -qF "$feed" "$meta" || fail "$id $version came from somewhere other than $feed"
done

# The paths the README tells a Consumer to link, and the module the component imports.
for f in _content/ExGrid/ex-grid.css _content/ExGrid/ex-grid.js _content/ExGrid.MudBlazor/mud-ex-grid.css; do
  [ -f "$out/wwwroot/$f" ] || fail "the published application has no $f"
done
grep -qF '"./_content/ExGrid/ex-grid.js"' "$root/src/ExGrid/Components/ExGrid.razor" \
  || fail "the component no longer imports ./_content/ExGrid/ex-grid.js; update this check with it"

echo "package check: $version passed"
