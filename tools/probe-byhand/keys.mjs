// Does a key sent through the OS input queue reach the browser's own accelerators, and does a page's
// preventDefault keep it from them? Does a real browser zoom reach devicePixelRatio and matchMedia?
// Does an IME compose? Run on a Windows desktop: node keys.mjs <channel>.
import { chromium } from 'playwright-core';
import { execFileSync } from 'node:child_process';

const channel = process.argv[2] ?? 'chrome';
const ime = process.argv.includes('--ime');
const inject = (...chords) => execFileSync('powershell', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', new URL('./inject.ps1', import.meta.url).pathname.replace(/^\/(\w:)/, '$1'), ...chords], { encoding: 'utf8' }).trim();

const PROBE = `<!doctype html><title>probe</title><input id=f style="font-size:20px;width:400px"><pre id=o></pre><script>
window.log = []; window.prevent = false;
const say = e => log.push(e);
for (const t of ['keydown', 'keyup']) addEventListener(t, e => { say(t + ' ' + e.key + ' code=' + e.code + ' kc=' + e.keyCode + ' ctrl=' + e.ctrlKey + ' alt=' + e.altKey + ' composing=' + e.isComposing); if (window.prevent) e.preventDefault(); }, true);
for (const t of ['compositionstart', 'compositionupdate', 'compositionend']) addEventListener(t, e => say(t + ' ' + JSON.stringify(e.data)), true);
addEventListener('blur', () => say('window blur'));
addEventListener('focus', () => say('window focus'));
document.addEventListener('visibilitychange', () => say('visibility ' + document.visibilityState));
let mq; const watch = () => { mq = matchMedia('(resolution: ' + devicePixelRatio + 'dppx)'); mq.addEventListener('change', () => { say('dpr change -> ' + devicePixelRatio); watch(); }, { once: true }); }; watch();
</script>`;

const browser = await chromium.launch({ channel, headless: false, args: ['--window-size=1000,700'] });
const context = await browser.newContext({ viewport: null });
const other = await context.newPage();
await other.setContent('<title>other</title>other');
const page = await context.newPage();
await page.setContent(PROBE);
await page.bringToFront();
await page.click('#f');
const read = async label => {
    const state = await page.evaluate(() => ({ log: log.splice(0), focus: document.hasFocus(), vis: document.visibilityState, dpr: devicePixelRatio, value: document.getElementById('f').value, active: document.activeElement?.id }));
    console.log(`--- ${channel}: ${label}\n${JSON.stringify(state, null, 1)}`);
    return state;
};
await read('start');

for (const [label, chord] of [['Ctrl+1', 'ctrl+1'], ['Ctrl+F1', 'ctrl+f1'], ['Alt alone', 'alt'], ['F10', 'f10']]) {
    for (const prevent of [false, true]) {
        await page.bringToFront(); await page.click('#f');
        await page.evaluate(p => { window.prevent = p; }, prevent);
        console.log(inject(chord));
        await page.waitForTimeout(800);
        await read(`${label}, preventDefault=${prevent}`);
        console.log(inject('escape'));
    }
}

// A real browser zoom (Ctrl+= is the browser's), then back with Ctrl+0... Ctrl+minus to undo.
await page.bringToFront(); await page.click('#f');
await page.evaluate(() => { window.prevent = false; });
console.log(inject('ctrl+plus', 'ctrl+plus'));
await page.waitForTimeout(800);
await read('Ctrl+= twice (browser zoom)');
console.log(inject('ctrl+minus', 'ctrl+minus'));
await page.waitForTimeout(800);
await read('Ctrl+- twice');

if (ime) {
    await page.bringToFront(); await page.click('#f');
    console.log(inject('kanji'));
    console.log(inject('n', 'i', 'h', 'o', 'n'));
    await page.waitForTimeout(500);
    let s = await read('IME: kanji, n i h o n');
    if (!s.log.some(l => l.startsWith('compositionstart'))) {
        await page.fill('#f', '');
        console.log(inject('alt+grave'));
        console.log(inject('n', 'i', 'h', 'o', 'n'));
        await page.waitForTimeout(500);
        await read('IME: alt+grave, n i h o n');
    }
    console.log(inject('space'));
    await page.waitForTimeout(500);
    await read('IME: space');
    console.log(inject('enter'));
    await page.waitForTimeout(500);
    await read('IME: enter');
}
await browser.close();
