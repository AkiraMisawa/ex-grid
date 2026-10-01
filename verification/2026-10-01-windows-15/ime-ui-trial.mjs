// A trial of the tools, run before the recorded runs, on a page of the trial's own (an input, not
// ExSheet): the Japanese keyboard, the IME on, kana, Space, Space; then which top-level windows came
// and went, which windows of an IME UI Automation sees, whether the screen can be pictured, and (through
// ime-ui-trial.ps1) whether the Windows Input Experience's window can be pictured with PrintWindow
// while the candidates are up. What it found is in report.md, "The IME's candidate window".
// Run as ime-probe.mjs is (INPUT, SHOT, UIA set), from the Windows copy of tests/ExGrid.Browser.
import { chromium } from '@playwright/test';
import { spawn } from 'node:child_process';
import readline from 'node:readline';
const helper = spawn('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', process.env.INPUT], { stdio: ['pipe', 'pipe', 'inherit'] });
const lines = readline.createInterface({ input: helper.stdout }); const waiting = [];
lines.on('line', (l) => { const w = waiting.shift(); if (w) w(l.trim()); });
const first = new Promise((d) => waiting.push(d));
const input = (cmd) => new Promise((d) => { waiting.push(d); helper.stdin.write(cmd + '\n'); });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
console.log('helper', await first);
const browser = await chromium.launch({ channel: 'chrome', headless: false, args: ['--window-position=40,40', '--window-size=1200,700'] });
const page = await (await browser.newContext({ viewport: null })).newPage();
await page.setContent('<title>ime-trial-15</title><input id=f style="font-size:20px;width:400px;margin:40px">');
await page.evaluate(() => { window.__ev = []; for (const t of ['keydown', 'compositionstart', 'compositionupdate', 'compositionend', 'input']) document.addEventListener(t, (e) => window.__ev.push(`${t} key=${e.key ?? ''} kc=${e.keyCode ?? ''} comp=${e.isComposing} data=${e.data ?? ''}`), true); });
await page.bringToFront(); await page.focus('#f'); await sleep(500);
console.log(await input('front ime-trial-15'));
console.log(await input('japanese ime-trial-15'));
console.log(await input('imestate ime-trial-15'));
console.log(await input('imeon'));
const w0 = (await input('windows')).split(' ;; ');
console.log(await input('imestate ime-trial-15'));
for (const k of ['k', 'a', 'n', 'a', '{SPACE}', '{SPACE}']) { console.log(k, await input(`type 0 ${k}`)); await sleep(400); }
const w1 = (await input('windows')).split(' ;; ');
console.log('new windows:', w1.filter((w) => !w0.includes(w)));
console.log('gone:', w0.filter((w) => !w1.includes(w)));
console.log(await input('imeui'));
const { execFileSync } = await import('node:child_process');
console.log(execFileSync('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', process.env.UIA], { encoding: 'utf8' }));
const g = await page.evaluate(() => ({ sx: screenX, sy: screenY, ow: outerWidth, oh: outerHeight, dpr: devicePixelRatio }));
console.log(await input(`screen ${Math.round(g.sx * g.dpr)} ${Math.round(g.sy * g.dpr)} ${Math.round(g.ow * g.dpr)} ${Math.round(g.oh * g.dpr)} ${process.env.SHOT}`));
console.log(await page.evaluate(() => [document.querySelector('#f').value, window.__ev.join('\n')]));
for (const k of ['{ESC}', '{ESC}', '{ESC}']) { await input(`type 0 ${k}`); await sleep(300); }
console.log(await input('imeoff'));
console.log(await input('imestate ime-trial-15'));
console.log(await page.evaluate(() => document.querySelector('#f').value));
helper.stdin.write('quit\n'); await browser.close();
