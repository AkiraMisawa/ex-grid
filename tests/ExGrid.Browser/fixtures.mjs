import { test as base, expect } from '@playwright/test';
import fs from 'node:fs';
import { HOST_LOG, LATENCY_CONTROL_URL, SERVER } from './hosting.mjs';

// What every spec shares: the console capture that CON-1/2/3/6 are read from, and the
// two records a run writes — console.json and metrics.json — under one directory.

// Where a run's records go. This path used to be the literal verification/2026-09-01,
// so every layer-3 run on every machine overwrote that one dated record: a record whose
// results.md names macOS ended up holding WSL2 timings, with nothing in the file to say
// so, and the second browser project overwrote the first. A structural count (DOM-5) is
// comparable across machines; a timing is not. So the directory carries the day and the
// platform, and each project writes under its own key.
export const RECORD_DIR = (() => {
    // The operator's day, not UTC: the directory names where and when this ran, and an
    // evening run west of Greenwich filing itself under tomorrow would be a record that
    // lies about the second half of that.
    const day = new Date().toLocaleDateString('en-CA');
    const wsl = process.platform === 'linux'
        && fs.existsSync('/proc/version')
        && fs.readFileSync('/proc/version', 'utf8').toLowerCase().includes('microsoft');
    const platform = process.platform === 'darwin' ? 'macos'
        : process.platform === 'win32' ? 'windows'
        : wsl ? 'linux-wsl2'
        : process.platform;
    return `../../verification/${day}-${platform}`;
})();

// Several tests write each file and the read-modify-write below is not atomic. It is
// safe only because playwright.config.mjs pins `workers: 1, fullyParallel: false` —
// which it does for its own reason, that the suite changes the page zoom. If that ever
// relaxes, this needs a lock, and the symptom will be a key missing from a record.
function update(file, change) {
    fs.mkdirSync(RECORD_DIR, { recursive: true });
    const path = `${RECORD_DIR}/${file}`;
    const all = fs.existsSync(path) ? JSON.parse(fs.readFileSync(path, 'utf8')) : {};
    change(all);
    fs.writeFileSync(path, `${JSON.stringify(all, null, 2)}\n`);
}

/** Merges observational numbers into metrics.json under the project's key. */
export function record(project, entries) {
    update('metrics.json', (all) => {
        all[project] = { ...all[project], ...entries };
    });
}

// Whose a console message is (CON-3). ExGrid's own code is what the packages serve —
// ex-grid.js and anything else under _content/ExGrid* — and a message that names
// itself: the module prefixes every write with [ex-grid], and a .NET log line carries
// its category, so one from inside the component says ExGrid.Something. The demo
// pages and their hosts live in ExGrid.DemoPages and ExGrid.DemoHost and are not the
// product, so they do not count — nor are the pages' own stylesheet and script.
function isExGrids(message) {
    return /\/_content\/ExGrid(?!\.DemoPages\b)/.test(message.url)
        || /\[ex-grid\]/.test(message.text)
        || /\bExGrid\.(?!DemoHost\b|DemoPages\b)[A-Z]/.test(message.text);
}

/**
 * Sets the round trip between the browser and the Blazor Server host (latency-proxy.mjs).
 * Answers false on WebAssembly, where there is no circuit to delay: a test that needs a
 * round trip still runs there, as the case without one.
 */
export async function setRoundTrip(ms) {
    if (!SERVER) {
        return false;
    }
    const response = await fetch(`${LATENCY_CONTROL_URL}/?rtt=${ms}`, { method: 'POST' });
    if (!response.ok) {
        throw new Error(`the latency proxy refused rtt=${ms}: ${response.status}`);
    }
    return true;
}

// What the Server host logged while one test ran (CON-6): the file the host appends to,
// read from where it stood when the test began.
const hostLogSize = () => (fs.existsSync(HOST_LOG) ? fs.statSync(HOST_LOG).size : 0);
function hostLogSince(offset) {
    if (!SERVER || !fs.existsSync(HOST_LOG)) {
        return [];
    }
    const handle = fs.openSync(HOST_LOG, 'r');
    try {
        const length = fs.fstatSync(handle).size - offset;
        if (length <= 0) {
            return [];
        }
        const buffer = Buffer.alloc(length);
        fs.readSync(handle, buffer, 0, length, offset);
        return buffer.toString('utf8').split('\n').filter((line) => line.length > 0);
    } finally {
        fs.closeSync(handle);
    }
}

export const test = base.extend({
    // A refusal the grid raises as an exception by decision — a bundled Grid Source
    // attached from a second circuit (ADR-0018) — reaches the host log as an unhandled
    // exception, and a test that provokes it on purpose names it here. Named lines are
    // not CON-6 failures; each must appear, so the refusal is asserted, not excused.
    // Anything else in the log still fails the test.
    expectedHostLog: [[], { option: true }],
    // A warning the grid writes by decision — the Fill-height parent with no height it
    // names (ADR-0028) — is provoked on purpose by the test that pins it, and named here
    // the same way: not a CON-3 failure, and asserted to appear, in the console on
    // WebAssembly or in the host's log on the Server host.
    expectedWarnings: [[], { option: true }],
    // Every page a test drives is listened to from before its first navigation, so
    // nothing the app says while booting escapes the record.
    page: async ({ page, expectedHostLog, expectedWarnings }, use, testInfo) => {
        const messages = [];
        const pageErrors = [];
        page.on('console', (m) => {
            messages.push({ type: m.type(), text: m.text(), url: m.location().url ?? '' });
        });
        page.on('pageerror', (e) => pageErrors.push(String(e)));
        const hostLogFrom = hostLogSize();

        // On the Server host every page is prerendered first: painted, and deaf until its
        // circuit connects and each grid has attached its listener (A11Y-20). A user who
        // acts before that loses the input — the grid says it is busy for exactly that
        // reason — so every navigation here waits, as that user would, for the page to
        // be interactive and no grid to be Prerendered. WebAssembly has no prerender and
        // the wait is immediate.
        if (SERVER) {
            const ready = async () => {
                await page.locator('#demo-interactive').waitFor({ state: 'attached' });
                await page.waitForFunction(() => !document.querySelector('.ex-grid[aria-busy]'));
            };
            for (const name of ['goto', 'reload']) {
                const navigate = page[name].bind(page);
                page[name] = async (...args) => {
                    const response = await navigate(...args);
                    await ready();
                    return response;
                };
            }
        }

        await use(page);

        // A test that raised the round trip leaves it where the next one expects it.
        await setRoundTrip(0);

        const errors = messages.filter((m) => m.type === 'error');
        const warnings = messages.filter((m) => m.type === 'warning');
        // CON-6. The DemoHost is Blazor WebAssembly: its host runs in the page, and
        // the host's log — the renderer's "Unhandled exception rendering component"
        // among it — is written to the browser console, at whatever level the logger
        // chose. So every level is read here, not only errors. What the dev server
        // prints is only static-file serving; playwright.config.mjs pipes it into the
        // run's own output, where §22 Step 4's tee keeps it.
        //
        // On the Server host the renderer runs in the host's process, and its log is the
        // host's (SERVER in hosting.mjs): every Error or Critical line it appended while
        // the test ran counts, as well as any line naming an unhandled exception.
        const hostLog = hostLogSince(hostLogFrom);
        const expected = (line) => expectedHostLog.some((pattern) => pattern.test(line));
        const unhandled = [...messages.map((m) => m.text), ...pageErrors, ...hostLog.filter((l) => !expected(l))]
            .filter((text) => /unhandled exception|^\S+ (Error|Critical) /i.test(text));
        for (const pattern of expectedHostLog) {
            expect(hostLog.some((line) => pattern.test(line)), `the host log names ${pattern}`).toBe(true);
        }

        // console.json keeps each test's errors and warnings under its title, so a
        // rerun replaces its own entry instead of appending a second copy. A test that
        // said nothing leaves no entry: an empty file is CON-1's pass. Third-party
        // warnings stay in it for results.md to list, as CON-3 asks. Each entry is
        // stamped, so one left by a test renamed since, earlier the same day, shows
        // itself as older than the run.
        const title = testInfo.titlePath.slice(1).join(' › ');
        update('console.json', (all) => {
            const project = (all[testInfo.project.name] ??= {});
            if (errors.length || warnings.length || pageErrors.length) {
                project[title] = {
                    at: new Date().toISOString(),
                    errors: errors.map(({ text, url }) => ({ text, url })),
                    warnings: warnings.map(({ text, url }) => ({ text, url, exgrid: isExGrids({ text, url }) })),
                    pageErrors,
                };
            } else {
                delete project[title];
            }
        });

        expect(errors.map((m) => m.text), 'zero console errors (CON-1)').toEqual([]);
        expect(pageErrors, 'zero uncaught page errors (CON-2)').toEqual([]);
        const named = (text) => expectedWarnings.some((pattern) => pattern.test(text));
        for (const pattern of expectedWarnings) {
            expect([...warnings.map((m) => m.text), ...hostLog].some((line) => pattern.test(line)),
                `the grid warned ${pattern}`).toBe(true);
        }
        expect(warnings.filter(isExGrids).filter((m) => !named(m.text)).map((m) => m.text),
            'zero warnings from ExGrid\'s own code (CON-3)').toEqual([]);
        expect(unhandled, 'no unhandled exception reaches the host log (CON-6)').toEqual([]);
    },
});

export { expect };
