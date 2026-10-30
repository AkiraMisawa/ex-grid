// The spec files shard <k> of <n> takes, for a run that repeats every test (hunt.yml, ADR-0220 §4).
//
//   node shard-files.mjs <k> <n> [files…]
//
// Playwright's own --shard cuts the list of tests after --repeat-each has multiplied it, so a shard
// ran some repetitions of a file and another shard the rest — or, as the first run of hunt.yml showed,
// a shard listed for the tests once took a third of their repetitions and the others none. Here a
// shard is whole files, every repetition of each, so a file's booted app is never split (ADR-0056)
// and counted-run.sh can count a shard against what Playwright lists for its files.
//
// Files go to the shard with the fewest tests so far, the files with the most tests first, so the
// shards come out about even. A test is counted from the source (each `test(` or `test.<modifier>(`
// that starts a line), which is close enough to balance on and needs no browser, and a test whose
// line names only the other host's tag is not counted, since EXGRID_HOSTING's run leaves it out
// (ADR-0370 section 11). The same files give the same shards on every runner. With no files named,
// every *.spec.mjs here.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const [k, n] = process.argv.slice(2, 4).map(Number);
if (!(Number.isInteger(n) && n >= 1 && Number.isInteger(k) && k >= 1 && k <= n)) {
    console.error('node shard-files.mjs <k> <n> [files…], with 1 ≤ k ≤ n');
    process.exit(2);
}
const here = path.dirname(fileURLToPath(import.meta.url));
const named = process.argv.slice(4);
const files = (named.length > 0 ? named : fs.readdirSync(here).filter((f) => f.endsWith('.spec.mjs'))).sort();
const [own, other] = process.env.EXGRID_HOSTING === 'react' ? ['@behaviour', '@blazor'] : ['@blazor', '@behaviour'];
const weight = (file) => (fs.readFileSync(path.join(here, file), 'utf8').match(/^\s*test(?:\.\w+)*\(.*$/gm) ?? [])
    .filter((line) => !(line.includes(other) && !line.includes(own) && !line.includes('Host(')))
    .length;

const shards = Array.from({ length: n }, () => ({ tests: 0, files: [] }));
const weighed = files.map((file) => ({ file, tests: weight(file) }))
    .sort((a, b) => b.tests - a.tests || (a.file < b.file ? -1 : 1));
for (const { file, tests } of weighed) {
    const lightest = shards.reduce((best, shard) => (shard.tests < best.tests ? shard : best));
    lightest.tests += tests;
    lightest.files.push(file);
}
console.log(shards[k - 1].files.sort().join(' '));
