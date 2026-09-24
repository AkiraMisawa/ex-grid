import { test as base, expect } from '@playwright/test';
import fs from 'node:fs';

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
// its category, so one from inside the component says ExGrid.Something. The DemoHost
// lives in the ExGrid.DemoHost namespace and is not the product, so it does not count.
function isExGrids(message) {
    return /\/_content\/ExGrid/.test(message.url)
        || /\[ex-grid\]/.test(message.text)
        || /\bExGrid\.(?!DemoHost\b)[A-Z]/.test(message.text);
}

export const test = base.extend({
    // Every page a test drives is listened to from before its first navigation, so
    // nothing the app says while booting escapes the record.
    page: async ({ page }, use, testInfo) => {
        const messages = [];
        const pageErrors = [];
        page.on('console', (m) => {
            messages.push({ type: m.type(), text: m.text(), url: m.location().url ?? '' });
        });
        page.on('pageerror', (e) => pageErrors.push(String(e)));

        await use(page);

        const errors = messages.filter((m) => m.type === 'error');
        const warnings = messages.filter((m) => m.type === 'warning');
        // CON-6. The DemoHost is Blazor WebAssembly: its host runs in the page, and
        // the host's log — the renderer's "Unhandled exception rendering component"
        // among it — is written to the browser console, at whatever level the logger
        // chose. So every level is read here, not only errors. What the dev server
        // prints is only static-file serving; playwright.config.mjs pipes it into the
        // run's own output, where §22 Step 4's tee keeps it.
        const unhandled = [...messages.map((m) => m.text), ...pageErrors]
            .filter((text) => /unhandled exception/i.test(text));

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
        expect(warnings.filter(isExGrids).map((m) => m.text), 'zero warnings from ExGrid\'s own code (CON-3)').toEqual([]);
        expect(unhandled, 'no unhandled exception reaches the host log (CON-6)').toEqual([]);
    },
});

export { expect };
