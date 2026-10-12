// Runs scroll-layers.mjs on both builds, at 100% and at 150%, under both Chromes, interleaving the
// two builds round by round; writes every run to raw/scroll-layers.json and prints the medians.
//
//     xvfb-run -a node run-scroll.mjs
//
// The hosts as run-drag.mjs takes them; EXGRID_ROUNDS the rounds (2, as raw/scroll-layers.json was taken).
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';

const here = new URL('.', import.meta.url).pathname;
const host = {
    server: [['before', process.env.EXGRID_BEFORE_SERVER ?? 'http://localhost:7470'], ['after', process.env.EXGRID_AFTER_SERVER ?? 'http://localhost:6470']],
    wasm: [['before', process.env.EXGRID_BEFORE_WASM ?? 'http://localhost:8471'], ['after', process.env.EXGRID_AFTER_WASM ?? 'http://localhost:5471']],
};
const ROUNDS = Number(process.env.EXGRID_ROUNDS ?? 2);
const runs = [];
for (const hostName of Object.keys(host)) {
    for (const scale of ['1', '1.5']) {
        for (const chrome of ['', 'mud']) {
            for (let round = 0; round < ROUNDS; round++) {
                for (const [label, base] of host[hostName]) {
                    try {
                        const out = execFileSync('node', [`${here}scroll-layers.mjs`, base, label, scale, chrome], {
                            encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'], timeout: 600_000,
                        });
                        const run = JSON.parse(out.split('\n').find((line) => line.startsWith('{')));
                        runs.push({ host: hostName, round, ...run });
                        const f = (s) => `${s.frames.down.summary.median}/${s.frames.down.summary.p95} down, ${s.frames.across.summary.median}/${s.frames.across.summary.p95} across`;
                        console.log(`${hostName} ${scale} ${chrome || 'builtin'} ${label} r${round}: layers ${run.oneCell.layers.count} -> ${run.columnA.layers.count} (shade ${run.columnA.shade}); one cell ${f(run.oneCell)}; column A ${f(run.columnA)}${run.problems.length ? `; ${run.problems.length} problems` : ''}`);
                    } catch (error) {
                        runs.push({ host: hostName, round, label, base, scale: Number(scale), chrome: chrome || 'builtin', failed: String(error).slice(0, 500) });
                        console.log(`${hostName} ${scale} ${chrome || 'builtin'} ${label} r${round}: failed`);
                    }
                }
            }
        }
    }
}
fs.writeFileSync(`${here}raw/scroll-layers.json`, `${JSON.stringify({ hosts: host, rounds: ROUNDS, runs }, null, 1)}\n`);
