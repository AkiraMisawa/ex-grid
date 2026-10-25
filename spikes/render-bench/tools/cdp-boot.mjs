// The time to the first answer and the bytes downloaded (ADR-0370 section 6): opens one /core/<scenario>
// page of a host in a fresh tab, with the cache off, and records when the page first painted a cell with
// text, and every byte that crossed the network to paint it: the page's own requests and those of any
// Worker it started. Run once per round; a sibling of cdp-core.mjs, no npm dependencies.
//
//   node tools/cdp-boot.mjs <base-url> <label> [scenarios] [rounds]
//
// The first painted cell is seen by a MutationObserver installed before the page's own scripts run; its
// time is ms from navigation, on the page's clock. A page that records more of its start
// (window.engineBoot, the Engine Host spike's) has that recorded beside it. Bytes are what the network
// carried for each response (CDP's encodedDataLength: compressed, headers included), summed per page.
// CDP_HOST and CDP_PORT name the browser's debugging endpoint; RESULTS_DIR where the JSON goes.
import fs from 'node:fs';
import path from 'node:path';

const CDP = `http://${process.env.CDP_HOST ?? '127.0.0.1'}:${process.env.CDP_PORT ?? 9222}`;
const [base, label, scenarioList = 'sheet,pivot', rounds = '5'] = process.argv.slice(2);
if (!base || !label) {
    console.error('usage: node tools/cdp-boot.mjs <base-url> <label> [scenarios] [rounds]');
    process.exit(2);
}
const host = process.env.CDP_HOST ?? '127.0.0.1';
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

// Installed ahead of the page: the first cell with text inside #core-bench, timed as it lands.
const observe = `(() => {
    const painted = () => [...document.querySelectorAll('#core-bench [role=gridcell]')].some((c) => (c.textContent ?? '') !== '');
    const watch = () => {
        const observer = new MutationObserver(() => {
            if (window.__firstCellAt !== undefined || !painted()) return;
            window.__firstCellAt = performance.now();
            observer.disconnect();
        });
        observer.observe(document.documentElement, { subtree: true, childList: true, characterData: true });
    };
    if (document.documentElement) watch(); else document.addEventListener('readystatechange', watch, { once: true });
})();`;

const openOnce = async (url) => {
    let nextId = 1;
    const pending = new Map();
    const complaints = [];
    const responses = new Map();
    const finished = [];
    const opened = await fetch(`${CDP}/json/new?about:blank`, { method: 'PUT' });
    const target = await opened.json();
    const ws = new WebSocket(target.webSocketDebuggerUrl.replace('127.0.0.1', host).replace('localhost', host));
    await new Promise((resolve, reject) => { ws.onopen = resolve; ws.onerror = reject; });
    const rpc = (method, params = {}, sessionId = undefined) => {
        const id = nextId++;
        ws.send(JSON.stringify({ id, method, params, ...(sessionId ? { sessionId } : {}) }));
        return new Promise((resolve, reject) => pending.set(id, { resolve, reject }));
    };
    const enable = async (sessionId) => {
        await rpc('Network.enable', {}, sessionId);
        await rpc('Network.setCacheDisabled', { cacheDisabled: true }, sessionId);
        await rpc('Runtime.enable', {}, sessionId);
    };
    ws.onmessage = (event) => {
        const message = JSON.parse(event.data);
        if (message.id && pending.has(message.id)) {
            const { resolve, reject } = pending.get(message.id);
            pending.delete(message.id);
            message.error ? reject(new Error(message.error.message)) : resolve(message.result);
            return;
        }
        const p = message.params;
        const key = `${message.sessionId ?? 'page'}:${p?.requestId}`;
        switch (message.method) {
            case 'Target.attachedToTarget':
                // A Worker: its requests are its own session's.
                enable(p.sessionId).then(() => rpc('Runtime.runIfWaitingForDebugger', {}, p.sessionId))
                    .catch((error) => complaints.push({ kind: 'cdp', text: `a Worker could not be followed: ${error.message}` }));
                break;
            case 'Network.responseReceived':
                responses.set(key, { url: p.response.url, encoding: p.response.headers['content-encoding'] ?? p.response.headers['Content-Encoding'] ?? null });
                break;
            case 'Network.loadingFinished':
                finished.push({ key, bytes: p.encodedDataLength });
                break;
            case 'Runtime.consoleAPICalled':
                if (['error', 'warning', 'assert'].includes(p.type)) complaints.push({ kind: `console.${p.type}`, text: p.args.map((a) => a.value ?? a.description ?? '').join(' ') });
                break;
            case 'Runtime.exceptionThrown':
                complaints.push({ kind: 'exception', text: p.exceptionDetails.exception?.description ?? p.exceptionDetails.text });
                break;
        }
    };
    await enable(undefined);
    await rpc('Target.setAutoAttach', { autoAttach: true, waitForDebuggerOnStart: true, flatten: true });
    await rpc('Page.enable');
    await rpc('Page.addScriptToEvaluateOnNewDocument', { source: observe });
    await rpc('Page.bringToFront');
    await rpc('Page.navigate', { url });
    const evaluate = async (expression) => {
        const answer = await rpc('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
        if (answer.exceptionDetails) throw new Error(JSON.stringify(answer.exceptionDetails.exception?.description ?? answer.exceptionDetails));
        return answer.result?.value;
    };
    let firstCellAt;
    for (let i = 0; i < 600 && firstCellAt === undefined; i++) {
        firstCellAt = await evaluate('window.__firstCellAt').catch(() => undefined);
        if (firstCellAt === undefined) await sleep(100);
    }
    // The page may record its own start a frame after the first cell.
    let engineBoot = null;
    for (let i = 0; i < 50; i++) {
        engineBoot = await evaluate('window.engineBoot ?? null').catch(() => null);
        if (engineBoot !== null || !(await evaluate('typeof window.engineEdits === "function" || typeof window.engineOperations === "function"').catch(() => false))) break;
        await sleep(100);
    }
    await sleep(500);
    const files = finished.map(({ key, bytes }) => ({ ...(responses.get(key) ?? { url: key, encoding: null }), bytes }));
    // A Worker's own script is fetched before its target can be followed: CDP's Network domain reports
    // its response with the headers' bytes only (about 200), the page's Resource Timing with the whole
    // transferSize (the compressed body and the headers, as encodedDataLength counts them). Where the page
    // saw more of a response than CDP did, or saw one CDP missed, its count is taken.
    const timed = await evaluate(`performance.getEntriesByType('resource').map((e) => ({ url: e.name, bytes: e.transferSize }))`);
    for (const entry of timed) {
        const seen = files.find((f) => f.url === entry.url);
        if (seen === undefined) files.push({ url: entry.url, encoding: null, bytes: entry.bytes, seenBy: 'resource timing' });
        else if (entry.bytes > seen.bytes) Object.assign(seen, { bytes: entry.bytes, seenBy: 'resource timing' });
    }
    await fetch(`${CDP}/json/close/${target.id}`).catch(() => {});
    ws.close();
    return {
        firstCellAt: firstCellAt ?? null,
        failed: firstCellAt === undefined ? `${url} painted no cell with text in 60 s` : undefined,
        bytes: files.reduce((sum, f) => sum + f.bytes, 0),
        files,
        engineBoot,
        complaints,
    };
};

const at = new Date().toISOString();
const scenarios = [];
for (const scenario of scenarioList.split(',')) {
    const url = `${base.replace(/\/$/, '')}/core/${scenario}`;
    const runs = [];
    for (let round = 0; round < Number(rounds); round++) runs.push(await openOnce(url));
    const failed = runs.find((r) => r.failed)?.failed;
    console.log(`${scenario}: ${failed ? `FAILED: ${failed}` : `first cell ${runs.map((r) => Math.round(r.firstCellAt)).join(', ')} ms; ${runs[0].bytes.toLocaleString('en-US')} bytes`}${runs.some((r) => r.complaints.length) ? ' (complaints)' : ''}`);
    scenarios.push({ scenario, url, runs });
}
const name = `${at.replace(/[-:]/g, '').replace(/\..*/, '')}-boot-${label}.json`;
const file = path.join(process.env.RESULTS_DIR ?? path.join(path.dirname(new URL(import.meta.url).pathname), '..', 'results'), name);
fs.writeFileSync(file, JSON.stringify({ label, base, at, scenarios }, null, 2));
console.log(`wrote ${file}`);
process.exit(scenarios.some((s) => s.runs.some((r) => r.failed)) ? 1 : 0);
