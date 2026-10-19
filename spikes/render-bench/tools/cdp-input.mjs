// The Input Order's measurement (ADR-0240): opens /settled in a browser already listening for CDP,
// runs input-bench.js's phases there, and writes what it returns, with every console error and
// uncaught exception the page reported meanwhile, to results/ under a name of its own. A sibling of
// cdp-settled.mjs; no npm dependencies (Node 22 has fetch and WebSocket).
//
//   node tools/cdp-input.mjs <url> <label> [rounds] [chars] [presses] [menus]
//
// CDP_HOST and CDP_PORT name the browser's debugging endpoint (127.0.0.1:9222 by default). The label
// names the build and host, e.g. `branch-server-0ms`, `main-wasm`. RESULTS_DIR names where the JSON
// goes (results/ by default).
import fs from 'node:fs';
import path from 'node:path';

const CDP = `http://${process.env.CDP_HOST ?? '127.0.0.1'}:${process.env.CDP_PORT ?? 9222}`;
const [url, label, rounds = '7', chars = '20', presses = '20', menus = '10'] = process.argv.slice(2);
if (!url || !label) {
    console.error('usage: node tools/cdp-input.mjs <url> <label> [rounds] [chars] [presses] [menus]');
    process.exit(2);
}

let nextId = 1;
const pending = new Map();
const complaints = [];
const rpc = (ws, method, params = {}) => {
    const id = nextId++;
    ws.send(JSON.stringify({ id, method, params }));
    return new Promise((resolve, reject) => pending.set(id, { resolve, reject }));
};
const evaluate = async (ws, expression) => {
    const answer = await rpc(ws, 'Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (answer.exceptionDetails) {
        throw new Error(JSON.stringify(answer.exceptionDetails.exception?.description ?? answer.exceptionDetails));
    }
    return answer.result?.value;
};
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

const opened = await fetch(`${CDP}/json/new?${encodeURIComponent(url)}`, { method: 'PUT' });
const target = await opened.json();
const host = process.env.CDP_HOST ?? '127.0.0.1';
const ws = new WebSocket(target.webSocketDebuggerUrl.replace('127.0.0.1', host).replace('localhost', host));
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
    } else if (message.method === 'Runtime.consoleAPICalled' && ['error', 'warning', 'assert'].includes(message.params.type)) {
        complaints.push({ kind: `console.${message.params.type}`, text: message.params.args.map((a) => a.value ?? a.description ?? '').join(' ') });
    } else if (message.method === 'Runtime.exceptionThrown') {
        const d = message.params.exceptionDetails;
        complaints.push({ kind: 'exception', text: d.exception?.description ?? d.text });
    } else if (message.method === 'Log.entryAdded' && ['error', 'warning'].includes(message.params.entry.level) && !/favicon\.ico/.test(message.params.entry.url ?? '')) {
        complaints.push({ kind: `log.${message.params.entry.level}`, text: `${message.params.entry.text} ${message.params.entry.url ?? ''}`.trim() });
    }
};
await rpc(ws, 'Runtime.enable');
await rpc(ws, 'Log.enable');
await rpc(ws, 'Page.bringToFront');
for (let i = 0; i < 120; i++) {
    if (await evaluate(ws, 'window.inputBench?.ready() === true').catch(() => false)) break;
    await sleep(500);
}
let result;
try {
    result = await evaluate(ws, `window.inputBench.run({ rounds: ${Number(rounds)}, chars: ${Number(chars)}, presses: ${Number(presses)}, menus: ${Number(menus)} })`);
} catch (error) {
    result = { failed: String(error.message ?? error) };
}
result.label = label;
result.url = url;
result.at = new Date().toISOString();
result.complaints = complaints;
const name = `${result.at.replace(/[-:]/g, '').replace(/\..*/, '')}-input-${label}.json`;
const file = path.join(process.env.RESULTS_DIR ?? path.join(path.dirname(new URL(import.meta.url).pathname), '..', 'results'), name);
fs.writeFileSync(file, JSON.stringify(result, null, 2));
console.log(`wrote ${file}${result.failed ? ` (FAILED: ${result.failed})` : ''}${complaints.length ? ` (${complaints.length} complaints)` : ''}`);
await fetch(`${CDP}/json/close/${target.id}`).catch(() => {});
ws.close();
process.exit(0);
