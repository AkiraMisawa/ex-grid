import { test, expect, alterPage, circuitQuiet, record, roundTrip, setRoundTrip } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';
import { candidates, clickCell, editor, openSheet, sheet } from './sheet-helpers.mjs';

// SH-19 (Definition of Done §27): completion's keystroke-to-list time on a circuit, measured on the
// Server host with 150 ms injected between the browser and the host (latency-proxy.mjs), the case
// decided with the user on 2026-10-11. Recorded and never gated (§1, AGENTS.md), so this runs only
// when asked for:
//
//   EXGRID_HOSTING=server EXGRID_MEASURE=sheet npx playwright test measure-sheet.spec.mjs --project=chrome
//
// SH-19's other half, the recalculation at a large Sheet, is measured on CoreCLR by
// dotnet/tests/ExSheet.Engine.Tests' Measurements.
//
// Every time is taken in the page's own clock (performance.now), so the runner's latency is in none
// of it. The keystroke is its keydown's timeStamp, read by a capture-phase listener on the window,
// which hears it before the grid's own listener on the instance root does (ADR-0010). The list is
// the first change to the DOM after which the completion list shows candidates for the text typed —
// every candidate begins with it, and the list is not the one shown when the key was armed — kept
// by a MutationObserver at the end of the task that made the change, with the animation frame after
// it: the frame that paints the list. Both are installed through alterPage, which takes them away
// as the test ends (ADR-0056). Each run's time is kept beside the median, so that a spread of two
// clusters shows as two.
//
// The same keystrokes are measured with no round trip too, on the same host: the host's own share
// of the time, for comparison. Only the 150 ms case is SH-19's.
//
// Each key is measured twelve times at each round trip; EXGRID_MEASURE_RUNS=<n> sets another
// count, which changes what is recorded, so a record names it when it is not twelve. The median of
// an even count is the mean of the two middle runs.

const MEASURE = process.env.EXGRID_MEASURE;
const RUNS = Number(process.env.EXGRID_MEASURE_RUNS ?? 12);

test.skip(MEASURE !== 'sheet', 'the measurement runs only with EXGRID_MEASURE=sheet');
test.skip(MEASURE === 'sheet' && !SERVER, 'SH-19\'s completion is measured on a circuit: run it with EXGRID_HOSTING=server');

const median = (values) => {
    const sorted = [...values].sort((a, b) => a - b);
    const n = sorted.length;
    return n === 0 ? null : (sorted[Math.floor((n - 1) / 2)] + sorted[Math.floor(n / 2)]) / 2;
};

/** The median, the best and the worst of a number over the runs, to a tenth of a millisecond. */
function spread(runs, pick) {
    const values = runs.map(pick).filter((v) => typeof v === 'number' && Number.isFinite(v));
    const tenth = (v) => Math.round(v * 10) / 10;
    return values.length === 0 ? null : { median: tenth(median(values)), min: tenth(Math.min(...values)), max: tenth(Math.max(...values)) };
}

/** The probe: the keystroke's time, and the time the list shows what was typed and the frame after it. */
function probe() {
    const root = document.querySelector('.ex-grid:has(> .ex-formula-bar)');
    const labels = () => [...root.querySelectorAll('.ex-completion [role=option]')].map((option) => option.textContent.trim());
    const p = { watch: null };
    const onKey = (event) => {
        const w = p.watch;
        if (w !== null && w.keyAt === null && event.key === w.key) {
            w.keyAt = event.timeStamp;
        }
    };
    window.addEventListener('keydown', onKey, true);
    const observer = new MutationObserver(() => {
        const w = p.watch;
        if (w === null || w.keyAt === null || w.listAt !== null) {
            return;
        }
        const now = labels();
        if (now.length > 0 && now.every((label) => label.toUpperCase().startsWith(w.prefix)) && now.join('|') !== w.before) {
            w.listAt = performance.now();
            w.candidates = now.length;
            w.first = now[0];
            requestAnimationFrame(() => { w.frameAt = performance.now(); });
        }
    });
    observer.observe(root, { subtree: true, childList: true, characterData: true, attributes: true });
    p.arm = (key, prefix) => {
        p.watch = { key, prefix, before: labels().join('|'), keyAt: null, listAt: null, frameAt: null, candidates: 0, first: null };
    };
    window.__sh19 = p;
    return () => {
        observer.disconnect();
        window.removeEventListener('keydown', onKey, true);
        delete window.__sh19;
    };
}

/**
 * One keystroke measured: armed once the circuit is quiet, so nothing earlier is still on the wire,
 * and read once the frame after the list has run.
 */
async function measureKey(page, key, prefix) {
    await circuitQuiet();
    await page.evaluate(({ key, prefix }) => window.__sh19.arm(key, prefix), { key, prefix });
    await page.keyboard.type(key);
    await page.waitForFunction(() => window.__sh19.watch.frameAt !== null, null, { polling: 10, timeout: 30_000 });
    return page.evaluate(() => {
        const w = window.__sh19.watch;
        return {
            keyToListMs: w.listAt - w.keyAt,
            keyToFrameMs: w.frameAt - w.keyAt,
            candidates: w.candidates,
            first: w.first,
        };
    });
}

test('SH-19: completion\'s keystroke-to-list time on the Server host, with 150 ms injected and with none @blazor', async ({ page }, testInfo) => {
    test.setTimeout(1_800_000);
    await openSheet(page);
    const grid = sheet(page);
    await alterPage(page, probe);

    const results = {};
    for (const rtt of [0, 150]) {
        await setRoundTrip(rtt);
        expect(await roundTrip()).toBe(rtt);
        const opens = [];
        const narrows = [];
        for (let run = 0; run < RUNS; run++) {
            // An empty cell on screen beside /sheet's data: = opens the Cell Editor, and its first
            // letter opens the list.
            await clickCell(grid, 'F2');
            await page.keyboard.type('=');
            await expect(editor(grid)).toHaveValue('=');
            opens.push(await measureKey(page, 'S', 'S'));
            await expect(editor(grid)).toHaveValue('=S');
            // The list open, a second letter narrows it.
            narrows.push(await measureKey(page, 'U', 'SU'));
            await expect(candidates(grid).first()).toHaveText('SUBSTITUTE');
            // Escape closes the list, and a second cancels the edit (ADR-0051).
            await page.keyboard.press('Escape');
            await expect(candidates(grid)).toHaveCount(0);
            await page.keyboard.press('Escape');
            await expect(editor(grid)).toHaveCount(0);
        }
        for (const [name, runs] of [['the list opens (=S)', opens], ['the list narrows (=SU)', narrows]]) {
            results[`SH-19 completion, ${name}, Server host, ${rtt} ms round trip`] = {
                runs: runs.length,
                keyToListMs: spread(runs, (r) => r.keyToListMs),
                keyToFrameMs: spread(runs, (r) => r.keyToFrameMs),
                keyToFrameMsByRun: runs.map((r) => Math.round(r.keyToFrameMs * 10) / 10),
                candidates: runs.at(-1).candidates,
                first: runs.at(-1).first,
            };
        }
    }
    record(testInfo.project.name, results);
    console.log(`SH-19 ${JSON.stringify(results, null, 1)}`);
});
