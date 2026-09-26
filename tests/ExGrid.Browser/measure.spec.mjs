import { test, expect, record, setRoundTrip } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';

// SRV-6 — ADR-0021's owed number: what the pointer reports cost on a Blazor Server
// circuit. Observational, never a gate (Definition of Done §1). Runs only when asked for,
// against the Server host, because it is about the circuit:
//
//   EXGRID_HOSTING=server EXGRID_MEASURE=pointer npx playwright test measure.spec.mjs
//
// Two numbers per round trip. The traffic of a sweep while scrolling — WebSocket frames
// the circuit carries per second, which is what "a wire round trip per row" costs in
// practice, whatever the grid's own share of it. And the band's lag: from the pointer
// crossing onto a row to the band standing on that row.

const MEASURE = process.env.EXGRID_MEASURE === 'pointer';
const ROUND_TRIPS = [0, 50, 150];

test.skip(!MEASURE, 'SRV-6 runs only with EXGRID_MEASURE=pointer');
test.skip(MEASURE && !SERVER, 'SRV-6 measures a circuit: run it with EXGRID_HOSTING=server');

const median = (values) => {
    const sorted = [...values].sort((a, b) => a - b);
    return sorted[Math.floor(sorted.length / 2)];
};

test('what the pointer reports cost on a circuit, at 0, 50 and 150 ms round trip (SRV-6)', async ({ page }, testInfo) => {
    test.setTimeout(180_000);
    const results = {};
    for (const rtt of ROUND_TRIPS) {
        await setRoundTrip(0);
        await page.goto('/stripes');
        const grid = page.locator('.ex-grid').first();
        await expect(grid).toHaveAttribute('tabindex', '0');
        await setRoundTrip(rtt);

        const rows = grid.locator('.ex-row:not(.ex-placeholder)');
        const box = async (i) => rows.nth(i).boundingBox();

        // The band's lag, row by row: the move is dispatched by the browser, the page
        // stamps it, and a frame loop in the page stamps the first frame the band stands
        // on that row. Measured in the page's own clock, so the test runner's latency
        // is not in it.
        const lags = [];
        for (let i = 1; i <= 10; i++) {
            const target = await box(i);
            await page.evaluate(() => {
                window.__movedAt = undefined;
                window.addEventListener('mousemove', () => { window.__movedAt ??= performance.now(); },
                    { capture: true, once: true });
            });
            await page.mouse.move(target.x + 80, target.y + target.height / 2);
            const lag = await page.evaluate((top) => new Promise((resolve) => {
                const tick = () => {
                    const band = document.querySelector('.ex-grid .ex-hover-row');
                    const at = band?.getBoundingClientRect().top;
                    if (window.__movedAt !== undefined && at !== undefined && Math.abs(at - top) < 1) {
                        resolve(performance.now() - window.__movedAt);
                    } else {
                        requestAnimationFrame(tick);
                    }
                };
                tick();
            }), target.y);
            lags.push(lag);
        }

        // The traffic of a sweep while scrolling: the pointer walks up and down the rows
        // a step a frame while the wheel scrolls, for three seconds.
        const client = await page.context().newCDPSession(page);
        await client.send('Network.enable');
        let sent = 0;
        let received = 0;
        client.on('Network.webSocketFrameSent', () => { sent += 1; });
        client.on('Network.webSocketFrameReceived', () => { received += 1; });
        const top = await box(0);
        const bottom = await box(12);
        const started = Date.now();
        let step = 0;
        while (Date.now() - started < 3_000) {
            const phase = step % 48;
            const t = phase < 24 ? phase / 24 : (48 - phase) / 24;
            await page.mouse.move(top.x + 80, top.y + t * (bottom.y - top.y));
            if (step % 6 === 0) {
                await page.mouse.wheel(0, phase < 24 ? 40 : -40);
            }
            await page.waitForTimeout(16);
            step += 1;
        }
        const seconds = (Date.now() - started) / 1000;
        await client.detach();

        results[`rtt${rtt}`] = {
            bandLagMs: { median: Math.round(median(lags)), max: Math.round(Math.max(...lags)) },
            framesSentPerSecond: Math.round(sent / seconds),
            framesReceivedPerSecond: Math.round(received / seconds),
        };
    }
    await setRoundTrip(0);
    record(testInfo.project.name, { 'SRV-6': results });
    testInfo.annotations.push({ type: 'SRV-6', description: JSON.stringify(results) });
    console.log(`SRV-6 ${JSON.stringify(results)}`);
});
