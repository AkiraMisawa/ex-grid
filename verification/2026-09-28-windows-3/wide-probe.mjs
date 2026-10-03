// verify-on-windows-3.md Part D, beside the suite: on /wide at 10⁶ rows, with the OS's own input,
//
// 1. one real wheel notch at the top (down), and near the end (up, then down again): the rows
//    moved, and, frame by frame for 2 s after the notch, where the top of the view is, so that
//    rows that move back against the scroll (jitter) show;
// 2. Ctrl+Plus to 200% page zoom, then Ctrl+End: whether the last row is painted and the Focus is
//    whole on screen.
//
// Run on Windows from a copy of tests/ExGrid.Browser (for its node_modules), headed, the window
// left to its own size and scale (viewport: null), one host and one browser:
//
//     set EXGRID_CHANNEL=msedge & set OSINPUT=\\wsl.localhost\...\os-input.ps1 & set SHOTS=C:\...
//     node wide-probe.mjs http://localhost:6298
//
// The wheel and the keys go through os-input.ps1 (SetCursorPos, mouse_event, SendKeys) to the
// browser's window, which is brought to the front by its title first; Playwright only reads. The
// view's top is read from the painted cells: the first row whose bottom is below the column
// headings, plus the part of it scrolled under them, in rows. Prints one JSON document.
import { chromium } from '@playwright/test';
import { spawnSync } from 'node:child_process';
import path from 'node:path';

const BASE = process.argv[2] ?? 'http://localhost:5299';
const CHANNEL = process.env.EXGRID_CHANNEL ?? 'chrome';
const OSINPUT = process.env.OSINPUT;
const SHOTS = process.env.SHOTS;
const LABEL = process.env.LABEL ?? '';
const out = { channel: CHANNEL, base: BASE, label: LABEL, wheel: {}, zoom: [] };
const messages = [];

function os(args) {
    const r = spawnSync('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', OSINPUT, ...args.map(String)], { encoding: 'utf8' });
    if (r.status !== 0 || !r.stdout.includes('ok')) throw new Error(`os-input failed: ${r.stdout} ${r.stderr}`);
}

const browser = await chromium.launch({ channel: CHANNEL, headless: false, args: ['--window-position=40,40', '--window-size=1600,1000'] });

async function open(context, title) {
    const page = await context.newPage();
    page.on('console', (m) => { if (m.type() === 'error' || m.type() === 'warning') messages.push(`${m.type()}: ${m.text()}`); });
    page.on('pageerror', (e) => messages.push(`pageerror: ${e}`));
    await page.goto(`${BASE}/wide`);
    await page.locator('#demo-interactive').waitFor({ state: 'attached', timeout: 60_000 });
    await page.waitForFunction(() => !document.querySelector('.ex-grid[aria-busy]'), null, { timeout: 60_000 });
    await page.locator(".ex-grid [id$='-r0c0']").filter({ hasText: 'K-000000' }).waitFor({ timeout: 30_000 });
    await page.evaluate(() => document.addEventListener('mousemove', (e) => { window.__move = { x: e.clientX, y: e.clientY }; }, true));
    const retitle = () => page.evaluate((t) => { document.title = t; }, title);
    await retitle();
    return { page, retitle };
}

// Where the top of the view is, in rows, from what is painted; and the scroller's numbers.
const VIEW = () => {
    const root = document.querySelector('.ex-grid');
    const sc = root.querySelector('.ex-scroller');
    const head = root.querySelector('[role=columnheader]');
    const box = sc.getBoundingClientRect();
    const headBottom = head ? head.getBoundingClientRect().bottom : box.top;
    const rows = new Map();
    for (const el of sc.querySelectorAll('[id]')) {
        const m = /-r(\d+)c\d+$/.exec(el.id);
        if (!m) continue;
        const r = el.getBoundingClientRect();
        if (r.height === 0) continue;
        const row = Number(m[1]);
        if (!rows.has(row)) rows.set(row, { top: r.top, bottom: r.bottom });
    }
    let top = null;
    for (const [row, r] of rows) if (r.bottom > headBottom + 0.5 && (top === null || row < top.row)) top = { row, ...r };
    return {
        scrollTop: sc.scrollTop, scrollHeight: sc.scrollHeight, clientHeight: sc.clientHeight,
        topRow: top?.row ?? null,
        position: top ? top.row + (headBottom - top.top) / (top.bottom - top.top) : null,
        dpr: window.devicePixelRatio,
    };
};

async function pointAtGrid(page, title) {
    const g = await page.evaluate(() => {
        const r = document.querySelector('.ex-grid .ex-scroller').getBoundingClientRect();
        return { sx: window.screenX, sy: window.screenY, ow: window.outerWidth, oh: window.outerHeight, iw: window.innerWidth, ih: window.innerHeight, dpr: window.devicePixelRatio, cx: r.left + r.width * 0.6, cy: r.top + r.height * 0.5 };
    });
    const border = (g.ow - g.iw) / 2;
    let px = Math.round((g.sx + border + g.cx) * g.dpr);
    let py = Math.round((g.sy + (g.oh - g.ih) - border + g.cy) * g.dpr);
    const tries = [];
    for (let i = 0; i < 3; i++) {
        await page.evaluate(() => { window.__move = null; });
        os(['-Title', title, '-X', px, '-Y', py, '-Move']);
        await page.waitForTimeout(250);
        const m = await page.evaluate(() => window.__move);
        tries.push({ px, py, client: m });
        if (!m) break;
        const dx = g.cx - m.x; const dy = g.cy - m.y;
        if (Math.abs(dx) < 3 && Math.abs(dy) < 3) break;
        px += Math.round(dx * g.dpr); py += Math.round(dy * g.dpr);
    }
    return { px, py, target: { x: g.cx, y: g.cy }, tries };
}

async function notch(page, title, at, delta, label) {
    await page.waitForTimeout(600);
    const before = await page.evaluate(VIEW);
    await page.evaluate((VIEWsrc) => {
        const view = new Function(`return (${VIEWsrc})()`);
        window.__samples = [];
        const t0 = performance.now();
        const step = () => {
            const v = view();
            window.__samples.push({ t: Math.round(performance.now() - t0), position: v.position === null ? null : Number(v.position.toFixed(3)), scrollTop: v.scrollTop });
            if (performance.now() - t0 < 2000) requestAnimationFrame(step);
        };
        requestAnimationFrame(step);
    }, VIEW.toString());
    os(['-Title', title, '-X', at.px, '-Y', at.py, '-Wheel', delta]);
    await page.waitForTimeout(2300);
    const after = await page.evaluate(VIEW);
    const samples = await page.evaluate(() => window.__samples);
    // Against the scroll: a frame whose view top moved the other way from the notch, by more than
    // a hundredth of a row.
    const sign = delta < 0 ? 1 : -1;
    const back = [];
    for (let i = 1; i < samples.length; i++) {
        const a = samples[i - 1].position; const b = samples[i].position;
        if (a !== null && b !== null && sign * (b - a) < -0.01) back.push({ t: samples[i].t, from: a, to: b });
    }
    const shot = SHOTS ? path.join(SHOTS, `D-wheel-${label}-${LABEL}-${CHANNEL}.png`) : null;
    if (shot) await page.screenshot({ path: shot });
    return {
        delta, before, after,
        rowsMoved: before.position !== null && after.position !== null ? Number((after.position - before.position).toFixed(3)) : null,
        scrollTopMoved: after.scrollTop - before.scrollTop,
        framesAgainstTheScroll: back,
        samples,
        shot: shot ? `shots/${path.basename(shot)}` : null,
    };
}

// 1. The wheel (skipped with ONLY=zoom).
if (process.env.ONLY !== 'zoom') {
    const title = `wide-probe-wheel-${Date.now()}`;
    const context = await browser.newContext({ viewport: null });
    const { page, retitle } = await open(context, title);
    await page.bringToFront();
    const at = await pointAtGrid(page, title);
    out.wheel.pointer = at;
    out.wheel.atTheTopDown = await notch(page, title, at, -120, 'top-down');
    // To the end as a user goes there: a click on a cell in view, then a real Ctrl+End.
    await page.locator(".ex-grid [id$='-r8c2']").click({ force: true });
    await retitle();
    os(['-Title', title, '-Keys', '^{END}']);
    out.wheel.reachedTheEnd = await page.locator(".ex-grid [id$='-r999999c0']").waitFor({ timeout: 15_000 }).then(() => true, () => false);
    await page.waitForTimeout(800);
    await retitle();
    out.wheel.nearTheEndUp = await notch(page, title, at, 120, 'end-up');
    out.wheel.nearTheEndDown = await notch(page, title, at, -120, 'end-down');
    await context.close();
}

// 2. 200% page zoom, then Ctrl+End, in two orders. "as-run": a click on a cell, the zoom, then
//    Ctrl+End, with the page left where the zoom left it. "grid-in-view": the zoom first, then the
//    page scrolled so the grid's top is at the window's top (as a user scrolls to what they want
//    to use), a click on a cell, then Ctrl+End. Each reads the Focus and the last row against the
//    grid's readable box and against the browser window.
async function zoomCase(order) {
    const title = `wide-probe-zoom-${order}-${Date.now()}`;
    const context = await browser.newContext({ viewport: null });
    const { page, retitle } = await open(context, title);
    await page.bringToFront();
    const dpr0 = await page.evaluate(() => window.devicePixelRatio);
    const zoomIn = async () => {
        for (let i = 0; i < 5; i++) { await retitle(); os(['-Title', title, '-Keys', '^{ADD}']); await page.waitForTimeout(400); }
        await page.waitForTimeout(1000);
    };
    if (order === 'as-run') {
        await page.locator(".ex-grid [id$='-r0c2']").click({ force: true });
        await zoomIn();
    } else {
        await zoomIn();
        await page.evaluate(() => document.querySelector('.ex-grid').scrollIntoView({ block: 'start' }));
        await page.waitForTimeout(600);
        await page.locator(".ex-grid [id$='-r0c2']").click({ force: true });
    }
    const dpr1 = await page.evaluate(() => window.devicePixelRatio);
    await retitle();
    os(['-Title', title, '-Keys', '^{END}']);
    await page.waitForTimeout(3000);
    const read = await page.evaluate(() => {
        const root = document.querySelector('.ex-grid');
        const sc = root.querySelector('.ex-scroller');
        const box = sc.getBoundingClientRect();
        const head = root.querySelector('[role=columnheader]');
        const headBottom = head ? head.getBoundingClientRect().bottom : box.top;
        const readable = { top: headBottom, left: box.left + sc.clientLeft, bottom: box.top + sc.clientTop + sc.clientHeight, right: box.left + sc.clientLeft + sc.clientWidth };
        const win = { innerWidth: window.innerWidth, innerHeight: window.innerHeight, scrollX: window.scrollX, scrollY: window.scrollY };
        const id = root.getAttribute('aria-activedescendant') ?? '';
        const focusEl = id ? document.getElementById(id) : null;
        const focus = focusEl ? focusEl.getBoundingClientRect().toJSON() : null;
        let pinnedRight = readable.left;
        for (const p of sc.querySelectorAll('.ex-cell.ex-pinned')) pinnedRight = Math.max(pinnedRight, p.getBoundingClientRect().right);
        const last = root.querySelector("[id$='-r999999c0']");
        const lastBox = last ? last.getBoundingClientRect().toJSON() : null;
        const inWindow = (r) => !!r && r.top >= -0.5 && r.left >= -0.5 && r.bottom <= win.innerHeight + 0.5 && r.right <= win.innerWidth + 0.5;
        const probe = root.querySelector('.ex-ceiling-probe > div');
        const spacer = root.querySelector('.ex-spacer');
        return {
            activeDescendant: id.replace(/^.*-(r\d+c\d+)$/, '$1'),
            focus, readable, pinnedRight, window: win,
            focusWholeInGrid: focus ? focus.top >= readable.top - 0.5 && focus.bottom <= readable.bottom + 0.5 && focus.left >= pinnedRight - 0.5 && focus.right <= readable.right + 0.5 : false,
            focusWholeInWindow: inWindow(focus),
            lastRowPainted: !!last, lastRowText: last ? last.textContent.trim() : null, lastRow: lastBox,
            lastRowWholeInGrid: lastBox ? lastBox.top >= readable.top - 0.5 && lastBox.bottom <= readable.bottom + 0.5 : false,
            lastRowWholeInWindow: inWindow(lastBox),
            scrollTop: sc.scrollTop, scrollHeight: sc.scrollHeight, clientHeight: sc.clientHeight,
            ceiling: probe ? probe.getBoundingClientRect().height : null,
            spacerDeclared: spacer ? Number((/height: ([\d.]+)px/.exec(spacer.getAttribute('style')) ?? [])[1]) : null,
        };
    });
    // From the screen, not through the browser: a capture through the browser at 200% did not
    // match the geometry read above.
    const shot = SHOTS ? path.join(SHOTS, `D-zoom-200-ctrl-end-${order}-${LABEL}-${CHANNEL}.png`) : null;
    if (shot) {
        await retitle();
        const r = spawnSync('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', OSINPUT.replace(/os-input\.ps1$/, 'window-shot.ps1'), '-Title', title, '-Out', shot], { encoding: 'utf8' });
        if (!r.stdout.includes('ok')) throw new Error(`window-shot failed: ${r.stdout} ${r.stderr}`);
    }
    await retitle();
    os(['-Title', title, '-Keys', '^0']);
    await context.close();
    return { order, dprBefore: dpr0, dprAfter: dpr1, zoom: Number((dpr1 / dpr0).toFixed(3)), ...read, shot: shot ? `shots/${path.basename(shot)}` : null };
}
out.zoom = [];
for (const order of ['as-run', 'grid-in-view']) out.zoom.push(await zoomCase(order));

out.console = messages;
await browser.close();
console.log(JSON.stringify(out, null, 2));
