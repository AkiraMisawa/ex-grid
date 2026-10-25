// docs/specs/exsheet/verify-on-windows-17.md: the browser half of Parts C, D, F, G and H. A headed
// Windows Chrome or Edge (Playwright's channel, a fresh profile, the display's own scale), kept open and
// fed one JSON command per line on stdin, one JSON answer per line on stdout, so that each step can be
// read before the next is chosen. Every key and press goes through the sixteenth run's input-server.ps1
// (SendInput: a virtual-key and a scan code per key; the real pointer), never through Playwright's own
// input; Playwright opens the page, reads the DOM and takes the page's pictures. The probe's listeners
// (keydown, composition, focus, presses, copy and paste, in the capture phase on the window) log what
// the page saw, as the sixteenth run's ime-probe.mjs did.
//
//   node pg-server.mjs <channel chrome|msedge> <input-server.ps1> <title prefix>
//
// Commands ({"op": ...}):
//   goto {url, ready?}            the page; ready: a JS expression polled until true (60 s)
//   front                         the window to the front (by the title the page is given)
//   raise                         the same, after Playwright's bringToFront (when another program holds the foreground)
//   calibrate                     screen pixels for client points, from the page's own mousemove
//   input {cmd}                   one command to input-server.ps1, as it is (type, burst, imeon, ...)
//   press {sel, nth?, at?, dx?, dy?, button?}
//                                 the middle (or a place) of an element, pressed with the real pointer:
//                                 at: 'centre' | 'left' (dx CSS px in) | 'right' (dx CSS px in)
//   eval {fn, arg?}               page.evaluate of a function's source; its value
//   events                        what the probe's listeners logged since the last call
//   shot {file, clip?, sel?}      the page's picture (Playwright), of a clip or an element's box
//   cdpshot {file, clip?}         the compositor's picture in device pixels (CDP, fromSurface)
//   pixels {clip, file?}          the compositor's picture of a small clip in device pixels, decoded: RGB rows
//   rtt {control, ms}             the latency proxy's round trip
//   version                       the browser's version
//   console                       the console messages and page errors since the last call
//   quit
import { chromium } from '@playwright/test';
import { spawn } from 'node:child_process';
import readline from 'node:readline';
import fs from 'node:fs';

const [CHANNEL, INPUT, PREFIX] = process.argv.slice(2);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const say = (o) => process.stdout.write(JSON.stringify(o) + '\n');

const helper = spawn('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', INPUT], { stdio: ['pipe', 'pipe', 'pipe'] });
const hl = readline.createInterface({ input: helper.stdout });
const waiting = [];
hl.on('line', (l) => { const w = waiting.shift(); if (w) w(l.trim()); });
const first = new Promise((done) => waiting.push(done));
const input = (cmd) => new Promise((done, fail) => {
    waiting.push((l) => (l.startsWith('ok') ? done(l.replace(/ t=\d+$/, '')) : fail(new Error(`${cmd.slice(0, 80)}: ${l}`))));
    helper.stdin.write(cmd + '\n');
});
if ((await first) !== 'ready') throw new Error('input-server did not start');

const browser = await chromium.launch({ channel: CHANNEL, headless: false, args: ['--window-position=40,40', '--window-size=1600,1050'] });
const context = await browser.newContext({ viewport: null });
const page = await context.newPage();
const messages = [];
page.on('console', (m) => messages.push({ t: new Date().toISOString(), type: m.type(), text: m.text() }));
page.on('pageerror', (e) => messages.push({ t: new Date().toISOString(), type: 'pageerror', text: String(e) }));
let title = PREFIX;
let cal = null;

const LISTENERS = () => {
    if (window.__probe) return;
    window.__probe = true; window.__events = [];
    const name = (t) => {
        if (!(t instanceof Element)) return String(t?.nodeName ?? t);
        const grids = [...document.querySelectorAll('.ex-grid')];
        const g = grids.find((x) => x.contains(t));
        const base = `${t.tagName.toLowerCase()}${t.id ? '#' + t.id : ''}${typeof t.className === 'string' && t.className ? '.' + t.className.split(' ')[0] : ''}`;
        return g ? `grid${grids.indexOf(g)}:${t === g ? 'root' : base}` : base;
    };
    const push = (r) => window.__events.push({ t: Math.round(performance.now()), ...r });
    window.addEventListener('focusin', (e) => push({ type: 'focusin', target: name(e.target) }), true);
    window.addEventListener('focusout', (e) => push({ type: 'focusout', target: name(e.target), to: e.relatedTarget ? name(e.relatedTarget) : null }), true);
    for (const type of ['mousedown', 'mouseup', 'click', 'dblclick', 'contextmenu']) window.addEventListener(type, (e) => push({ type, target: name(e.target), x: Math.round(e.clientX), y: Math.round(e.clientY), button: e.button, detail: e.detail, defaultPrevented: e.defaultPrevented || undefined }), true);
    for (const type of ['keydown', 'keyup', 'compositionstart', 'compositionupdate', 'compositionend', 'input', 'copy', 'paste']) {
        window.addEventListener(type, (e) => {
            const r = { type, target: name(e.target) };
            if (type.startsWith('key')) Object.assign(r, { key: e.key, code: e.code, isComposing: e.isComposing || undefined, ctrl: e.ctrlKey || undefined, shift: e.shiftKey || undefined, alt: e.altKey || undefined, repeat: e.repeat || undefined });
            else if (type.startsWith('composition')) r.data = e.data;
            else if (type === 'copy' || type === 'paste') r.text = e.clipboardData?.getData('text/plain')?.slice(0, 200) ?? null;
            else Object.assign(r, { inputType: e.inputType, data: e.data });
            if (e.target instanceof HTMLInputElement) r.value = e.target.value.slice(0, 120);
            push(r);
            // defaultPrevented is read after the page's own handlers: on the next task.
            setTimeout(() => { if (e.defaultPrevented) r.defaultPrevented = true; }, 0);
        }, true);
    }
    window.__move = null;
    document.addEventListener('mousemove', (e) => { window.__move = { x: e.clientX, y: e.clientY }; }, true);
};

async function calibrate() {
    const g = await page.evaluate(() => ({ sx: window.screenX, sy: window.screenY, ow: window.outerWidth, oh: window.outerHeight, iw: window.innerWidth, ih: window.innerHeight, dpr: window.devicePixelRatio }));
    const border = (g.ow * g.dpr - g.iw * g.dpr) / 2 / g.dpr;
    const target = { x: Math.min(400, g.iw - 50), y: Math.min(120, g.ih - 50) };
    let ox = (g.sx + border) * g.dpr, oy = (g.sy + (g.oh - g.ih) - border) * g.dpr;
    const tries = [];
    for (let i = 0; i < 6; i++) {
        await page.evaluate(() => { window.__move = null; });
        const px = Math.round(ox + target.x * g.dpr), py = Math.round(oy + target.y * g.dpr);
        await input(`move ${px} ${py}`);
        await sleep(200);
        const m = await page.evaluate(() => window.__move);
        tries.push({ px, py, client: m });
        if (!m) continue;
        const dx = target.x - m.x, dy = target.y - m.y;
        if (Math.abs(dx) < 1 && Math.abs(dy) < 1) break;
        ox += dx * g.dpr; oy += dy * g.dpr;
    }
    cal = { dpr: g.dpr, ox, oy };
    return { dpr: g.dpr, tries };
}
const at = (x, y) => [Math.round(cal.ox + x * cal.dpr), Math.round(cal.oy + y * cal.dpr)];

const rl = readline.createInterface({ input: process.stdin });
say({ ready: true, version: browser.version(), channel: CHANNEL });
for await (const line of rl) {
    if (!line.trim()) continue;
    let c;
    try { c = JSON.parse(line); } catch (e) { say({ error: `not JSON: ${line.slice(0, 80)}` }); continue; }
    try {
        if (c.op === 'quit') { say({ ok: 'quit' }); break; }
        else if (c.op === 'goto') {
            await page.goto(c.url);
            if (c.ready) await page.waitForFunction(c.ready, null, { timeout: 60_000 });
            await page.evaluate(LISTENERS);
            title = c.title ?? `${PREFIX}-${Date.now() % 100000}`;
            await page.evaluate((t) => { document.title = t; }, title);
            await page.bringToFront();
            await sleep(400);
            await input(`front ${title}`);
            say({ ok: 'goto', title, url: page.url() });
        }
        else if (c.op === 'front') { say({ ok: await input(`front ${title}`) }); }
        else if (c.op === 'raise') { await page.bringToFront(); await sleep(400); say({ ok: await input(`front ${title}`) }); }
        else if (c.op === 'calibrate') { say({ ok: 'calibrate', ...(await calibrate()) }); }
        else if (c.op === 'input') { say({ ok: await input(c.cmd) }); }
        else if (c.op === 'press') {
            const r = await page.evaluate(([sel, nth, where, dx, dy]) => {
                const e = document.querySelectorAll(sel)[nth ?? 0]; if (!e) return null;
                const b = e.getBoundingClientRect();
                const x = where === 'left' ? b.left + (dx ?? 4) : where === 'right' ? b.right - (dx ?? 4) : (b.left + b.right) / 2;
                return { x, y: (b.top + b.bottom) / 2 + (dy ?? 0), box: [b.left, b.top, b.width, b.height] };
            }, [c.sel, c.nth, c.at, c.dx, c.dy]);
            if (!r) throw new Error(`${c.sel} is not on the page`);
            const [x, y] = at(r.x, r.y);
            const verb = c.button === 'right' ? 'rclick' : c.button === 'double' ? 'dblclick' : c.button === 'shift' ? 'shiftclick' : 'click';
            await input(`${verb} ${x} ${y}`);
            say({ ok: 'press', screen: [x, y], client: [r.x, r.y], box: r.box });
        }
        else if (c.op === 'where') {
            // Added for Part H: the screen point of an element, as 'press' finds it, without pressing (for a drag).
            const r = await page.evaluate(([sel, nth, dx, dy]) => {
                const e = document.querySelectorAll(sel)[nth ?? 0]; if (!e) return null;
                const b = e.getBoundingClientRect();
                return { x: (b.left + b.right) / 2 + (dx ?? 0), y: (b.top + b.bottom) / 2 + (dy ?? 0), box: [b.left, b.top, b.width, b.height] };
            }, [c.sel, c.nth, c.dx, c.dy]);
            if (!r) throw new Error(`${c.sel} is not on the page`);
            say({ ok: 'where', screen: at(r.x, r.y), client: [r.x, r.y], box: r.box });
        }
        else if (c.op === 'eval') { say({ ok: 'eval', value: await page.evaluate(new Function('arg', `return (${c.fn})(arg);`), c.arg ?? null) }); }
        else if (c.op === 'events') { say({ ok: 'events', value: await page.evaluate(() => (window.__events ?? []).splice(0)) }); }
        else if (c.op === 'console') { say({ ok: 'console', value: messages.splice(0) }); }
        else if (c.op === 'shot') {
            let clip = c.clip;
            if (c.sel) clip = await page.evaluate((sel) => { const b = document.querySelector(sel).getBoundingClientRect(); return { x: Math.max(0, b.left - 4), y: Math.max(0, b.top - 4), width: b.width + 8, height: b.height + 8 }; }, c.sel);
            fs.writeFileSync(c.file, await page.screenshot(clip ? { clip } : {}));
            say({ ok: 'shot', file: c.file, clip });
        }
        else if (c.op === 'cdpshot') {
            const s = await context.newCDPSession(page);
            const r = await s.send('Page.captureScreenshot', { format: 'png', fromSurface: true, ...(c.clip ? { clip: { ...c.clip, scale: 1 } } : {}) });
            fs.writeFileSync(c.file, Buffer.from(r.data, 'base64')); await s.detach();
            say({ ok: 'cdpshot', file: c.file });
        }
        else if (c.op === 'pixels') {
            // The compositor's picture of a clip (CSS px) in device pixels (CDP, fromSurface), decoded in
            // the page: every pixel's RGB, row by row. For small clips only.
            const s = await context.newCDPSession(page);
            const r = await s.send('Page.captureScreenshot', { format: 'png', fromSurface: true, captureBeyondViewport: false, clip: { ...c.clip, scale: 1 } });
            await s.detach();
            if (c.file) fs.writeFileSync(c.file, Buffer.from(r.data, 'base64'));
            const px = await page.evaluate(async (a) => {
                const bytes = Uint8Array.from(atob(a), (ch) => ch.charCodeAt(0));
                const bmp = await createImageBitmap(new Blob([bytes], { type: 'image/png' }));
                const cv = new OffscreenCanvas(bmp.width, bmp.height); const ctx = cv.getContext('2d'); ctx.drawImage(bmp, 0, 0);
                const d = ctx.getImageData(0, 0, bmp.width, bmp.height).data; const rows = [];
                for (let y = 0; y < bmp.height; y++) { const row = []; for (let x = 0; x < bmp.width; x++) { const i = (y * bmp.width + x) * 4; row.push([d[i], d[i + 1], d[i + 2]]); } rows.push(row); }
                return { width: bmp.width, height: bmp.height, rows };
            }, r.data);
            say({ ok: 'pixels', ...px });
        }
        else if (c.op === 'rtt') { const r = await fetch(`${c.control}/?rtt=${c.ms}`, { method: 'POST' }); say({ ok: 'rtt', status: r.status, now: await (await fetch(c.control)).text() }); }
        else if (c.op === 'version') { say({ ok: 'version', value: browser.version() }); }
        else say({ error: `unknown op ${c.op}` });
    } catch (e) { say({ error: String(e).slice(0, 600) }); }
}
try { await input('imeoff'); await input(`english ${title}`); } catch { }
helper.stdin.write('quit\n');
await browser.close();
process.exit(0);
