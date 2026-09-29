// Calibration for end-probe.mjs: where, in the grid's vertical scrollbar gutter on /wide, a real
// mouse press grabs the thumb. For each offset below the scroller's top: a loaded /wide at the top, the
// real mouse pressed there, moved 60 CSS px down in steps, released; the scroll offset read before,
// while held and after. A thumb drag moves the offset by about 60/585 of the scroll range; a press
// on the track pages by the view's height. Also whether anything is painted in the gutter.
//
//     set EXGRID_CHANNEL=chrome & set INPUT=\\...\input-server.ps1 & node thumb-calibrate.mjs http://localhost:5299
import { chromium } from '@playwright/test';
import { spawn } from 'node:child_process';
import readline from 'node:readline';

const BASE = process.argv[2] ?? 'http://localhost:5299';
const CHANNEL = process.env.EXGRID_CHANNEL ?? 'chrome';
const helper = spawn('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', process.env.INPUT], { stdio: ['pipe', 'pipe', 'pipe'] });
const lines = readline.createInterface({ input: helper.stdout });
const waiting = [];
lines.on('line', (l) => { const w = waiting.shift(); if (w) w(l.trim()); });
const first = new Promise((done) => waiting.push(done));
const input = (cmd) => new Promise((done, fail) => { waiting.push((l) => (l.startsWith('ok') ? done(l) : fail(new Error(`${cmd}: ${l}`)))); helper.stdin.write(cmd + '\n'); });
if ((await first) !== 'ready') throw new Error('input-server did not start');

const browser = await chromium.launch({ channel: CHANNEL, headless: false, args: ['--window-position=40,40', '--window-size=1600,1000'] });
const out = { channel: CHANNEL, base: BASE, browserVersion: browser.version(), tries: [] };
const page = await (await browser.newContext({ viewport: null })).newPage();
await page.goto(`${BASE}/wide`);
await page.locator('#demo-interactive').waitFor({ state: 'attached', timeout: 60_000 });
await page.waitForFunction(() => !document.querySelector('.ex-grid[aria-busy]'), null, { timeout: 60_000 });
await page.evaluate(() => { document.title = 'thumb-calibrate'; });
await input('front thumb-calibrate');
const g = await page.evaluate(() => ({ sx: screenX, sy: screenY, ow: outerWidth, oh: outerHeight, iw: innerWidth, ih: innerHeight, dpr: devicePixelRatio }));
const border = (g.ow - g.iw) / 2;
const ox = (g.sx + border) * g.dpr, oy = (g.sy + (g.oh - g.ih) - border) * g.dpr;
const at = (x, y) => [Math.round(ox + x * g.dpr), Math.round(oy + y * g.dpr)];
const box = await page.evaluate(() => { const sc = document.querySelector('.ex-grid .ex-scroller'); const r = sc.getBoundingClientRect(); return { left: r.left, top: r.top, right: r.right, bottom: r.bottom, clientWidth: sc.clientWidth, clientHeight: sc.clientHeight, scrollHeight: sc.scrollHeight }; });
out.box = box;
const x = box.left + box.clientWidth + (box.right - box.left - box.clientWidth) / 2;
for (const off of [1, 3, 5, 8, 12, 16, 20]) {
    await page.evaluate(() => { document.querySelector('.ex-grid .ex-scroller').scrollTop = 0; });
    await page.waitForTimeout(500);
    const [px, py] = at(x, box.top + off);
    const [, py2] = at(x, box.top + off + 60);
    const before = await page.evaluate(() => document.querySelector('.ex-grid .ex-scroller').scrollTop);
    const answer = await input(`drag ${px} ${py} ${px} ${py2} 6 40`);
    await page.waitForTimeout(600);
    const after = await page.evaluate(() => document.querySelector('.ex-grid .ex-scroller').scrollTop);
    out.tries.push({ offsetCss: off, screen: [px, py, py2], before, after, moved: after - before, answer });
}
console.log(JSON.stringify(out, null, 2));
await browser.close();
helper.stdin.write('quit\n');
