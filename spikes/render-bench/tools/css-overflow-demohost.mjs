// The candidate on the real grid (docs/research/css-decided-overflow.md): loads a running
// DemoHost page, injects a candidate stylesheet, and gives every cell the C# hashed back
// its real text (from its aria-label) — what the markup would be if C# stopped deciding.
// Then selects across those cells and screenshots, to see the selection Overlay and the
// Focus outline paint over the CSS run. Spike code.
//
//   xvfb-run -a node tools/css-overflow-demohost.mjs http://localhost:5310/features [candidate.css]
import { createRequire } from 'node:module';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const require = createRequire(path.join(here, '../../../tests/ExGrid.Browser/package.json'));
const { chromium } = require('@playwright/test');
const url = process.argv[2] ?? 'http://localhost:5310/features';
const candidate = process.argv[3] ?? 'candidate.css';

const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium', headless: false });
const page = await browser.newPage({ viewport: { width: 1400, height: 900 }, deviceScaleFactor: 1 });
const messages = [];
page.on('console', (m) => { if (m.type() !== 'debug') messages.push(`${m.type()}: ${m.text()}`); });
page.on('pageerror', (e) => messages.push(`pageerror: ${e.message}`));
await page.goto(url);
const grid = page.locator('.ex-grid').first();
await grid.locator('.ex-row').first().waitFor();

await page.addStyleTag({ content: fs.readFileSync(path.join(here, '../css-overflow', candidate), 'utf8') });
const rewritten = await grid.evaluate((g) => {
    const cells = [...g.querySelectorAll('.ex-cell[aria-label]')];
    for (const c of cells) { c.textContent = c.getAttribute('aria-label'); c.removeAttribute('aria-label'); }
    return cells.map((c) => c.id);
});
await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));

const hashedNow = await grid.evaluate((g) => [...g.querySelectorAll('.ex-cell-numeric')]
    .filter((el) => getComputedStyle(el, '::after').display === 'block').map((el) => el.id));

// Select the Notional cell of row 0 and extend over the narrowed column (as CP-5 does).
await grid.locator("[id$='r0c2']").click({ force: true });
await page.keyboard.press('Shift+ArrowRight');
await page.keyboard.press('Shift+ArrowDown');
await page.keyboard.press('Shift+ArrowDown');
await page.evaluate(() => new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r))));
const outDir = path.join(here, '../results/css-overflow');
const shot = path.join(outDir, 'demohost-' + candidate.replace('.css', '') + '.png');
await grid.screenshot({ path: shot });

const report = {
    url, candidate,
    cellsCSharpHashed: rewritten.length,
    cellsCssHashedAfterRewrite: hashedNow.length,
    sameCells: rewritten.length === hashedNow.length && rewritten.every((id) => hashedNow.includes(id)),
    // What the C# estimate hashed that CSS shows, or the reverse (the seam in question 5).
    cSharpOnly: rewritten.filter((id) => !hashedNow.includes(id)),
    cssOnly: hashedNow.filter((id) => !rewritten.includes(id)),
    screenshot: path.relative(path.join(here, '..'), shot),
    console: messages,
};
console.log(JSON.stringify(report, null, 2));
await browser.close();
