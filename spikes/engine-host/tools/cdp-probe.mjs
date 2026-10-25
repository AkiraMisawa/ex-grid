// Runs EngineHost's Probe in the Worker (serve.mjs's /probe) in a browser already listening for CDP, and
// writes what it answers: each engine step's ms on that build's runtime. A sibling of render-bench's
// cdp-core.mjs; no npm dependencies.
//
//   node tools/cdp-probe.mjs <base-url> <runtime label> [repeats]
//
// CDP_HOST and CDP_PORT name the browser's debugging endpoint; RESULTS_DIR where the JSON goes.
import fs from 'node:fs';
import path from 'node:path';

const CDP = `http://${process.env.CDP_HOST ?? '127.0.0.1'}:${process.env.CDP_PORT ?? 9222}`;
const [base, runtime, repeats = '5'] = process.argv.slice(2);
if (!base || !runtime) {
    console.error('usage: node tools/cdp-probe.mjs <base-url> <runtime label> [repeats]');
    process.exit(2);
}
const host = process.env.CDP_HOST ?? '127.0.0.1';
const url = `${base.replace(/\/$/, '')}/probe?runtime=${encodeURIComponent(runtime)}&repeats=${repeats}`;
const target = await (await fetch(`${CDP}/json/new?${encodeURIComponent(url)}`, { method: 'PUT' })).json();
const ws = new WebSocket(target.webSocketDebuggerUrl.replace('127.0.0.1', host).replace('localhost', host));
await new Promise((resolve, reject) => { ws.onopen = resolve; ws.onerror = reject; });
let nextId = 1;
const pending = new Map();
ws.onmessage = (event) => {
    const message = JSON.parse(event.data);
    if (message.id && pending.has(message.id)) {
        pending.get(message.id)(message.result);
        pending.delete(message.id);
    }
};
const evaluate = (expression) => {
    const id = nextId++;
    ws.send(JSON.stringify({ id, method: 'Runtime.evaluate', params: { expression, returnByValue: true } }));
    return new Promise((resolve) => pending.set(id, (result) => resolve(result?.result?.value)));
};
let result;
for (let i = 0; i < 1200 && result === undefined; i++) {
    result = await evaluate('window.probeResult');
    if (result === undefined) await new Promise((resolve) => setTimeout(resolve, 500));
}
await fetch(`${CDP}/json/close/${target.id}`);
ws.close();
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
