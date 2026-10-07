#!/bin/bash
cd /Users/akira338/github/ex-grid/.claude/worktrees/ld-measure-after
O=/private/tmp/claude-501/-Users-akira338-github-ex-grid/046d34fd-22d4-4f2b-b79a-7395332c483e/scratchpad/ld13
F='zoxide\|_ZO_\|ajeetdsouza\|Please ensure\|If the issue\|uncommitted\|nix-managed'
while read -r line; do
  [ -z "$line" ] && continue
  DOTNET_TieredCompilation=0 nix develop -c dotnet spikes/live-update/Costs/bin/Release/net10.0/Costs.dll $line 2>&1 | grep -v "$F" | tail -${TAILN:-14}
done
