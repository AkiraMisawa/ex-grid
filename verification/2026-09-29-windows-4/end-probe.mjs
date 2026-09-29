// verify-on-windows-4.md Part D, "A scroll to the end before the grid knows its ceiling" (ADR-0053,
// "Settled after the third Windows run"): /wide opened in a fresh tab, and as soon as the first rows
// are painted, either the scrollbar's thumb dragged to the bottom with the real mouse (MODE=drag) or
// one real click in the grid and a real Ctrl+End (MODE=ctrl-end). Then, once things settle: is the
// last row painted, where is the view, and where is the thumb. TRIES tries (5). "As soon as" is
// WHEN=painted (a first row painted) or WHEN=ready (the grid interactive and not busy), below.
//
// Run on Windows from a copy of tests/ExGrid.Browser (for its node_modules), headed, the window at
// the display's own scale (viewport: null), one host and one browser:
//
//     set EXGRID_CHANNEL=msedge & set MODE=drag & set INPUT=\\wsl.localhost\...\input-server.ps1
//     set SHOTS=C:\... & node end-probe.mjs http://localhost:6298
//
// The input goes through input-server.ps1, started once and kept running, so a gesture leaves
// within milliseconds of the page being ready. A script installed before the page loads records,
// frame by frame from navigation: when the first row is painted, when #demo-interactive appears,
// every change of the spacer's declared height (the ceiling reaching the grid shows as the 28,000,000
// px spacer becoming the compressed one), the scroll offset, and the mousedown/keydown the page saw.
// Playwright only reads. Prints one JSON document.
import { chromium } from '@playwright/test';
import { spawn } from 'node:child_process';
import readline from 'node:readline';
import path from 'node:path';

const BASE = process.argv[2] ?? 'http://localhost:5299';
const CHANNEL = process.env.EXGRID_CHANNEL ?? 'chrome';
const MODE = process.env.MODE ?? 'drag';
// WHEN=painted: the moment a first row is painted, which on the Server host is the prerendered grid,
// before the circuit is up. WHEN=ready: the first moment the grid is interactive and not busy
// (#demo-interactive attached, a first row in a grid without aria-busy).
const WHEN = process.env.WHEN ?? 'painted';
const TRIES = Number(process.env.TRIES ?? 5);
const SHOTS = process.env.SHOTS;
const LABEL = process.env.LABEL ?? '';
// Where the thumb is grabbed at scrollTop 0, in CSS px below the scroller's top (from the
// calibration screenshot); across, the middle of the scroller's vertical gutter.
const THUMB_Y = Number(process.env.THUMB_Y ?? 24);
const out = { channel: CHANNEL, base: BASE, label: LABEL, mode: MODE, when: WHEN, thumbY: THUMB_Y, tries: [] };
const messages = [];

// The input helper.
const helper = spawn('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', process.env.INPUT], { stdio: ['pipe', 'pipe', 'pipe'] });
const lines = readline.createInterface({ input: helper.stdout });
const waiting = [];
lines.on('line', (l) => { const w = waiting.shift(); if (w) w(l.trim()); });
const firstLine = new Promise((done) => waiting.push(done));
function input(cmd) {
    return new Promise((done, fail) => {
        waiting.push((l) => (l.startsWith('ok') ? done(l) : fail(new Error(`${cmd}: ${l}`))));
        helper.stdin.write(cmd + '\n');
    });
}
if ((await firstLine) !== 'ready') throw new Error('input-server did not start');

const browser = await chromium.launch({ channel: CHANNEL, headless: false, args: ['--window-position=40,40', '--window-size=1600,1000'] });
out.browserVersion = browser.version();
const context = await browser.newContext({ viewport: null });

// Recorded in the page from its first frame.
const RECORDER = () => {
    const rec = { t0: performance.now(), firstRow: null, interactive: null, lastRow: null, spacer: [], scroll: [], events: [], ceilingProbe: [] };
    window.__end = rec;
    const now = () => Math.round(performance.now());
    for (const type of ['mousedown', 'mouseup', 'keydown']) {
        window.addEventListener(type, (e) => rec.events.push({ t: now(), type, key: e.key ?? null, ctrl: e.ctrlKey, x: e.clientX ?? null, y: e.clientY ?? null, target: e.target?.className ?? null }), true);
    }
    let lastSpacer = null, lastTop = null, lastProbe = null;
    const step = () => {
        const root = document.querySelector('.ex-grid');
        if (root) {
            if (rec.firstRow === null && root.querySelector("[id$='-r0c0']")) rec.firstRow = now();
            if (rec.interactive === null && document.querySelector('#demo-interactive')) rec.interactive = now();
            if (rec.lastRow === null && root.querySelector("[id$='-r999999c0']")) rec.lastRow = now();
            const sp = root.querySelector('.ex-spacer');
            const declared = sp ? (/height:\s*([\d.]+)px/.exec(sp.getAttribute('style') ?? '') ?? [])[1] ?? null : null;
            if (declared !== lastSpacer) { rec.spacer.push({ t: now(), declared: declared === null ? null : Number(declared) }); lastSpacer = declared; }
            const probe = root.querySelector('.ex-ceiling-probe > div')?.getBoundingClientRect().height ?? null;
            if (probe !== lastProbe) { rec.ceilingProbe.push({ t: now(), height: probe }); lastProbe = probe; }
            const sc = root.querySelector('.ex-scroller');
            if (sc && sc.scrollTop !== lastTop) { rec.scroll.push({ t: now(), scrollTop: sc.scrollTop, scrollHeight: sc.scrollHeight }); lastTop = sc.scrollTop; }
        }
        if (now() - rec.t0 < 20000) requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
};

// What is painted and where, once settled.
const VIEW = () => {
    const root = document.querySelector('.ex-grid');
    const sc = root.querySelector('.ex-scroller');
    const head = root.querySelector('[role=columnheader]');
    const box = sc.getBoundingClientRect();
    const headBottom = head ? head.getBoundingClientRect().bottom : box.top;
    const bottom = box.top + sc.clientHeight;
    let top = null, last = null;
    for (const el of sc.querySelectorAll('[id]')) {
        const m = /-r(\d+)c0$/.exec(el.id);
        if (!m) continue;
        const r = el.getBoundingClientRect();
        if (r.height === 0) continue;
        const row = Number(m[1]);
        if (r.bottom > headBottom + 0.5 && r.top < bottom - 0.5) {
            if (top === null || row < top.row) top = { row, top: r.top, bottom: r.bottom };
            if (last === null || row > last.row) last = { row, top: r.top, bottom: r.bottom };
        }
    }
    const lastRow = sc.querySelector("[id$='-r999999c0']")?.getBoundingClientRect() ?? null;
    const spacer = root.querySelector('.ex-spacer');
    return {
        scrollTop: sc.scrollTop, scrollHeight: sc.scrollHeight, clientHeight: sc.clientHeight,
        atEnd: Math.abs(sc.scrollHeight - sc.clientHeight - sc.scrollTop) <= 1,
        thumbFraction: sc.scrollHeight > sc.clientHeight ? sc.scrollTop / (sc.scrollHeight - sc.clientHeight) : null,
        firstVisibleRow: top?.row ?? null, lastVisibleRow: last?.row ?? null,
        lastRowPainted: lastRow !== null,
        lastRowWhole: lastRow !== null && lastRow.top >= headBottom - 0.5 && lastRow.bottom <= bottom + 0.5,
        lastRowRect: lastRow ? { top: lastRow.top, bottom: lastRow.bottom } : null,
        viewBottom: bottom, headBottom,
        spacerDeclared: spacer ? Number((/height:\s*([\d.]+)px/.exec(spacer.getAttribute('style') ?? '') ?? [])[1]) : null,
        ceilingProbe: root.querySelector('.ex-ceiling-probe > div')?.getBoundingClientRect().height ?? null,
        activeDescendant: root.getAttribute('aria-activedescendant'),
        busy: root.hasAttribute('aria-busy'),
    };
};

// Screen pixels for a client point of this tab, calibrated against the page's own mousemove.
async function calibrate(page, title) {
    await page.evaluate(() => { window.__move = null; document.addEventListener('mousemove', (e) => { window.__move = { x: e.clientX, y: e.clientY }; }, true); });
    const g = await page.evaluate(() => ({ sx: window.screenX, sy: window.screenY, ow: window.outerWidth, oh: window.outerHeight, iw: window.innerWidth, ih: window.innerHeight, dpr: window.devicePixelRatio }));
    const border = (g.ow - g.iw) / 2;
    const target = { x: 400, y: 300 };
    let ox = (g.sx + border) * g.dpr, oy = (g.sy + (g.oh - g.ih) - border) * g.dpr;
    const tries = [];
    for (let i = 0; i < 4; i++) {
        await page.evaluate(() => { window.__move = null; });
        const px = Math.round(ox + target.x * g.dpr), py = Math.round(oy + target.y * g.dpr);
        await input(`move ${px} ${py}`);
        await page.waitForTimeout(200);
        const m = await page.evaluate(() => window.__move);
        tries.push({ px, py, client: m });
        if (!m) continue;
        const dx = target.x - m.x, dy = target.y - m.y;
        if (Math.abs(dx) < 1 && Math.abs(dy) < 1) break;
        ox += dx * g.dpr; oy += dy * g.dpr;
    }
    return { dpr: g.dpr, ox, oy, tries, at: (x, y) => [Math.round(ox + x * g.dpr), Math.round(oy + y * g.dpr)] };
}

for (let i = 1; i <= TRIES; i++) {
    const title = `end-probe-${LABEL}-${CHANNEL}-${MODE}-${WHEN}-${i}`;
    const t = { try: i };
    const page = await context.newPage();
    page.on('console', (m) => { if (m.type() === 'error' || m.type() === 'warning') messages.push(`${i} ${m.type()}: ${m.text()}`); });
    page.on('pageerror', (e) => messages.push(`${i} pageerror: ${e}`));
    try {
        await page.evaluate((x) => { document.title = x; }, title);
        await input(`front ${title}`);
        const cal = await calibrate(page, title);
        t.calibration = { dpr: cal.dpr, tries: cal.tries };
        await page.addInitScript(RECORDER);
        const nav = Date.now();
        await page.goto(`${BASE}/wide`, { waitUntil: 'commit' });
        // As soon as the first rows are painted: the scroller's rect in the same breath.
        const box = await (await page.waitForFunction((when) => {
            const root = document.querySelector(when === 'ready' ? '.ex-grid:not([aria-busy])' : '.ex-grid');
            if (when === 'ready' && !document.querySelector('#demo-interactive')) return null;
            const sc = root?.querySelector('.ex-scroller');
            if (!sc || !root.querySelector("[id$='-r0c0']")) return null;
            const r = sc.getBoundingClientRect();
            const cell = root.querySelector("[id$='-r3c2']")?.getBoundingClientRect() ?? null;
            return { left: r.left, top: r.top, right: r.right, bottom: r.bottom, clientWidth: sc.clientWidth, clientHeight: sc.clientHeight, t: Math.round(performance.now()),
                cell: cell ? { x: cell.left + cell.width / 2, y: cell.top + cell.height / 2 } : null, spacer: root.querySelector('.ex-spacer')?.getAttribute('style') ?? null,
                busy: root.hasAttribute('aria-busy'), interactive: !!document.querySelector('#demo-interactive') };
        }, WHEN, { polling: 'raf', timeout: 60_000 })).jsonValue();
        t.seen = { ...box, sinceNavigationMs: Date.now() - nav };
        if (MODE === 'drag') {
            const x = box.left + box.clientWidth + (box.right - box.left - box.clientWidth) / 2;
            const [x1, y1] = cal.at(x, box.top + THUMB_Y);
            const [, y2] = cal.at(x, box.bottom + 150);
            t.gesture = { cmd: `drag ${x1} ${y1} ${x1} ${y2} 12 8`, answer: await input(`drag ${x1} ${y1} ${x1} ${y2} 12 8`) };
        } else {
            const [cx, cy] = cal.at(box.cell.x, box.cell.y);
            t.gesture = { click: await input(`click ${cx} ${cy}`), keys: await input('keys ^{END}') };
        }
        t.gesture.sinceNavigationMs = Date.now() - nav;
        await page.waitForTimeout(3000);
        await page.waitForFunction(() => !document.querySelector('.ex-grid[aria-busy]'), null, { timeout: 30_000 }).catch(() => {});
        await page.waitForTimeout(500);
        t.settled = await page.evaluate(VIEW);
        t.record = await page.evaluate(() => window.__end);
        if (SHOTS) {
            const file = path.join(SHOTS, `D-end-${LABEL}-${CHANNEL}-${MODE}-${WHEN}-${i}.png`);
            await page.evaluate((x) => { document.title = x; }, title);
            await page.waitForTimeout(150);
            t.shot = { file: `shots/${path.basename(file)}`, answer: await input(`shot ${title} ${file}`) };
        }
    } catch (e) {
        t.error = String(e).slice(0, 600);
    }
    out.tries.push(t);
    await page.close();
}
out.console = messages;
await browser.close();
helper.stdin.write('quit\n');
console.log(JSON.stringify(out, null, 2));
