// The tests shard <k> of <n> takes, for a run that repeats every test (hunt.yml, ADR-0220 §4).
//
//   EXGRID_SHARD=<k>/<n> node shard-files.mjs [files…]
//
// Prints `--test-list=<path>` for counted-run.sh and Playwright, having written that shard's tests to <path>,
// or nothing when the shard takes none. What it chose goes to stderr, a line per file. (It once printed whole spec
// files, and keeps its name.)
//
// Playwright's own --shard cuts the list of tests after --repeat-each has multiplied it, so a shard
// ran some repetitions of a file and another shard the rest — or, as the first run of hunt.yml showed,
// a shard listed for the tests once took a third of their repetitions and the others none. Here a
// shard takes every repetition of each test it takes, and a file's tests stay together in one shard, so
// a file's booted app is booted once (ADR-0056) and counted-run.sh can count a shard against what
// Playwright lists for it.
//
// A test tagged `@spread` (SPREAD in tags.mjs) is the exception: it is far longer than a shard's
// share of tests allows for, and its file's tests go round the shards one at a time instead, each shard
// booting that file for its own few. The differential test is one: it runs its scenario twice behind a
// 150 ms round trip, 15 to 30 seconds a seed, and every product has a test for every fixed seed. Kept
// whole, it took 33 tests and close to two hours of a Server shard's ten passes, and the shard, judged the
// lightest, was given more of the other tests than any (Hunt run 42755971995, 2026-11-10, ADR-0451).
//
// The tests are Playwright's own listing for the host EXGRID_HOSTING names, which leaves out the other
// host's tests (ADR-0370 section 11) and counts every test a loop draws: a count of `test(` lines read
// from the source counted the differential's 30 tests as one, which is how its shard came to be judged
// the lightest. Files go to the shard with the fewest tests so far, the files with the most tests
// first, after the spread tests have gone round; the same listing gives the same shards on every
// runner. The listing must be taken under the same environment as the run, EXGRID_DIFF_SEED included,
// or a test drawn by the run would be in no shard's list: the job sets them once for both.
//
// The shard is handed on as a list of tests, never as file names: Playwright reads a file argument as a
// pattern, so `pivot.spec.mjs` also ran `script-pivot.spec.mjs` and `measure-pivot.spec.mjs`, and thirty
// tests a pass on the Server host ran in two shards each.
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { SPREAD } from './tags.mjs';

const shard = /^([1-9]\d*)\/([1-9]\d*)$/.exec(process.env.EXGRID_SHARD ?? '');
const [k, n] = shard === null ? [NaN, NaN] : [Number(shard[1]), Number(shard[2])];
if (!(k >= 1 && k <= n)) {
    console.error(`EXGRID_SHARD is "${process.env.EXGRID_SHARD ?? ''}": it is <k>/<n>, with 1 ≤ k ≤ n. node shard-files.mjs [files…]`);
    process.exit(2);
}
const here = path.dirname(fileURLToPath(import.meta.url));
const named = process.argv.slice(2);

// Playwright's listing of these arguments, as JSON. It is the same whichever browser runs it; chrome-150 has a
// --grep of its own and is not the hunt's.
const list = (args) => {
    // --pass-with-no-tests: files named whose tests are all the other host's list none here, and the shard takes none.
    const listing = JSON.parse(execFileSync('npx', ['playwright', 'test', '--list', '--reporter=json', '--project=chrome', '--pass-with-no-tests', ...args], {
        cwd: here,
        encoding: 'utf8',
        maxBuffer: 256 * 1024 * 1024,
        stdio: ['ignore', 'pipe', 'inherit'],
    }));
    if (listing.errors?.length > 0) {
        console.error(listing.errors.map((error) => error.message).join('\n'));
        process.exit(1);
    }
    return listing;
};
const listing = list(named);

// Each test as --test-list names it: its file, the titles of the describes around it, and its own title. A
// describe with no title (one that only sets options) is not part of the name, as Playwright's own listing shows it.
const testsOf = (listing) => {
    const tests = [];
    const walk = (suite, titles) => {
        for (const spec of suite.specs ?? []) {
            tests.push({ file: spec.file, name: [spec.file, ...titles, spec.title].join(' › '), spread: spec.tags.includes(SPREAD.replace(/^@/, '')) });
        }
        for (const inner of suite.suites ?? []) walk(inner, inner.title === '' ? titles : [...titles, inner.title]);
    };
    for (const file of listing.suites ?? []) walk(file, []);
    return tests;
};
const tests = testsOf(listing);

const shards = Array.from({ length: n }, () => ({ tests: [], files: new Map() }));
const take = (to, test) => {
    to.tests.push(test.name);
    to.files.set(test.file, (to.files.get(test.file) ?? 0) + 1);
};
const order = (a, b) => (a < b ? -1 : a > b ? 1 : 0);
// Every test of a file that holds a spread test goes round the shards, one to each in turn, in the order of
// their names; each shard boots that file for its own few. The files kept together then go, largest first, to
// whichever shard holds the fewest tests so far.
const spreadFiles = new Set(tests.filter((test) => test.spread).map((test) => test.file));
tests.filter((test) => spreadFiles.has(test.file))
    .sort((a, b) => order(a.name, b.name))
    .forEach((test, at) => take(shards[at % n], test));
const byFile = Map.groupBy(tests.filter((test) => !spreadFiles.has(test.file)), (test) => test.file);
for (const [, kept] of [...byFile].sort(([a, x], [b, y]) => y.length - x.length || order(a, b))) {
    const lightest = shards.reduce((best, each) => (each.tests.length < best.tests.length ? each : best));
    for (const test of kept) take(lightest, test);
}

const assigned = shards.reduce((sum, each) => sum + each.tests.length, 0);
if (assigned !== tests.length || new Set(shards.flatMap((each) => each.tests)).size !== tests.length) {
    console.error(`shard-files.mjs: ${tests.length} tests listed, ${assigned} given to a shard: every test is in exactly one.`);
    process.exit(1);
}

const mine = shards[k - 1];
console.error(`Shard ${k}/${n}: ${mine.tests.length} of ${tests.length} tests.`);
for (const [file, count] of [...mine.files].sort(([a], [b]) => order(a, b))) {
    console.error(`  ${file}: ${count}${spreadFiles.has(file) ? ` of ${tests.filter((test) => test.file === file).length}, spread` : ''}`);
}
if (mine.tests.length > 0) {
    // A directory of its own, so two checkouts on one machine never read each other's list.
    const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), 'exgrid-shard-')), `${k}-of-${n}.txt`);
    fs.writeFileSync(file, `${mine.tests.join('\n')}\n`);
    // A line Playwright does not match is a test no shard runs, and counted-run.sh, which counts what Playwright
    // lists for the same list, would not see it go: the list is read back before it is handed on.
    const matched = testsOf(list([`--test-list=${file}`])).map((test) => test.name);
    if (matched.length !== mine.tests.length || !mine.tests.every((name) => matched.includes(name))) {
        console.error(`shard-files.mjs: shard ${k}/${n} lists ${mine.tests.length} tests in ${file}, and Playwright matches ${matched.length} tests from it.`);
        process.exit(1);
    }
    console.log(`--test-list=${file}`);
}
