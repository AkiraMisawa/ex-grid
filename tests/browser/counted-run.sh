#!/usr/bin/env bash
# Runs layer 3 with every test repeated, and fails unless every repetition of every test Playwright
# lists for the same arguments ran (ADR-0220). A job that says "three times" or "ten times" is green
# only when it ran that many: a test that never started, a shard cut short, or a repeat that did not
# take turns the job red, as a failing test does. retries stays 0 (ADR-0026), so a repetition that
# fails is a failure, and Playwright's own exit decides that; this adds only the count.
#
#   counted-run.sh <repeat> <playwright test arguments…>
#
# Run from tests/browser. Each test's outcome is in test-results/counted.json, and the counts
# go to the job's summary when GITHUB_STEP_SUMMARY names one.
set -euo pipefail

repeat=$1
shift
if ! [[ "${repeat}" =~ ^[1-9][0-9]*$ ]]; then
    echo "counted-run.sh: the repeat is \"${repeat}\"; it is a count of 1 or more." >&2
    exit 2
fi

# The tests these arguments choose, each once: Playwright's own listing, which applies --project,
# --shard, --grep and the files named exactly as the run does. Playwright exits 1 when it lists no
# test, so its status is kept and read below with the count rather than ending the script here.
listing_status=0
listing=$(npx playwright test "$@" --list --reporter=list 2>&1) || listing_status=$?
listed=$(printf '%s\n' "${listing}" | sed -n 's/^Total: \([0-9][0-9]*\) tests\{0,1\} in .*/\1/p')
if [ -z "${listed}" ] || [ "${listed}" -eq 0 ]; then
    # A spec file's tests may all be the other host's (ADR-0370 section 11): the same arguments with
    # no host left out list them, and this host has nothing to repeat. Listed nowhere, the arguments
    # are wrong.
    anywhere=$( (EXGRID_EVERY_HOST=1 npx playwright test "$@" --list --reporter=list 2>&1 || true) \
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
    echo "counted-run.sh: Playwright listed no tests for: $* (its listing exited ${listing_status})" >&2
    exit 1
fi
if [ "${listing_status}" -ne 0 ]; then
    printf '%s\n' "${listing}" >&2
    echo "counted-run.sh: Playwright listed ${listed} tests for: $*, and exited ${listing_status}" >&2
    exit 1
fi
expected=$((listed * repeat))

report=test-results/counted.json
status=0
PLAYWRIGHT_JSON_OUTPUT_FILE="${report}" npx playwright test "$@" --repeat-each="${repeat}" --reporter=list,json || status=$?

# Every repetition the report holds an outcome for: passed, failed or timed out, and skipped where the
# test skipped itself by decision, which it says in an annotation — it ran, and said so. A repetition the
# run never reached, because it was stopped or ran out of its --global-timeout, has no result, or one
# marked skipped with no such annotation, though the report's stats count it as skipped.
ran=$(node -e '
    const report = JSON.parse(require("node:fs").readFileSync(process.argv[1], "utf8"));
    let ran = 0;
    const walk = (suite) => {
        for (const spec of suite.specs ?? []) {
            for (const test of spec.tests) {
                const decided = test.annotations.some((annotation) => annotation.type === "skip" || annotation.type === "fixme");
                ran += test.results.filter((result) => result.status !== "skipped" || decided).length;
            }
        }
        for (const inner of suite.suites ?? []) walk(inner);
    };
    for (const suite of report.suites ?? []) walk(suite);
    console.log(ran);
' "${report}" 2>/dev/null || echo 0)
# What stopped the run as a whole, such as its --global-timeout: the run is red, and this says why.
stopped=$(node -e '
    const { errors } = JSON.parse(require("node:fs").readFileSync(process.argv[1], "utf8"));
    for (const error of errors ?? []) console.log(error.message.replace(/\x1b\[[0-9;]*m/g, ""));
' "${report}" 2>/dev/null || true)
# Where the run's time went, a line per spec file, the longest first: a shard that outgrows its job's
# time names the file that took it (hunt.yml, 2026-11-10).
minutes=$(node -e '
    const report = JSON.parse(require("node:fs").readFileSync(process.argv[1], "utf8"));
    const spent = new Map();
    const walk = (suite) => {
        for (const spec of suite.specs ?? []) {
            for (const test of spec.tests) {
                for (const result of test.results) spent.set(spec.file, (spent.get(spec.file) ?? 0) + result.duration);
            }
        }
        for (const inner of suite.suites ?? []) walk(inner);
    };
    for (const suite of report.suites ?? []) walk(suite);
    for (const [file, ms] of [...spent].sort(([, a], [, b]) => b - a)) console.log(`${file}: ${(ms / 60000).toFixed(1)} min`);
' "${report}" 2>/dev/null || echo 'no report to read the time from')
failed=$(node -e '
    const { stats } = JSON.parse(require("node:fs").readFileSync(process.argv[1], "utf8"));
    console.log(stats.unexpected);
' "${report}" 2>/dev/null || echo '?')

line="${listed} tests listed, repeated ${repeat}×: ${expected} expected, ${ran} ran, ${failed} failed."
echo "counted-run.sh: ${line}"
if [ -n "${stopped}" ]; then
    line="${line} The run did not finish: ${stopped//$'\n'/; }."
    echo "counted-run.sh: the run did not finish: ${stopped}" >&2
fi
printf 'counted-run.sh: the time each spec file took:\n%s\n' "${minutes}"
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
    echo "${line} (\`$*\`)" >> "${GITHUB_STEP_SUMMARY}"
    printf '\n%s\n' "$(printf '%s\n' "${minutes}" | sed 's/^/- /')" >> "${GITHUB_STEP_SUMMARY}"
fi

if [ -n "${stopped}" ] && [ "${status}" -eq 0 ]; then
    exit 1
fi
if [ "${ran}" != "${expected}" ]; then
    echo "counted-run.sh: ${expected} repetitions were asked for and ${ran} ran." >&2
    exit 1
fi
exit "${status}"
