// Runs the Probe in the Worker (serve.mjs's /probe) in a browser already listening for CDP, and writes
// what it answers: each engine step's ms on that Worker's runtime (EngineHost's Probe.cs on the
// interpreter or AOT, or the TypeScript engine's probe in ts-engine-worker.ts). A sibling of
// render-bench's cdp-core.mjs; no npm dependencies.
//
//   node tools/cdp-probe.mjs <base-url> <runtime label> [repeats]
//
// CDP_HOST and CDP_PORT name the browser's debugging endpoint; RESULTS_DIR where the JSON goes.
import fs from 'node:fs';
import path from 'node:path';
import { readWhenSet } from './cdp-page.mjs';

const [base, runtime, repeats = '5'] = process.argv.slice(2);
if (!base || !runtime) {
    console.error('usage: node tools/cdp-probe.mjs <base-url> <runtime label> [repeats]');
    process.exit(2);
}
const url = `${base.replace(/\/$/, '')}/probe?runtime=${encodeURIComponent(runtime)}&repeats=${repeats}`;
const result = await readWhenSet(url, 'window.probeResult', 10);
if (result === undefined) {
    console.error(`${url} answered nothing in 10 minutes`);
    process.exit(1);
}
const at = new Date().toISOString();
const file = path.join(process.env.RESULTS_DIR ?? path.join(path.dirname(new URL(import.meta.url).pathname), '..', 'results'),
    `${at.replace(/[-:]/g, '').replace(/\..*/, '')}-probe-${runtime}.json`);
fs.writeFileSync(file, JSON.stringify({ base, at, ...result }, null, 2));
console.log(result.failed ? `FAILED: ${result.failed}` : `wrote ${file}`);
process.exit(result.failed ? 1 : 0);
