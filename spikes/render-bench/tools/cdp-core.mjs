// The core comparison (ADR-0350 section 15): opens each /core/<scenario> page of one host in a browser
// already listening for CDP, runs core-bench.js there, and writes what it returns, with every console
// error and uncaught exception each page reported, to results/ under a name of its own. A sibling of
// cdp-input.mjs; no npm dependencies (Node 22 has fetch and WebSocket).
//
//   node tools/cdp-core.mjs <base-url> <label> [scenarios] [rounds]
//
// <base-url> is the host's root (http://127.0.0.1:5491); scenarios is a comma-separated list of grid,
// template, sheet and pivot (all four by default); a React host serves grid and template only. The
// label names the core and host, e.g. `csharp-server`, `csharp-wasm`, later `script-react`,
// `script-blazor-server`; tools/summarize-core.mjs puts labels side by side. CORE_OPTS is JSON merged
// into the run's options (core-bench.js, `run`). CDP_HOST and CDP_PORT name the browser's debugging
// endpoint (127.0.0.1:9222 by default); RESULTS_DIR where the JSON goes (results/ by default).
import fs from 'node:fs';
import path from 'node:path';

const CDP = `http://${process.env.CDP_HOST ?? '127.0.0.1'}:${process.env.CDP_PORT ?? 9222}`;
const [base, label, scenarioList = 'grid,template,sheet,pivot', rounds = '5'] = process.argv.slice(2);
if (!base || !label) {
    console.error('usage: node tools/cdp-core.mjs <base-url> <label> [scenarios] [rounds]');
    process.exit(2);
}
const options = { rounds: Number(rounds), ...JSON.parse(process.env.CORE_OPTS ?? '{}') };
const host = process.env.CDP_HOST ?? '127.0.0.1';
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

const runScenario = async (scenario) => {
    const url = `${base.replace(/\/$/, '')}/core/${scenario}`;
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
    // A page that crashes or closes mid-run fails whatever was asked of it, rather than leaving it unanswered.
    const gone = (why) => {
        for (const { reject } of pending.values()) reject(new Error(why));
        pending.clear();
    };
    ws.onclose = () => gone(`the page's debugging connection closed (${url})`);
    ws.onerror = () => gone(`the page's debugging connection failed (${url})`);
    const rpc = (method, params = {}) => {
        const id = nextId++;
        ws.send(JSON.stringify({ id, method, params }));
        return new Promise((resolve, reject) => pending.set(id, { resolve, reject }));
    };
    const evaluate = async (expression) => {
        const answer = await rpc('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
        if (answer.exceptionDetails) {
            throw new Error(JSON.stringify(answer.exceptionDetails.exception?.description ?? answer.exceptionDetails));
        }
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
        } else if (message.method === 'Log.entryAdded' && ['error', 'warning'].includes(message.params.entry.level) && !/favicon\.ico/.test(message.params.entry.url ?? '')) {
            complaints.push({ kind: `log.${message.params.entry.level}`, text: `${message.params.entry.text} ${message.params.entry.url ?? ''}`.trim() });
        }
    };
    await rpc('Runtime.enable');
    await rpc('Log.enable');
    await rpc('Page.bringToFront');
    let ready = false;
    for (let i = 0; i < 240 && !ready; i++) {
        ready = await evaluate('window.coreBench?.ready() === true').catch(() => false);
        if (!ready) await sleep(500);
    }
    let result;
    try {
        if (!ready) throw new Error(`${url} never painted a cell`);
        result = await evaluate(`window.coreBench.run(${JSON.stringify(scenario)}, ${JSON.stringify(options)})`);
    } catch (error) {
        result = { scenario, failed: String(error.message ?? error) };
    }
    result.url = url;
    result.complaints = complaints;
    await fetch(`${CDP}/json/close/${target.id}`).catch(() => {});
    ws.close();
    console.log(`${scenario}: ${result.failed ? `FAILED: ${result.failed}` : 'done'}${complaints.length ? ` (${complaints.length} complaints)` : ''}`);
    return result;
};

const at = new Date().toISOString();
const scenarios = [];
for (const scenario of scenarioList.split(',')) scenarios.push(await runScenario(scenario));
const name = `${at.replace(/[-:]/g, '').replace(/\..*/, '')}-core-${label}.json`;
const file = path.join(process.env.RESULTS_DIR ?? path.join(path.dirname(new URL(import.meta.url).pathname), '..', 'results'), name);
fs.writeFileSync(file, JSON.stringify({ label, base, at, options, scenarios }, null, 2));
console.log(`wrote ${file}`);
// A scenario that failed, or a phase in one, is a failed run, whatever was written.
const failed = scenarios.some((s) => s.failed || JSON.stringify(s.rounds ?? []).includes('Failed"'));
process.exit(failed ? 1 : 0);
