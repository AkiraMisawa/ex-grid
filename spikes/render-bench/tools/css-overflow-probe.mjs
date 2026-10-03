// Feasibility probe for a CSS-decided #### (docs/research/css-decided-overflow.md).
// Loads css-overflow/probe.html — the product's ex-grid.css plus candidate.css — in a
// headed Chromium and checks, against layout geometry, what the scroll-timeline switch
// decides. Spike code: prints a JSON report and writes it to results/.
//
//   xvfb-run -a node tools/css-overflow-probe.mjs [executablePath]
//
// Playwright is borrowed from tests/ExGrid.Browser (run `npm ci` there first).
import { createRequire } from 'node:module';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const require = createRequire(path.join(here, '../../../tests/ExGrid.Browser/package.json'));
const { chromium } = require('@playwright/test');

const exe = process.argv[2] ?? '/opt/pw-browsers/chromium';
const candidate = process.env.CANDIDATE ?? 'candidate.css';
const url = pathToFileURL(path.join(here, '../css-overflow/probe.html')).href + '?css=' + candidate;

const browser = await chromium.launch({ executablePath: exe, headless: false });
const page = await browser.newPage({ viewport: { width: 1280, height: 1000 }, deviceScaleFactor: 1 });
const consoleMessages = [];
page.on('console', (m) => consoleMessages.push(`${m.type()}: ${m.text()}`));
page.on('pageerror', (e) => consoleMessages.push(`pageerror: ${e.message}`));
await page.goto(url);
await page.evaluate(() => document.fonts.ready);

const report = { browser: browser.version(), candidate, url, when: new Date().toISOString() };

// ---- A. The detection boundary, against geometry, across typography ----------------
const texts = ['1,234.56', '123,456,789,012.50', '-98.7%', '2026-10-02', '(1,234,567.00)', '¥1,234,567'];
const variants = [
    { name: 'regular', rowClass: '', cls: '' },
    { name: 'total row (600)', rowClass: 'ex-row-total', cls: '' },
    { name: 'consumer bold 700', rowClass: '', cls: 'probe-bold' },
    { name: 'consumer 17px', rowClass: '', cls: 'probe-big' },
    { name: 'consumer letter-spacing 1px', rowClass: '', cls: 'probe-spaced' },
    { name: 'consumer serif family', rowClass: '', cls: 'probe-serif' },
    { name: 'stale (italic)', rowClass: '', cls: 'ex-state-stale' },
    { name: 'pinned', rowClass: '', cls: '', pinned: true },
];

report.boundary = await page.evaluate(async ({ texts, variants }) => {
    const out = [];
    for (const v of variants) {
        for (const text of texts) {
            probe.clear();
            // The text's own width in this typography, from a cell wide enough to hold it.
            const wide = probe.row([{ text, width: 600, cls: v.cls, pinned: v.pinned }], v.rowClass).firstChild;
            const textW = probe.geometry(wide).text;
            probe.clear();
            const cells = [];
            // Widths from 4px too narrow to 4px to spare, in 1/8 px steps, around text + 2×8px padding.
            for (let d = -4; d <= 4.0001; d += 0.125) cells.push({ text, width: textW + 16 + d, cls: v.cls, pinned: v.pinned });
            // One row per cell, so the pinned variant's sticky cells do not overlap.
            const els = cells.map((c) => probe.row([c], v.rowClass).firstChild);
            await probe.frames();
            let maxUndetected = -Infinity, minDetected = Infinity, falseHash = 0, missed = 0;
            for (const el of els) {
                const g = probe.geometry(el);
                const h = probe.hashed(el);
                if (h) minDetected = Math.min(minDetected, g.overflow);
                else maxUndetected = Math.max(maxUndetected, g.overflow);
                if (h && g.overflow <= 0) falseHash++;
                if (!h && g.overflow > 0) missed++;
            }
            out.push({ variant: v.name, text, textW: +textW.toFixed(3), samples: els.length, minDetectedOverflow: +minDetected.toFixed(4), maxUndetectedOverflow: +maxUndetected.toFixed(4), hashedWhileFitting: falseHash, shownWhileOverflowing: missed });
        }
    }
    return out;
}, { texts, variants });

// What happens to an overflow too small to be detected: is any of the value cut?
// The clip edge is the padding box, so an undetected sub-pixel overflow runs into the
// 8px end padding and is painted whole. Measured: text right edge vs the cell's border box.
report.subpixel = await page.evaluate(async () => {
    probe.clear();
    const text = '123,456,789,012.50';
    const textW = probe.geometry(probe.row([{ text, width: 600 }]).firstChild).text;
    probe.clear();
    const res = [];
    const ds = [0.01, 0.1, 0.25, 0.5, 0.75, 0.99, 1.0, 1.01];
    const els = ds.map((d) => probe.row([{ text, width: textW + 16 - d }]).firstChild);
    await probe.frames();
    for (const [k, d] of ds.entries()) {
        const el = els[k];
        const range = document.createRange(); range.selectNodeContents(el);
        const tr = range.getBoundingClientRect(), cr = el.getBoundingClientRect();
        res.push({ overflowPx: d, hashed: probe.hashed(el), scrollWidth: el.scrollWidth, clientWidth: el.clientWidth, textRightToCellRight: +(cr.right - tr.right).toFixed(3) });
    }
    return res;
});

// ---- B. The run of hashes: as many as fit, no partial glyph, no ellipsis ------------
report.fill = await page.evaluate(async () => {
    probe.clear();
    const res = [];
    const made = [];
    for (const [cls, rowClass] of [['', ''], ['probe-bold', ''], ['', 'ex-row-total'], ['probe-big', ''], ['probe-spaced', '']])
        for (const width of [40, 57, 80, 101, 133])
            made.push({ cls, rowClass, width, el: probe.row([{ text: '123,456,789,012.50', width, cls }], rowClass).firstChild });
    await probe.frames();
    for (const { cls, rowClass, width, el } of made) {
        {
            const cs = getComputedStyle(el);
            // A replica of the ::after, as a real element, so its line can be read with a Range.
            const rep = document.createElement('div');
            rep.style.cssText = `position:absolute;inset:0;box-sizing:border-box;padding:${cs.padding};overflow:hidden;white-space:normal;word-break:break-all;`;
            rep.textContent = '#'.repeat(128);
            el.appendChild(rep);
            const tn = rep.firstChild;
            const r = document.createRange();
            let firstTop = null, count = 0, lastRight = 0, firstLeft = 0;
            for (let i = 0; i < 128; i++) {
                r.setStart(tn, i); r.setEnd(tn, i + 1);
                const b = r.getBoundingClientRect();
                if (firstTop === null) { firstTop = b.top; firstLeft = b.left; }
                if (Math.abs(b.top - firstTop) > 1) break;
                count++; lastRight = b.right;
            }
            // # width in this typography, the way C# charges it.
            const m = document.createElement('span'); m.textContent = '#'.repeat(100); el.appendChild(m);
            m.style.position = 'absolute'; m.style.whiteSpace = 'nowrap';
            const hashW = m.getBoundingClientRect().width / 100;
            m.remove(); rep.remove();
            const cr = el.getBoundingClientRect();
            const contentL = cr.left + parseFloat(cs.paddingLeft), contentR = cr.right - parseFloat(cs.paddingRight);
            res.push({ typography: cls || rowClass || 'regular', width, hashed: probe.hashed(el), hashesOnFirstLine: count,
                expectedFloor: Math.max(1, Math.floor((contentR - contentL) / hashW)),
                runInsideContentBox: firstLeft >= contentL - 0.01 && lastRight <= contentR + 0.01,
                textOverflow: cs.textOverflow });
        }
    }
    return res;
});
// Painted evidence of the fill: a screenshot of hashed cells in each typography.
await page.evaluate(() => {
    probe.clear();
    for (const [cls, rowClass] of [['', ''], ['probe-bold', ''], ['', 'ex-row-total'], ['probe-big', ''], ['probe-spaced', ''], ['ex-state-error', ''], ['ex-tone-negative', '']])
        probe.row([40, 57, 80, 101, 133, 180].map((w) => ({ text: '123,456,789,012.50', width: w, cls }))
            .concat([{ text: '1,234.56', width: 120, cls }]), rowClass);
});
const shotDir = path.join(here, '../results/css-overflow');
fs.mkdirSync(shotDir, { recursive: true });
await page.evaluate(() => probe.frames());
await page.locator('.ex-viewport').screenshot({ path: path.join(shotDir, 'fill-' + candidate.replace('.css', '') + '.png') });

// ---- C. Accessible name, copy surface and selection-relevant geometry ----------------
await page.evaluate(async () => {
    probe.clear();
    probe.row([{ text: '1,234.56', width: 120, id: 'fits' }, { text: '123,456,789,012.50', width: 80, id: 'hashed' }]);
    await probe.frames();
});
const axName = async (id) => {
    const s = await page.locator('#' + id).ariaSnapshot();
    return s.trim();
};
report.accessibility = {
    fits: await axName('fits'),
    hashedWithAltText: await axName('hashed'),
    hashedInnerText: await page.locator('#hashed').evaluate((el) => el.innerText),
    hashedTextContent: await page.locator('#hashed').evaluate((el) => el.textContent),
};
// The same without the `/ ""` alt text, to show it is load-bearing.
await page.addStyleTag({ content: '.ex-cell.ex-cell-numeric::after { content: "########"; }' });
report.accessibility.hashedWithoutAltText = await axName('hashed');
// And through CDP's own tree, which is what a screen reader is handed.
const cdp = await page.context().newCDPSession(page);
await cdp.send('Accessibility.enable');
const tree = await cdp.send('Accessibility.getFullAXTree');
report.accessibility.cdpGridcellNamesWithoutAlt = tree.nodes.filter((n) => n.role?.value === 'gridcell').map((n) => n.name?.value);
await page.reload();
await page.evaluate(async () => {
    probe.row([{ text: '1,234.56', width: 120, id: 'fits' }, { text: '123,456,789,012.50', width: 80, id: 'hashed' }]);
    await probe.frames();
});
const tree2 = await cdp.send('Accessibility.getFullAXTree');
report.accessibility.cdpGridcellNamesWithAlt = tree2.nodes.filter((n) => n.role?.value === 'gridcell').map((n) => n.name?.value);

// ---- P8 / UX-6: what the existing invariant test would read ---------------------------
report.ux6 = await page.evaluate(() => {
    const el = document.getElementById('hashed');
    const fits = document.getElementById('fits');
    const anims = el.getAnimations();
    return {
        animationName: getComputedStyle(el).animationName,
        animationsPerCell: anims.length,
        timelineCurrentTimeHashed: String(anims[0]?.timeline?.currentTime ?? null),
        timelineCurrentTimeFits: String(fits.getAnimations()[0]?.timeline?.currentTime ?? null),
        playStateFits: fits.getAnimations()[0]?.playState,
        playStateHashed: anims[0]?.playState,
    };
});

// ---- Forced colors (UX-7): does the hiding survive the browser forcing colours? ------
// A hashed cell and a cell painting the same hashes as text (today's C# output) are
// compared by how many pixels each inks: if the digits showed through, the CSS cell
// would ink far more than the literal one.
await page.emulateMedia({ forcedColors: 'active' });
await page.evaluate(async () => {
    probe.clear();
    probe.row([{ text: '123,456,789,012.50', width: 80, id: 'css' }, { text: '#####', width: 80, id: 'literal' }]);
    probe.row([{ text: '123,456,789,012.50', width: 80, id: 'css2', cls: 'ex-state-error' }, { text: '#####', width: 80, id: 'literal2', cls: 'ex-state-error' }]);
    await probe.frames();
});
const { decodePng } = await import('./png.mjs');
const forcedShot = await page.screenshot();
await page.locator('.ex-viewport').screenshot({ path: path.join(shotDir, 'forced-colors-' + candidate.replace('.css', '') + '.png'), clip: undefined });
const img = decodePng(forcedShot);
const inkOf = async (id) => {
    const r = await page.locator('#' + id).boundingBox();
    const bg = (() => { const i = (Math.floor(r.y + 1) * img.width + Math.floor(r.x + 1)) * 4; return [img.data[i], img.data[i + 1], img.data[i + 2]]; })();
    let n = 0;
    for (let y = Math.ceil(r.y); y < Math.floor(r.y + r.height); y++)
        for (let x = Math.ceil(r.x); x < Math.floor(r.x + r.width); x++) {
            const i = (y * img.width + x) * 4;
            if (Math.abs(img.data[i] - bg[0]) + Math.abs(img.data[i + 1] - bg[1]) + Math.abs(img.data[i + 2] - bg[2]) > 120) n++;
        }
    return n;
};
report.forcedColors = {
    hashedInk: await inkOf('css'), literalHashesInk: await inkOf('literal'),
    hashedErrorInk: await inkOf('css2'), literalHashesErrorInk: await inkOf('literal2'),
    hashed: await page.evaluate(() => probe.hashed(document.getElementById('css'))),
};
await page.emulateMedia({ forcedColors: 'none' });

report.console = consoleMessages;
await browser.close();

const out = JSON.stringify(report, null, 2);
console.log(out);
fs.writeFileSync(path.join(shotDir, 'probe-' + candidate.replace('.css', '') + '-' + new Date().toISOString().replace(/[:.]/g, '-') + '.json'), out);
