// First-frame probe for a CSS-decided #### (docs/research/css-decided-overflow.md).
//
// A scroll timeline is fed from layout, and layout happens after style. If the browser
// painted a frame with the timeline's state from BEFORE that frame's layout, a cell that
// has just started to overflow would show its clipped digits for one frame — the
// recycled-row worry behind ADR-0027 P8. This drives Chromium frame by frame
// (HeadlessExperimental.beginFrame, deterministic: exactly one frame per call), changes
// the DOM between frames without letting any frame run, and reads the pixels of the
// FIRST frame that contains the change. Digits paint blue, hashes red.
//
//   node tools/css-overflow-frames.mjs [headless_shell path]
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { decodePng, inkIn } from './png.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const exe = process.argv[2] ?? '/opt/pw-browsers/chromium_headless_shell-1194/chrome-linux/headless_shell';
const candidate = process.env.CANDIDATE ?? 'candidate.css';
const url = pathToFileURL(path.join(here, '../css-overflow/probe.html')).href + '?css=' + candidate;
const port = 9333;
const userDir = fs.mkdtempSync('/tmp/css-overflow-frames-');

const proc = spawn(exe, [
    `--remote-debugging-port=${port}`, `--user-data-dir=${userDir}`,
    '--enable-begin-frame-control', '--run-all-compositor-stages-before-draw',
    '--deterministic-mode', '--disable-gpu', '--no-sandbox', '--allow-file-access-from-files',
    '--window-size=1280,1000', '--force-device-scale-factor=1', 'about:blank',
], { stdio: ['ignore', 'ignore', 'pipe'] });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

let ver;
for (let i = 0; i < 50 && !ver; i++) {
    try { ver = await (await fetch(`http://127.0.0.1:${port}/json/version`)).json(); } catch { await sleep(200); }
}
const ws = new WebSocket(ver.webSocketDebuggerUrl);
await new Promise((r) => ws.addEventListener('open', r, { once: true }));
let nextId = 1;
const pending = new Map();
ws.addEventListener('message', (ev) => {
    const m = JSON.parse(ev.data);
    if (m.id && pending.has(m.id)) { const p = pending.get(m.id); pending.delete(m.id); m.error ? p.reject(new Error(JSON.stringify(m.error))) : p.resolve(m.result); }
});
const send = (method, params = {}, sessionId) => {
    const id = nextId++;
    ws.send(JSON.stringify({ id, method, params, sessionId }));
    return new Promise((resolve, reject) => pending.set(id, { resolve, reject }));
};

const { browserContextId } = await send('Target.createBrowserContext');
const { targetId } = await send('Target.createTarget', { url: 'about:blank', width: 1280, height: 1000, enableBeginFrameControl: true, browserContextId });
const { sessionId } = await send('Target.attachToTarget', { targetId, flatten: true });
const s = (m, p) => send(m, p, sessionId);
await s('Page.enable');
await s('Runtime.enable');

let frameTime = Date.now();
const frame = async (screenshot = false) => {
    frameTime += 1000 / 60;
    return s('HeadlessExperimental.beginFrame', { frameTimeTicks: frameTime, interval: 1000 / 60, noDisplayUpdates: false, ...(screenshot ? { screenshot: { format: 'png' } } : {}) });
};
const evaluate = async (expression) => {
    const r = await s('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: false });
    if (r.exceptionDetails) throw new Error(JSON.stringify(r.exceptionDetails));
    return r.result.value;
};

// Navigate, pumping frames until the page and its fonts are in.
s('Page.navigate', { url });
for (let i = 0; i < 120; i++) {
    await frame();
    if (await evaluate(`document.readyState === 'complete' && !!window.probe && document.fonts.status === 'loaded'`)) break;
}
await evaluate(`document.querySelector('.ex-grid').classList.add('probe-colour'); true`);

const rectOf = (id) => evaluate(`(() => { const r = document.getElementById(${JSON.stringify(id)}).getBoundingClientRect(); return { x: r.x, y: r.y, width: r.width, height: r.height }; })()`);

// Each scenario: `setup` runs and settles over a few frames, `change` runs with NO frame in
// between, and the next single frame's pixels are read for the cell, then a few frames later.
const scenarios = [
    { name: 'a new row mounted already overflowing (fresh element, e.g. a row scrolled into the Viewport)',
      setup: `probe.clear(); true`,
      change: `probe.row([{ text: '123,456,789,012.50', width: 80, id: 'c' }]); true`, expect: 'hashed' },
    { name: 'a new row mounted fitting',
      setup: `probe.clear(); true`,
      change: `probe.row([{ text: '1,234.56', width: 120, id: 'c' }]); true`, expect: 'value' },
    { name: 'text rewritten in place: fits → overflows (value churn, or a cell reused for another column)',
      setup: `probe.clear(); probe.row([{ text: '1,234.56', width: 120, id: 'c' }]); true`,
      change: `document.getElementById('c').textContent = '123,456,789,012.50'; true`, expect: 'hashed' },
    { name: 'text rewritten in place: overflows → fits',
      setup: `probe.clear(); probe.row([{ text: '123,456,789,012.50', width: 120, id: 'c' }]); true`,
      change: `document.getElementById('c').textContent = '1,234.56'; true`, expect: 'value' },
    { name: 'inline width narrowed (a column resize applied)',
      setup: `probe.clear(); probe.row([{ text: '123,456,789,012.50', width: 200, id: 'c' }]); true`,
      change: `document.getElementById('c').style.width = '100px'; true`, expect: 'hashed' },
    { name: 'inline width widened',
      setup: `probe.clear(); probe.row([{ text: '123,456,789,012.50', width: 100, id: 'c' }]); true`,
      change: `document.getElementById('c').style.width = '200px'; true`, expect: 'value' },
    { name: 'a Consumer class making the text bold, on a value that just fits at 400',
      setup: `probe.clear(); probe.row([{ text: '123,456,789,012.50', width: 160, id: 'c' }]); true`,
      change: `document.getElementById('c').classList.add('probe-bold'); true`, expect: 'hashed' },
    { name: 'the row turning into a total row (weight 600 from the row class)',
      setup: `probe.clear(); probe.row([{ text: '123,456,789,012.50', width: 160, id: 'c' }]); true`,
      change: `document.getElementById('c').parentElement.classList.add('ex-row-total'); true`, expect: 'hashed' },
    { name: 'a row element reused for another row: every cell text rewritten, mixed outcomes',
      setup: `probe.clear(); probe.row([{ text: '1.00', width: 100, id: 'c' }, { text: '123,456,789.00', width: 100, id: 'd' }]); true`,
      change: `document.getElementById('c').textContent = '9,999,999,999.99'; document.getElementById('d').textContent = '2.00'; true`, expect: 'hashed', also: { id: 'd', expect: 'value' } },
    // CONTROL: proves the screenshot is of the frame asked for, not a settled page. A
    // 1s stepped colour transition from blue to red must still be blue on the first frame
    // and on the fifth (83ms of frame time later). If this read red, the method could not
    // see a stale frame and every OK above would be meaningless.
    { name: 'CONTROL: a 1s stepped colour transition (blue → red) — must NOT have arrived yet',
      setup: `probe.clear(); probe.row([{ text: '1,234.56', width: 120, id: 'c' }]); document.getElementById('c').style.transition = 'color 1s steps(1, end)'; true`,
      change: `document.getElementById('c').style.color = 'rgb(255, 0, 0)'; true`, expect: 'value' },
];

const classify = (ink) => ink.red > 0 && ink.blue === 0 ? 'hashed' : ink.blue > 0 && ink.red === 0 ? 'value' : ink.red === 0 && ink.blue === 0 ? 'blank' : 'BOTH';
const results = [];
for (const sc of scenarios) {
    await evaluate(sc.setup);
    for (let i = 0; i < 5; i++) await frame();
    await evaluate(sc.change);
    const first = await frame(true);
    for (let i = 0; i < 4; i++) await frame();
    const later = await frame(true);
    const row = { scenario: sc.name, expected: sc.expect, hasDamage: first.hasDamage };
    for (const [label, f] of [['firstFrame', first], ['fifthFrame', later]]) {
        if (!f.screenshotData) { row[label] = 'NO SCREENSHOT'; continue; }
        const img = decodePng(Buffer.from(f.screenshotData, 'base64'));
        const ink = inkIn(img, await rectOf('c'));
        row[label] = classify(ink);
        row[label + 'Ink'] = ink;
        if (sc.also) row[label + 'Other'] = classify(inkIn(img, await rectOf(sc.also.id)));
    }
    row.ok = row.firstFrame === sc.expect && row.fifthFrame === sc.expect && (!sc.also || (row.firstFrameOther === sc.also.expect && row.fifthFrameOther === sc.also.expect));
    results.push(row);
}

const version = ver.Browser;
const report = { browser: version, candidate, method: 'HeadlessExperimental.beginFrame, one frame per call, DOM changed between frames', results };
console.log(JSON.stringify(report, null, 2));
const outDir = path.join(here, '../results/css-overflow');
fs.mkdirSync(outDir, { recursive: true });
fs.writeFileSync(path.join(outDir, 'frames-' + candidate.replace('.css', '') + '-' + new Date().toISOString().replace(/[:.]/g, '-') + '.json'), JSON.stringify(report, null, 2));
ws.close();
proc.kill();
