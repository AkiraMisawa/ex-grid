// ExChart's measurement (ADR-0283): opens /chart in a browser already listening for CDP, runs
// chart-bench.js's phases there, and writes what it returns to results/ under a name of its own, with
// every console error and uncaught exception the page raised. Built on cdp-settled.mjs.
//
//   node tools/cdp-chart.mjs <url> <label> [rounds]
//
// CDP_HOST and CDP_PORT name the browser's debugging endpoint (127.0.0.1:9222 by default). The label
// names the host and build, e.g. `server`, `wasm`, `wasm-aot`. RESULTS_DIR names where the JSON goes.
import fs from 'node:fs';
import path from 'node:path';

const CDP = `http://${process.env.CDP_HOST ?? '127.0.0.1'}:${process.env.CDP_PORT ?? 9222}`;
const [url, label, rounds = '5'] = process.argv.slice(2);
if (!url || !label) {
    console.error('usage: node tools/cdp-chart.mjs <url> <label> [rounds]');
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
    } else if (message.method === 'Runtime.exceptionThrown') {
        errors.push(message.params.exceptionDetails?.exception?.description ?? message.params.exceptionDetails?.text);
    } else if (message.method === 'Runtime.consoleAPICalled' && ['error', 'warning'].includes(message.params.type)) {
        errors.push(`${message.params.type}: ${message.params.args.map((a) => a.value ?? a.description).join(' ')}`);
    }
};
const errors = [];
await rpc(ws, 'Runtime.enable');
await rpc(ws, 'Page.bringToFront');
let ready = false;
for (let i = 0; i < 120 && !ready; i++) {
    ready = await evaluate(ws, 'window.chartBench?.ready() === true').catch(() => false);
    if (!ready) await sleep(500);
}
if (!ready) {
    const text = await evaluate(ws, "document.body.innerText.slice(0, 300)").catch((e) => String(e));
    console.error(`the chart never drew: ${JSON.stringify(errors)}; the page reads ${JSON.stringify(text)}`);
    process.exit(1);
}
const result = await evaluate(ws, `window.chartBench.run({ rounds: ${Number(rounds)} })`);
result.errors = errors;
result.label = label;
result.url = url;
result.at = new Date().toISOString();
const name = `${result.at.replace(/[-:]/g, '').replace(/\..*/, '')}-chart-${label}.json`;
const file = path.join(process.env.RESULTS_DIR ?? path.join(path.dirname(new URL(import.meta.url).pathname), '..', 'results'), name);
fs.writeFileSync(file, JSON.stringify(result, null, 2));
console.log(`wrote ${file}`);
await fetch(`${CDP}/json/close/${target.id}`).catch(() => {});
ws.close();
