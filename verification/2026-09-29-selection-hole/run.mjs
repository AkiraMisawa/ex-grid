// Runs every comparison of the hole's cost, interleaving the two sides of each round so drift in
// the machine falls on both, writes every drag to results.json and prints the medians and
// interquartile ranges. Each batch is a fresh headless Chrome (drag-cost.mjs); a batch that times
// out is recorded as such and left out.
//
//     node run.mjs
//
// The hosts, as the README starts them:
//   EXGRID_BEFORE_SERVER  the pre-change build's Server host       (default http://localhost:5313)
//   EXGRID_AFTER_SERVER   the changed build's Server host          (default http://localhost:5316)
//   EXGRID_BEFORE_WASM    the pre-change build's WebAssembly host  (default http://localhost:5314)
//   EXGRID_AFTER_WASM     the changed build's WebAssembly host     (default http://localhost:5315)
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';

const here = new URL('.', import.meta.url).pathname;
const host = {
    beforeServer: process.env.EXGRID_BEFORE_SERVER ?? 'http://localhost:5313',
    afterServer: process.env.EXGRID_AFTER_SERVER ?? 'http://localhost:5316',
    beforeWasm: process.env.EXGRID_BEFORE_WASM ?? 'http://localhost:5314',
    afterWasm: process.env.EXGRID_AFTER_WASM ?? 'http://localhost:5315',
};
const ROUNDS = 4;
const DRAGS = 5;
// Switched off on the changed build by an injected rule: the hole (the tint's clip), and the
// range's isolation (which puts the tint beneath the range's outline).
const NO_HOLE = '.ex-range::before { clip-path: none !important; }';
const FLAT = '.ex-range { isolation: auto !important; } .ex-range::before { z-index: auto !important; }';

const comparisons = [
    { name: 'server, B3: before against after', sides: [['before', host.beforeServer, ''], ['after', host.afterServer, '']], start: 'r2c1' },
    { name: 'server, A3 across the pinned boundary: before against after', sides: [['before', host.beforeServer, ''], ['after', host.afterServer, '']], start: 'r2c0' },
    { name: 'wasm, B3: before against after', sides: [['before', host.beforeWasm, ''], ['after', host.afterWasm, '']], start: 'r2c1' },
    { name: 'wasm, A3 across the pinned boundary: before against after', sides: [['before', host.beforeWasm, ''], ['after', host.afterWasm, '']], start: 'r2c0' },
    { name: 'server, B3: the hole, on against off', sides: [['hole', host.afterServer, ''], ['no hole', host.afterServer, NO_HOLE]], start: 'r2c1' },
    { name: 'wasm, B3: the hole, on against off', sides: [['hole', host.afterWasm, ''], ['no hole', host.afterWasm, NO_HOLE]], start: 'r2c1' },
    { name: 'server, B3: the isolation, on against off', sides: [['isolated', host.afterServer, ''], ['flat', host.afterServer, FLAT]], start: 'r2c1' },
];

const quantile = (values, q) => {
    const sorted = [...values].sort((a, b) => a - b);
    const at = (sorted.length - 1) * q;
    const low = Math.floor(at);
    return Math.round((sorted[low] + (sorted[Math.ceil(at)] - sorted[low]) * (at - low)) * 10) / 10;
};
const summary = (rows) => Object.fromEntries(['task', 'script', 'style', 'layout'].map((k) => {
    const values = rows.map((r) => r[k]);
    return [k, { median: quantile(values, 0.5), q1: quantile(values, 0.25), q3: quantile(values, 0.75) }];
}));

const results = { hosts: host, rounds: ROUNDS, dragsPerBatch: DRAGS, comparisons: [] };
for (const comparison of comparisons) {
    const sides = Object.fromEntries(comparison.sides.map(([label]) => [label, { rows: [], timedOut: 0 }]));
    let chrome = '';
    for (let round = 0; round < ROUNDS; round++) {
        for (const [label, base, inject] of comparison.sides) {
            try {
                const out = execFileSync('node', [`${here}drag-cost.mjs`, base, label, String(DRAGS), comparison.start], {
                    encoding: 'utf8',
                    env: { ...process.env, EXGRID_INJECT: inject },
                    stdio: ['ignore', 'pipe', 'ignore'],
                });
                const batch = JSON.parse(out.split('\n').find((line) => line.startsWith('{')));
                if (batch.problems.length) {
                    console.error(comparison.name, label, batch.problems);
                }
                chrome = batch.chrome;
                sides[label].rows.push(...batch.rows);
            } catch {
                sides[label].timedOut++;
            }
        }
    }
    const entry = { name: comparison.name, start: comparison.start, chrome, sides };
    results.comparisons.push(entry);
    for (const [label, side] of Object.entries(sides)) {
        side.summary = summary(side.rows);
        const t = side.summary.task;
        console.log(`${comparison.name} | ${label}: n=${side.rows.length}${side.timedOut ? ` (${side.timedOut} batch timed out)` : ''}, task median ${t.median} ms, IQR ${t.q1}-${t.q3}, style median ${side.summary.style.median}, script median ${side.summary.script.median}`);
    }
}
fs.writeFileSync(`${here}results.json`, `${JSON.stringify(results, null, 1)}\n`);
