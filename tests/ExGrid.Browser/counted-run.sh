#!/usr/bin/env bash
# Runs layer 3 with every test repeated, and fails unless every repetition of every test Playwright
# lists for the same arguments ran (ADR-0220). A job that says "three times" or "ten times" is green
# only when it ran that many: a test that never started, a shard cut short, or a repeat that did not
# take turns the job red, as a failing test does. retries stays 0 (ADR-0026), so a repetition that
# fails is a failure, and Playwright's own exit decides that; this adds only the count.
#
#   counted-run.sh <repeat> <playwright test arguments…>
#
# Run from tests/ExGrid.Browser. Each test's outcome is in test-results/counted.json, and the counts
# go to the job's summary when GITHUB_STEP_SUMMARY names one.
set -euo pipefail

repeat=$1
shift
if ! [[ "${repeat}" =~ ^[1-9][0-9]*$ ]]; then
    echo "counted-run.sh: the repeat is \"${repeat}\"; it is a count of 1 or more." >&2
    exit 2
fi

# The tests these arguments choose, each once: Playwright's own listing, which applies --project,
# --shard, --grep and the files named exactly as the run does.
listing=$(npx playwright test "$@" --list --reporter=list)
listed=$(printf '%s\n' "${listing}" | sed -n 's/^Total: \([0-9][0-9]*\) tests\{0,1\} in .*/\1/p')
if [ -z "${listed}" ] || [ "${listed}" -eq 0 ]; then
    # A spec file's tests may all be the other host's (ADR-0370 section 11): the same arguments with
    # no host left out list them, and this host has nothing to repeat. Listed nowhere, the arguments
    # are wrong.
    anywhere=$(EXGRID_EVERY_HOST=1 npx playwright test "$@" --list --reporter=list \
        | sed -n 's/^Total: \([0-9][0-9]*\) tests\{0,1\} in .*/\1/p')
    if [ -n "${anywhere}" ] && [ "${anywhere}" -gt 0 ]; then
        line="no test of this host's in: $* (${anywhere} are the other host's)."
        echo "counted-run.sh: ${line}"
        if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
            echo "${line}" >> "${GITHUB_STEP_SUMMARY}"
        fi
        exit 0
    fi
    printf '%s\n' "${listing}" >&2
    echo "counted-run.sh: Playwright listed no tests for: $*" >&2
    exit 1
fi
expected=$((listed * repeat))

report=test-results/counted.json
status=0
PLAYWRIGHT_JSON_OUTPUT_FILE="${report}" npx playwright test "$@" --repeat-each="${repeat}" --reporter=list,json || status=$?

# Every repetition the report holds an outcome for: passed, failed, flaky (never, with no retries)
# and skipped — a test that skips itself by decision ran, and said so.
ran=$(node -e '
    const { stats } = JSON.parse(require("node:fs").readFileSync(process.argv[1], "utf8"));
    console.log(stats.expected + stats.unexpected + stats.flaky + stats.skipped);
' "${report}" 2>/dev/null || echo 0)
failed=$(node -e '
    const { stats } = JSON.parse(require("node:fs").readFileSync(process.argv[1], "utf8"));
    console.log(stats.unexpected);
' "${report}" 2>/dev/null || echo '?')

line="${listed} tests listed, repeated ${repeat}×: ${expected} expected, ${ran} ran, ${failed} failed."
echo "counted-run.sh: ${line}"
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
    echo "${line} (\`$*\`)" >> "${GITHUB_STEP_SUMMARY}"
fi

if [ "${ran}" != "${expected}" ]; then
    echo "counted-run.sh: ${expected} repetitions were asked for and ${ran} ran." >&2
    exit 1
fi
exit "${status}"
