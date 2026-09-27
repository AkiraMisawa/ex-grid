#!/usr/bin/env bash
# git bisect run's command: the scrollbar group (VZ-14 and the three "never behind a scrollbar"
# tests) decides good or bad; a build failure or a missing test skips the commit.
SP=/tmp/claude-1000/-home-akira-src-hobby-ex-grid/3f3091f7-2a61-49e5-8414-79b7a126faa0/scratchpad
out=$("$SP/bisect-step.sh" 2>&1 | grep -vE "zoxide|^Please|^If the issue|^https://github|^Disable this|^$")
echo "$out" >> "$SP/bisect-steps.txt"
v=$(echo "$out" | grep '^verdict')
echo "$v"
case "$v" in
  *skip*|*scrollbar=absent*) exit 125 ;;
  *scrollbar=bad*) exit 1 ;;
  *scrollbar=good*) exit 0 ;;
esac
exit 125
