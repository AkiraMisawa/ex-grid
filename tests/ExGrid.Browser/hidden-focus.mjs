// Chromium's own console warning for a focused element hidden from assistive technology: "Blocked
// aria-hidden on an element because its descendant retained focus" (the seventeenth Windows run,
// Part D). Chromium says it only while its accessibility tree is kept — a screen reader running, or
// the DevTools protocol having read the tree — and only when the element holds focus across a frame,
// so a test reads the tree once and lets the element stand a round trip on the Server host. Measured
// here, in Playwright's Chromium: Accessibility.enable alone, nothing; with the tree read once, the
// warning for a focus held 40 ms or more, and none for one given up in the same task. On WebAssembly
// the element goes in the next render, and the test is the case without the hold. The fixtures'
// console rule counts warnings from ExGrid's own code (CON-3), and this one names no file, so the
// test that watches for it asserts it by name.

/**
 * Turns on the page's accessibility tree over the DevTools protocol, reads it once, and watches the
 * console for the warning. `read()` answers what was seen once every message the page sent before it
 * has arrived; `stop()` turns the tree off again.
 */
export async function watchHiddenFocus(page) {
    const client = await page.context().newCDPSession(page);
    await client.send('Accessibility.enable');
    // Read once, as a screen reader reads it: from then on Chromium keeps the tree up to date.
    await client.send('Accessibility.getFullAXTree');
    const seen = [];
    const listener = (message) => {
        if (/aria-hidden/i.test(message.text())) {
            seen.push(message.text());
        }
    };
    page.on('console', listener);
    return {
        async read() {
            // The page answers after a frame, behind every console message it sent before it.
            await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => resolve())));
            return seen;
        },
        async stop() {
            page.off('console', listener);
            await client.send('Accessibility.disable');
            await client.detach();
        },
    };
}
