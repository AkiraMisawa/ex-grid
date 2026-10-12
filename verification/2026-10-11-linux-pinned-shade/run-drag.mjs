// The drag cost before and after ADR-0206, with ../2026-09-29-selection-hole/drag-cost.mjs as
// extended for ?pin= and ?chrome=. Interleaves the two sides of each comparison round by round, so
// drift in the machine falls on both; each batch is a fresh browser. Writes every drag to
// raw/drag-cost.json and prints the medians and interquartile ranges.
//
//     node run-drag.mjs
//
// The hosts, Release publishes started as README.md says:
//   EXGRID_BEFORE_SERVER  the base's Server host        (default http://localhost:7470)
//   EXGRID_AFTER_SERVER   the change's Server host      (default http://localhost:6470)
//   EXGRID_BEFORE_WASM    the base's WebAssembly host   (default http://localhost:8471)
//   EXGRID_AFTER_WASM     the change's WebAssembly host (default http://localhost:5471)
// EXGRID_EXECUTABLE names the Chromium to launch (drag-cost.mjs), EXGRID_ROUNDS the rounds (4).
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';

const here = new URL('.', import.meta.url).pathname;
const dragCost = new URL('../2026-09-29-selection-hole/drag-cost.mjs', import.meta.url).pathname;
const host = {
    beforeServer: process.env.EXGRID_BEFORE_SERVER ?? 'http://localhost:7470',
    afterServer: process.env.EXGRID_AFTER_SERVER ?? 'http://localhost:6470',
    beforeWasm: process.env.EXGRID_BEFORE_WASM ?? 'http://localhost:8471',
    afterWasm: process.env.EXGRID_AFTER_WASM ?? 'http://localhost:5471',
};
const ROUNDS = Number(process.env.EXGRID_ROUNDS ?? 4);
const DRAGS = 5;

// Each comparison: the host pair, the start cell, how many columns are pinned, and the Chrome.
// A2 (r1c0) with column A pinned crosses the boundary on the first move right; A2 with three
// pinned crosses it on the fourth; B2 (r1c1) with none pinned is the case that paints no pinned
// layer at all. Row 2, not the 2026-09-29 record's row 3: drag-cost.mjs puts the Sheet at the
// window's top, where its 445 px Viewport ends a row short of a drag from row 3, and a move into
// the edge band scrolls the Sheet on past the range the wait looks for.
const comparisons = [];
for (const [hostName, before, after] of [['server', host.beforeServer, host.afterServer], ['wasm', host.beforeWasm, host.afterWasm]]) {
    for (const [start, pin, chrome] of [['r1c0', '1', ''], ['r1c0', '3', ''], ['r1c0', '1', 'mud'], ['r1c1', '0', '']]) {
        comparisons.push({
            name: `${hostName}, ${start === 'r1c0' ? 'A2' : 'B2'}, pin=${pin}${chrome ? `, chrome=${chrome}` : ''}: before against after`,
            host: hostName, start, pin, chrome,
            sides: [['before', before], ['after', after]],
        });
    }
}

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

const results = { hosts: host, rounds: ROUNDS, dragsPerBatch: DRAGS, startedAt: new Date().toISOString(), comparisons: [] };
for (const comparison of comparisons) {
    const sides = Object.fromEntries(comparison.sides.map(([label]) => [label, { rows: [], timedOut: 0, problems: [] }]));
    let browser = '';
    for (let round = 0; round < ROUNDS; round++) {
        for (const [label, base] of comparison.sides) {
            try {
                const out = execFileSync('node', [dragCost, base, label, String(DRAGS), comparison.start], {
                    encoding: 'utf8',
                    env: { ...process.env, EXGRID_PIN: comparison.pin, EXGRID_CHROME: comparison.chrome, EXGRID_INJECT: '' },
                    stdio: ['ignore', 'pipe', 'ignore'],
                    timeout: 600_000,
                });
                const batch = JSON.parse(out.split('\n').find((line) => line.startsWith('{')));
                sides[label].problems.push(...batch.problems);
                browser = batch.chrome;
                sides[label].rows.push(...batch.rows);
            } catch {
                sides[label].timedOut++;
            }
        }
    }
    const entry = { ...comparison, browser, sides };
    results.comparisons.push(entry);
    for (const [label, side] of Object.entries(sides)) {
        side.summary = side.rows.length ? summary(side.rows) : null;
        const t = side.summary?.task;
        console.log(`${comparison.name} | ${label}: n=${side.rows.length}${side.timedOut ? ` (${side.timedOut} batch timed out)` : ''}${side.problems.length ? `, ${side.problems.length} console problems` : ''}, task median ${t?.median} ms, IQR ${t?.q1}-${t?.q3}, style median ${side.summary?.style.median}, script median ${side.summary?.script.median}`);
    }
}
results.finishedAt = new Date().toISOString();
fs.writeFileSync(`${here}raw/drag-cost.json`, `${JSON.stringify(results, null, 1)}\n`);
