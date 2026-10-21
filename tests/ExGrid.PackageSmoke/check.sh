#!/usr/bin/env bash
# Packs the family's thirteen packages — ExGrid and ExGrid.MudBlazor; ExSheet.Engine, ExSheet and
# ExSheet.MudBlazor; ExGrid.Data and ExGrid.Data.Arrow; ExPivot.Engine, ExPivot and
# ExPivot.MudBlazor; ExChart.Engine, ExChart and ExChart.MudBlazor (ADR-0283) — reads back what
# each declares, and builds and publishes an application that takes them from the packed files
# alone (ADR-0042; the family ships at one version since its note of 2026-10-03). A Snapshot is written to an Arrow stream and read back through the packed data
# packages, and the check fails if it comes back different (DA-16). A trimmed Consumer also
# round-trips report Windows, Window Changes and versioned operations with reflection disabled (ADR-0152).
#
# ExGrid.Data.Xlsx (ADR-0330) is packed and checked the same way, and a workbook is written and read
# back through it, but it goes to .feed-unreleased, not to .feed: the release publishes the packages
# ADR-0042 names, and that list does not name it yet.
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
unreleased="$here/.feed-unreleased"
cache="$here/.nuget-packages"
out="$here/.publish"

fail() { echo "package check: $*" >&2; exit 1; }

rm -rf "$feed" "$unreleased" "$cache" "$out" "$here/bin" "$here/obj" "$here/RoundTrip/bin" "$here/RoundTrip/obj"
mkdir -p "$feed" "$unreleased"

echo "== pack $version"
packages="ExGrid ExGrid.MudBlazor ExSheet.Engine ExSheet ExSheet.MudBlazor ExGrid.Data ExGrid.Data.Arrow ExPivot.Engine ExPivot ExPivot.MudBlazor ExChart.Engine ExChart ExChart.MudBlazor"
for project in $packages; do
  dotnet pack "$root/src/$project" -c Release -o "$feed" -p:Version="$version" --nologo
done
held="ExGrid.Data.Xlsx"
for project in $held; do
  dotnet pack "$root/src/$project" -c Release -o "$unreleased" -p:Version="$version" --nologo
done

echo "== what the packages declare"
# A package held back from the release is read from where it was packed.
nupkg() { if [ -f "$feed/$1.$version.nupkg" ]; then echo "$feed/$1.$version.nupkg"; else echo "$unreleased/$1.$version.nupkg"; fi; }
nuspec() { unzip -p "$(nupkg "$1")" "$1.nuspec"; }
# Read into a string before a grep -q, never piped to one: grep -q exits at its first match, unzip
# then dies of SIGPIPE, and under pipefail the pipeline fails — a file that is there reported missing,
# or, inside an if, a forbidden one passed over.
entries() { unzip -Z1 "$(nupkg "$1")"; }
for id in $packages $held; do
  [ -f "$(nupkg "$id" | sed 's/\.nupkg$/.snupkg/')" ] || fail "$id has no symbol package"
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
# The core's dependencies: Blazor at the floor ADR-0022 fixes, and exactly the ExGrid.Data it was
# built with, for the Aggregations' one definition (ADR-0130, DA-1).
coredeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExGrid)" | sort)
[ "$coredeps" = "$(printf '%s\n' '<dependency id="Microsoft.AspNetCore.Components.Web" version="10.0.0"' "<dependency id=\"ExGrid.Data\" version=\"[$version]\"" | sort)" ] \
  || fail "ExGrid's dependencies are not exactly Microsoft.AspNetCore.Components.Web 10.0.0 and ExGrid.Data $version: $coredeps"
# The Wrapper takes exactly this core (ADR-0042) and MudBlazor 9, from its floor, 9.6.0, and not
# 10: a Consumer on an earlier 9.x or on MudBlazor 10 is told at restore (ADR-0205). Each Wrapper
# declares that one range.
mudblazor='<dependency id="MudBlazor" version="[9.6.0, 10.0.0)"'
# Nothing else: what PRE-3 names for it, read as the other packages' lists are.
gridmuddeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExGrid.MudBlazor)" | sort)
[ "$gridmuddeps" = "$(printf '%s\n' "<dependency id=\"ExGrid\" version=\"[$version]\"" "$mudblazor" | sort)" ] \
  || fail "ExGrid.MudBlazor's dependencies are not exactly ExGrid $version and MudBlazor [9.6.0, 10.0.0): $gridmuddeps"
# The engine depends on nothing at all: a server computes a Sheet with it alone (ADR-0047, SH-1).
if grep -q '<dependency ' <<<"$(nuspec ExSheet.Engine)"; then fail "ExSheet.Engine declares a dependency"; fi
# ExSheet depends on exactly the core and the engine it was built with, and on nothing else: the
# one direction there is (ADR-0046, SH-1). Anything more comes to it through ExGrid.
sheetdeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExSheet)" | sort)
[ "$sheetdeps" = "$(printf '%s\n' "<dependency id=\"ExGrid\" version=\"[$version]\"" "<dependency id=\"ExSheet.Engine\" version=\"[$version]\"" | sort)" ] \
  || fail "ExSheet's dependencies are not exactly ExGrid $version and ExSheet.Engine $version: $sheetdeps"

# ExSheet.MudBlazor depends on exactly the ExSheet and the Wrapper it was built with, and on
# MudBlazor in the Wrapper's range; nothing else (SH-47, ADR-0205). And it ships no script of its own: its
# one static asset is its stylesheet (ADR-0021).
mudsheetdeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExSheet.MudBlazor)" | sort)
[ "$mudsheetdeps" = "$(printf '%s\n' "<dependency id=\"ExGrid.MudBlazor\" version=\"[$version]\"" "<dependency id=\"ExSheet\" version=\"[$version]\"" "$mudblazor" | sort)" ] \
  || fail "ExSheet.MudBlazor's dependencies are not exactly ExGrid.MudBlazor $version, ExSheet $version and MudBlazor [9.6.0, 10.0.0): $mudsheetdeps"
if grep -qiE '\.(js|mjs|cjs)$' <<<"$(entries ExSheet.MudBlazor)"; then fail "ExSheet.MudBlazor ships a script"; fi
grep -qxF 'staticwebassets/mud-ex-sheet.min.css' <<<"$(entries ExSheet.MudBlazor)" || fail "ExSheet.MudBlazor is missing staticwebassets/mud-ex-sheet.min.css"
# What ships is minified, with its source map, and the sources stay home (ADR-0123): every script
# and stylesheet a package serves is a .min file, and none packs its Assets folder.
for p in ExGrid ExGrid.MudBlazor ExSheet ExSheet.MudBlazor ExPivot ExPivot.MudBlazor ExChart ExChart.MudBlazor; do
  unminified=$(entries "$p" | grep -E '^staticwebassets/.*\.(css|js)$' | grep -vE '\.min\.(css|js)$' || true)
  [ -z "$unminified" ] || fail "$p ships an asset that is not minified: $unminified"
  grep -qE '^staticwebassets/.*\.min\.css\.map$' <<<"$(entries "$p")" || fail "$p ships no source map for its stylesheet"
  if grep -qiE '(^|/)Assets/' <<<"$(entries "$p")"; then fail "$p packs its Assets sources"; fi
done
grep -qxF 'staticwebassets/ex-grid.min.js.map' <<<"$(entries ExGrid)" || fail "ExGrid ships no source map for its script"
grep -qxF 'staticwebassets/ex-chart.min.js.map' <<<"$(entries ExChart)" || fail "ExChart ships no source map for its script"

# The Wrapper still takes no ExSheet package: the direction is one-way (SH-47).
if grep -q '<dependency id="ExSheet' <<<"$(nuspec ExGrid.MudBlazor)"; then fail "ExGrid.MudBlazor depends on an ExSheet package"; fi

# The family's data package depends on nothing at all, not even on the grid (ADR-0064, DA-1).
if grep -q '<dependency ' <<<"$(nuspec ExGrid.Data)"; then fail "ExGrid.Data declares a dependency"; fi
# Its Arrow package depends on exactly the ExGrid.Data it was built with, and on Apache.Arrow
# within the major version it was built and tested with, and on nothing else (ADR-0065, DA-1).
arrowdeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExGrid.Data.Arrow)" | sort)
[ "$arrowdeps" = "$(printf '%s\n' "<dependency id=\"Apache.Arrow\" version=\"[23.0.0, 24.0.0)\"" "<dependency id=\"ExGrid.Data\" version=\"[$version]\"" | sort)" ] \
  || fail "ExGrid.Data.Arrow's dependencies are not exactly ExGrid.Data $version and Apache.Arrow [23.0.0, 24.0.0): $arrowdeps"
# Its xlsx package depends on exactly the ExGrid.Data it was built with, and on nothing else: the
# file layer is the platform's zip and XML (ADR-0330).
xlsxdeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExGrid.Data.Xlsx)" | sort)
[ "$xlsxdeps" = "<dependency id=\"ExGrid.Data\" version=\"[$version]\"" ] \
  || fail "ExGrid.Data.Xlsx's dependencies are not exactly ExGrid.Data $version: $xlsxdeps"
[ "$(ls "$unreleased" | sort)" = "$(for id in $held; do printf '%s\n' "$id.$version.nupkg" "$id.$version.snupkg"; done | sort)" ] \
  || fail "$unreleased does not hold exactly the packages held back from the release: $(ls "$unreleased" | tr '\n' ' ')"
# ExPivot's engine depends on exactly the ExGrid.Data it was built with, and on nothing else: it
# aggregates a Snapshot (ADR-0064, PV-1).
enginedeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExPivot.Engine)" | sort)
[ "$enginedeps" = "<dependency id=\"ExGrid.Data\" version=\"[$version]\"" ] \
  || fail "ExPivot.Engine's dependencies are not exactly ExGrid.Data $version: $enginedeps"
# Nothing else the family packs references a data package directly (DA-1), and nothing but the
# Arrow and xlsx packages themselves reference ExGrid.Data.Arrow or ExGrid.Data.Xlsx.
if grep -qE '<dependency id="ExGrid\.Data\.(Arrow|Xlsx)"' <<<"$(nuspec ExGrid)"; then fail "ExGrid references ExGrid.Data.Arrow or ExGrid.Data.Xlsx"; fi
if grep -q '<dependency id="ExGrid.Data.Arrow"' <<<"$(nuspec ExGrid.Data.Xlsx)"; then fail "ExGrid.Data.Xlsx references ExGrid.Data.Arrow"; fi
if grep -q '<dependency id="ExGrid.Data.Xlsx"' <<<"$(nuspec ExGrid.Data.Arrow)"; then fail "ExGrid.Data.Arrow references ExGrid.Data.Xlsx"; fi
for id in ExGrid.MudBlazor ExSheet.Engine ExSheet ExSheet.MudBlazor ExPivot ExPivot.MudBlazor ExChart.Engine ExChart ExChart.MudBlazor; do
  if grep -qE '<dependency id="ExGrid\.Data(\.Arrow|\.Xlsx)?"' <<<"$(nuspec "$id")"; then fail "$id references a data package"; fi
done
# ExPivot depends on exactly the core and the engine it was built with, and on nothing else.
pivotdeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExPivot)" | sort)
[ "$pivotdeps" = "$(printf '%s\n' "<dependency id=\"ExGrid\" version=\"[$version]\"" "<dependency id=\"ExPivot.Engine\" version=\"[$version]\"" | sort)" ] \
  || fail "ExPivot's dependencies are not exactly ExGrid $version and ExPivot.Engine $version: $pivotdeps"
# Its Wrapper depends on exactly ExPivot and the grid's Wrapper it was built with, and on MudBlazor
# in the range the grid's Wrapper takes (ADR-0062, ADR-0205).
muddeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExPivot.MudBlazor)" | sort)
[ "$muddeps" = "$(printf '%s\n' "<dependency id=\"ExGrid.MudBlazor\" version=\"[$version]\"" "<dependency id=\"ExPivot\" version=\"[$version]\"" "$mudblazor" | sort)" ] \
  || fail "ExPivot.MudBlazor's dependencies are not exactly ExPivot and ExGrid.MudBlazor $version and MudBlazor [9.6.0, 10.0.0): $muddeps"

# ExChart's engine depends on nothing at all, ExGrid included: a chart without a grid (ADR-0250, CH-13).
if grep -q '<dependency ' <<<"$(nuspec ExChart.Engine)"; then fail "ExChart.Engine declares a dependency"; fi
# ExChart depends on exactly the core and the engine it was built with, and on nothing else (CH-13).
chartdeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExChart)" | sort)
[ "$chartdeps" = "$(printf '%s\n' "<dependency id=\"ExChart.Engine\" version=\"[$version]\"" "<dependency id=\"ExGrid\" version=\"[$version]\"" | sort)" ] \
  || fail "ExChart's dependencies are not exactly ExGrid $version and ExChart.Engine $version: $chartdeps"
# Its Wrapper depends on exactly ExChart and the grid's Wrapper it was built with, and on MudBlazor
# in the range the grid's Wrapper takes (ADR-0281, ADR-0205); it ships no script of its own.
mudchartdeps=$(grep -o '<dependency id="[^"]*" version="[^"]*"' <<<"$(nuspec ExChart.MudBlazor)" | sort)
[ "$mudchartdeps" = "$(printf '%s\n' "<dependency id=\"ExChart\" version=\"[$version]\"" "<dependency id=\"ExGrid.MudBlazor\" version=\"[$version]\"" "$mudblazor" | sort)" ] \
  || fail "ExChart.MudBlazor's dependencies are not exactly ExChart and ExGrid.MudBlazor $version and MudBlazor [9.6.0, 10.0.0): $mudchartdeps"
if grep -qiE '\.(js|mjs|cjs)$' <<<"$(entries ExChart.MudBlazor)"; then fail "ExChart.MudBlazor ships a script"; fi

# The release publishes .feed as it is, so it holds the thirteen packages and their symbol packages, and
# nothing else (ADR-0042).
expected=$(for id in $packages; do printf '%s\n' "$id.$version.nupkg" "$id.$version.snupkg"; done | sort)
[ "$(ls "$feed" | sort)" = "$expected" ] || fail "the release feed $feed does not hold exactly the thirteen packages: $(ls "$feed" | tr '\n' ' ')"

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
for id in $held; do
  meta="$cache/${id,,}/$version/.nupkg.metadata"
  [ -f "$meta" ] || fail "$id $version was not restored"
  grep -qF "$unreleased" "$meta" || fail "$id $version came from somewhere other than $unreleased"
done

# Each package asks for the files it was built with (ADR-0123): its stylesheet component, and the
# grid's import, ask for the shipped file at its path with the first twelve hex digits of its SHA-256
# as ?v=, and the grid compares the script's stamp, its source's SHA-256, with the one it was built
# with. So each packed assembly must carry the version of the file packed beside it, and the
# stamp that file names; and the application must serve that same file at that path. The
# application links nothing itself: its eight stylesheets are the packages' components.
if grep -q '_content/Ex' "$out/wwwroot/index.html"; then fail "the application's index.html still links a package's file itself"; fi
utf16() { LC_ALL=C tr -d '\000'; }
for pair in ExGrid:ex-grid.min.css ExGrid:ex-grid.min.js ExGrid.MudBlazor:mud-ex-grid.min.css \
            ExSheet:ex-sheet.min.css ExSheet.MudBlazor:mud-ex-sheet.min.css \
            ExPivot:ex-pivot.min.css ExPivot.MudBlazor:mud-ex-pivot.min.css \
            ExChart:ex-chart.min.css ExChart:ex-chart.min.js ExChart.MudBlazor:mud-ex-chart.min.css; do
  id=${pair%%:*}; f=${pair#*:}
  packed=$(unzip -p "$feed/$id.$version.nupkg" "staticwebassets/$f" | sha256sum | cut -c1-64)
  served="$out/wwwroot/_content/$id/$f"
  [ -f "$served" ] || fail "the published application has no _content/$id/$f"
  [ "$(sha256sum < "$served" | cut -c1-64)" = "$packed" ] || fail "_content/$id/$f is not the file $id packed"
  shipped=$(unzip -p "$feed/$id.$version.nupkg" "staticwebassets/$f")
  stamp=$(grep -m1 -oE 'sha256:[0-9a-f]{64}' <<<"${shipped:0:300}" | cut -c8- || true)
  [ -n "$stamp" ] || fail "$id's $f names no source hash"
  assembly=$(unzip -p "$feed/$id.$version.nupkg" "lib/net10.0/$id.dll" | utf16)
  grep -qF "${packed:0:12}" <<<"$assembly" || fail "$id.dll does not carry the version of the $f it packs (${packed:0:12}): run tools/assets/minify.mjs"
  grep -qF "$stamp" <<<"$assembly" || fail "$id.dll does not carry the stamp of the $f it packs ($stamp)"
done

echo "== trimmed packed Consumer: Arrow, xlsx and report protocol round trips"
# RoundTrip runs what the application above compiled for a browser: it writes a Snapshot of every
# kind, with Blanks, captions, a Record Key and a version, and reads it back (ADR-0065, DA-16).
dotnet publish "$here/RoundTrip" -c Release --nologo -o "$out/roundtrip" --self-contained true -p:PublishTrimmed=true \
  -p:ExGridVersion="$version" -p:RestorePackagesPath="$cache"
for id in exgrid.data exgrid.data.arrow expivot.engine; do
  grep -qF "$feed" "$cache/$id/$version/.nupkg.metadata" || fail "the round trip took $id $version from somewhere other than $feed"
done
grep -qF "$unreleased" "$cache/exgrid.data.xlsx/$version/.nupkg.metadata" || fail "the round trip took exgrid.data.xlsx $version from somewhere other than $unreleased"
dotnet "$out/roundtrip/PackageSmoke.RoundTrip.dll" \
  || fail "a packed data, workbook or report protocol round trip changed its result"

echo "package check: $version passed"
