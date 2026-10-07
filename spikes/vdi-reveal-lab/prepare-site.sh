#!/usr/bin/env bash
# Turns a Docs publish into what GitHub Pages serves under /<repo>/, as docs.yml does.
# Usage: prepare-site.sh <publish dir> <repo name> <out dir>
set -euo pipefail
publish=$1; repo=$2; out=$3
rm -rf "$out"; mkdir -p "$out"
cp -r "$publish/wwwroot/." "$out/"
sed -i "s#<base href=\"/\" />#<base href=\"/$repo/\" />#" "$out/index.html"
grep -q "<base href=\"/$repo/\" />" "$out/index.html"
rm -f "$out/index.html.br" "$out/index.html.gz"
cp "$out/index.html" "$out/404.html"
touch "$out/.nojekyll"
du -sh "$out"
