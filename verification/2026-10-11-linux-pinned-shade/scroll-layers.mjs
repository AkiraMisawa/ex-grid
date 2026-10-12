// Frame intervals while scrolling /sheet with column A selected, and the composited layers the
// browser holds, read through the Chrome DevTools Protocol's LayerTree domain (ADR-0206, "What it
// costs, and what is checked"). One browser, one page; prints one JSON line.
//
//     node scroll-layers.mjs <base URL> <label> <scale> [mud]
//
// The browser is launched with its display scale on the command line, as an OS sets it and as the
// browser suite's chrome-150 project does (--force-device-scale-factor, a 1280×800 window, the
// viewport left to the window), headed: run it under xvfb on a machine without a display.
// EXGRID_EXECUTABLE names the Chromium to launch, in place of the installed Chrome.
//
// Read on /sheet, whose column A is pinned: first with nothing selected but A1, then with column A
// selected (A1, Ctrl+Space). For each: the layers, counted when the tree has settled, with their
// compositing reasons; then 150 frames scrolling down 28 px a frame (a row), and 150 frames
// scrolling across 64 px a frame, each frame's interval taken from requestAnimationFrame.
const { chromium } = await import(process.env.EXGRID_PLAYWRIGHT
    ?? new URL('../../tests/ExGrid.Browser/node_modules/playwright/index.mjs', import.meta.url).href);

const [base, label, scale = '1', chromeName = ''] = process.argv.slice(2);
const args = [`--force-device-scale-factor=${scale}`, '--window-size=1280,800'];
const browser = await chromium.launch(process.env.EXGRID_EXECUTABLE
    ? { executablePath: process.env.EXGRID_EXECUTABLE, headless: false, ignoreDefaultArgs: ['--hide-scrollbars'], args }
    : { channel: 'chrome', headless: false, args });
const context = await browser.newContext({ viewport: null });
const page = await context.newPage();
const problems = [];
page.on('console', (m) => { if (m.type() === 'error' || m.type() === 'warning') problems.push(m.text()); });
page.on('pageerror', (e) => problems.push(String(e)));
const cdp = await context.newCDPSession(page);
let tree = [];
let changes = 0;
cdp.on('LayerTree.layerTreeDidChange', (event) => {
    changes++;
    if (event.layers) tree = event.layers;
});
await cdp.send('LayerTree.enable');

const frames = (count) => page.evaluate((n) => new Promise((resolve) => {
    let left = n;
    const tick = () => (--left <= 0 ? resolve() : requestAnimationFrame(tick));
    requestAnimationFrame(tick);
}), count);

/** The layers once the tree has stopped changing for ten frames. */
async function layers() {
    let seen = -1;
    for (let i = 0; i < 50 && seen !== changes; i++) {
        seen = changes;
        await frames(10);
    }
    const out = [];
    for (const layer of tree) {
        let reasons = [];
        try {
            reasons = (await cdp.send('LayerTree.compositingReasons', { layerId: layer.layerId })).compositingReasonIds ?? [];
        } catch {
            reasons = ['(unavailable)'];
        }
        out.push({ drawsContent: layer.drawsContent, width: Math.round(layer.width), height: Math.round(layer.height), reasons });
    }
    return { count: out.length, drawing: out.filter((l) => l.drawsContent).length, layers: out };
}

/** Frame intervals from requestAnimationFrame while scrolling the Sheet by `step` px a frame. */
const intervals = (axis, step, count) => page.evaluate(({ axis, step, count }) => new Promise((resolve) => {
    const scroller = document.querySelector('.ex-grid .ex-scroller');
    const out = [];
    let last;
    const tick = (now) => {
        if (last !== undefined) out.push(now - last);
        last = now;
        if (out.length === count) {
            resolve(out);
            return;
        }
        const max = axis === 'scrollTop' ? scroller.scrollHeight - scroller.clientHeight : scroller.scrollWidth - scroller.clientWidth;
        scroller[axis] = scroller[axis] + step > max ? 0 : scroller[axis] + step;
        requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
}), { axis, step, count });

const summary = (values) => {
    const sorted = [...values].sort((a, b) => a - b);
    const at = (q) => Math.round(sorted[Math.min(sorted.length - 1, Math.floor(sorted.length * q))] * 100) / 100;
    return { count: values.length, median: at(0.5), p95: at(0.95), max: at(1) };
};

async function scrolled() {
    const reset = () => page.evaluate(() => { const s = document.querySelector('.ex-grid .ex-scroller'); s.scrollTop = 0; s.scrollLeft = 0; });
    await reset();
    await frames(10);
    const down = await intervals('scrollTop', 28, 150);
    await reset();
    await frames(10);
    const across = await intervals('scrollLeft', 64, 150);
    await reset();
    await frames(10);
    return { down: { summary: summary(down), raw: down.map((v) => Math.round(v * 100) / 100) }, across: { summary: summary(across), raw: across.map((v) => Math.round(v * 100) / 100) } };
}

await page.goto(`${base}/sheet${chromeName === 'mud' ? '?chrome=mud' : ''}`);
await page.locator('#demo-interactive').waitFor({ state: 'attached' });
await page.waitForFunction(() => !document.querySelector('.ex-grid[aria-busy]'));
const grid = page.locator('.ex-grid').first();
const a1 = grid.locator("[id$='-r0c0']").first();
const box = await a1.boundingBox();
await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
await page.mouse.move(0, 0);
await frames(10);
const oneCell = { layers: await layers(), shade: await page.locator('.ex-shade-pinned').count() };
oneCell.frames = await scrolled();

await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
await page.keyboard.press('Control+Space');
await page.waitForFunction(() => document.querySelector('.ex-grid .ex-range') !== null, null, { polling: 'raf' });
await page.mouse.move(0, 0);
const columnA = { layers: await layers(), shade: await page.locator('.ex-shade-pinned').count() };
columnA.frames = await scrolled();

console.log(JSON.stringify({
    base, label, scale: Number(scale), chrome: chromeName || 'builtin', browser: browser.version(),
    devicePixelRatio: await page.evaluate(() => devicePixelRatio),
    // What rasterises here: the WebGL renderer the page is given names it (SwiftShader where no GPU is).
    renderer: await page.evaluate(() => {
        const gl = document.createElement('canvas').getContext('webgl');
        if (!gl) return 'no WebGL';
        const info = gl.getExtension('WEBGL_debug_renderer_info');
        return String(gl.getParameter(info ? info.UNMASKED_RENDERER_WEBGL : gl.RENDERER));
    }),
    oneCell, columnA, problems,
}));
await browser.close();
