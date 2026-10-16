// The Settled Mark's measurement (ADR-0220): opens /settled in a browser already listening for CDP,
// runs settled-bench.js's phases there, and writes what it returns to results/ under a name of its
// own. No npm dependencies (Node 24 has fetch and WebSocket).
//
//   node tools/cdp-settled.mjs <url> <label> [rounds] [burst] [frames]
//
// CDP_HOST and CDP_PORT name the browser's debugging endpoint (127.0.0.1:9222 by default): a Chrome
// on the Windows side of WSL is reached at the host's address. The label names the build and host,
// e.g. `with-server`, `without-wasm`. RESULTS_DIR names where the JSON goes (results/ by default).
import fs from 'node:fs';
import path from 'node:path';

const CDP = `http://${process.env.CDP_HOST ?? '127.0.0.1'}:${process.env.CDP_PORT ?? 9222}`;
const [url, label, rounds = '7', burst = '40', frames = '120'] = process.argv.slice(2);
if (!url || !label) {
    console.error('usage: node tools/cdp-settled.mjs <url> <label> [rounds] [burst] [frames]');
    process.exit(2);
}

let nextId = 1;
const pending = new Map();
const rpc = (ws, method, params = {}) => {
    const id = nextId++;
    ws.send(JSON.stringify({ id, method, params }));
    return new Promise((resolve, reject) => pending.set(id, { resolve, reject }));
};
const evaluate = async (ws, expression) => {
    const answer = await rpc(ws, 'Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (answer.exceptionDetails) {
        throw new Error(JSON.stringify(answer.exceptionDetails.exception ?? answer.exceptionDetails));
    }
    return answer.result?.value;
};
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

const opened = await fetch(`${CDP}/json/new?${encodeURIComponent(url)}`, { method: 'PUT' });
const target = await opened.json();
const ws = new WebSocket(target.webSocketDebuggerUrl.replace('127.0.0.1', process.env.CDP_HOST ?? '127.0.0.1').replace('localhost', process.env.CDP_HOST ?? 'localhost'));
await new Promise((resolve, reject) => {
    ws.onopen = resolve;
    ws.onerror = reject;
});
ws.onmessage = (event) => {
    const message = JSON.parse(event.data);
    if (message.id && pending.has(message.id)) {
        const { resolve, reject } = pending.get(message.id);
        pending.delete(message.id);
        message.error ? reject(new Error(message.error.message)) : resolve(message.result);
    }
};
await rpc(ws, 'Runtime.enable');
await rpc(ws, 'Page.bringToFront');
for (let i = 0; i < 120; i++) {
    if (await evaluate(ws, 'window.settledBench?.ready() === true').catch(() => false)) break;
    await sleep(500);
}
const result = await evaluate(ws, `window.settledBench.run({ rounds: ${Number(rounds)}, burst: ${Number(burst)}, frames: ${Number(frames)} })`);
result.label = label;
result.url = url;
result.at = new Date().toISOString();
const name = `${result.at.replace(/[-:]/g, '').replace(/\..*/, '')}-settled-${label}.json`;
const file = path.join(process.env.RESULTS_DIR ?? path.join(path.dirname(new URL(import.meta.url).pathname), '..', 'results'), name);
fs.writeFileSync(file, JSON.stringify(result, null, 2));
console.log(`wrote ${file}`);
await fetch(`${CDP}/json/close/${target.id}`).catch(() => {});
ws.close();
