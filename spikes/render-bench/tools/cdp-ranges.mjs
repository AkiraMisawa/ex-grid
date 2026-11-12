// What a Consumer's placement of many ranges costs (ADR-0050 item 4's note of 2026-11-12): opens /core/ranges of
// the React host in a browser already listening for CDP, runs `rangesBench.run` there for each count of ranges,
// and writes what it returns, with every console error and uncaught exception the page reported, to results/.
// A sibling of cdp-core.mjs; no npm dependencies.
//
//   node tools/cdp-ranges.mjs <base-url> <label> [counts]
//
// <base-url> is the React host's root (http://127.0.0.1:5496); counts is a comma-separated list of how many
// one-row ranges to place (1,1000,10000,50000,200000 by default; the first is the floor every other is read
// against, two frames of the browser's). The label names the tree measured, e.g. `sweep` or `before-sweep`.
// CDP_HOST and CDP_PORT name the browser's debugging endpoint (127.0.0.1:9222 by default); RESULTS_DIR where the
// JSON goes (results/ by default).
import fs from 'node:fs';
import path from 'node:path';

const host = process.env.CDP_HOST ?? '127.0.0.1';
const CDP = `http://${host}:${process.env.CDP_PORT ?? 9222}`;
const [base, label, countList = '1,1000,10000,50000,200000'] = process.argv.slice(2);
if (!base || !label) {
    console.error('usage: node tools/cdp-ranges.mjs <base-url> <label> [counts]');
    process.exit(2);
}
const counts = countList.split(',').map(Number);
const url = `${base.replace(/\/$/, '')}/core/ranges`;
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

let nextId = 1;
const pending = new Map();
const complaints = [];
const opened = await fetch(`${CDP}/json/new?${encodeURIComponent(url)}`, { method: 'PUT' });
const target = await opened.json();
const ws = new WebSocket(target.webSocketDebuggerUrl.replace('127.0.0.1', host).replace('localhost', host));
await new Promise((resolve, reject) => {
    ws.onopen = resolve;
    ws.onerror = reject;
});
const gone = (why) => {
    for (const { reject } of pending.values()) reject(new Error(why));
    pending.clear();
};
ws.onclose = () => gone(`the page's debugging connection closed (${url})`);
const rpc = (method, params = {}) => {
    const id = nextId++;
    ws.send(JSON.stringify({ id, method, params }));
    return new Promise((resolve, reject) => pending.set(id, { resolve, reject }));
};
const evaluate = async (expression) => {
    const answer = await rpc('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (answer.exceptionDetails) throw new Error(JSON.stringify(answer.exceptionDetails.exception?.description ?? answer.exceptionDetails));
    return answer.result?.value;
};
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
    }
};
await rpc('Runtime.enable');
await rpc('Page.bringToFront');
let ready = false;
for (let i = 0; i < 240 && !ready; i++) {
    ready = await evaluate('window.rangesBench?.ready() === true').catch(() => false);
    if (!ready) await sleep(500);
}
let result;
try {
    if (!ready) throw new Error(`${url} never painted a cell`);
    result = { rounds: await evaluate(`window.rangesBench.run(${JSON.stringify(counts)})`) };
} catch (error) {
    result = { failed: String(error.message ?? error) };
}
await fetch(`${CDP}/json/close/${target.id}`).catch(() => {});
ws.close();

const at = new Date().toISOString();
const file = path.join(process.env.RESULTS_DIR ?? path.join(path.dirname(new URL(import.meta.url).pathname), '..', 'results'),
    `${at.replace(/[-:]/g, '').replace(/\..*/, '')}-ranges-${label}.json`);
fs.writeFileSync(file, JSON.stringify({ label, url, at, counts, ...result, complaints }, null, 2));
for (const round of result.rounds ?? []) {
    const scrolls = round.scrollPaintMs.toSorted((a, b) => a - b);
    console.log(`${String(round.ranges).padStart(7)} ranges: place ${round.placeMs.toFixed(1)} ms, first paint ${round.firstPaintMs.toFixed(1)} ms, `
        + `scroll paint median ${scrolls[Math.floor(scrolls.length / 2)].toFixed(1)} ms`);
}
console.log(`${result.failed ? `FAILED: ${result.failed}; ` : ''}${complaints.length} complaints; wrote ${file}`);
process.exit(result.failed || complaints.length > 0 ? 1 : 0);
