import { test as base, expect } from '@playwright/test';
import fs from 'node:fs';
import { BASE_URL, HOST_LOG, LATENCY_CONTROL_URL, SERVER } from './hosting.mjs';

// What every spec shares: the console capture that CON-1/2/3/6 are read from, the two
// records a run writes — console.json and metrics.json — under one directory, and the app a
// spec file boots once for all its tests (ADR-0048).

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
// safe only because playwright.config.mjs pins `workers: 1, fullyParallel: false` — one
// of the reasons it does (ADR-0048). If that ever relaxes, this needs a lock, and the
// symptom will be a key missing from a record.
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

// A grid is painted before it can hear a key: its listener attaches only once the module
// import has landed, and until then the grid is Prerendered — no tab stop, aria-busy
// (A11Y-20) — and a key pressed at it is lost. On the Server host the whole page is
// prerendered and deaf until its circuit connects as well. A user who acts before that loses
// the input — the grid says it is busy for exactly that reason — so every navigation here
// waits, as that user would, for the page to be interactive and no grid to be Prerendered. On
// WebAssembly this used to be skipped as immediate; it is not: rows paint before the import
// resolves, and a test that clicked and typed in that gap lost its keys on a slow runner.
async function ready(page) {
    await page.locator('#demo-interactive').waitFor({ state: 'attached' });
    await page.waitForFunction(() => !document.querySelector('.ex-grid[aria-busy]'));
}

// Every page a test drives is listened to from before its first navigation, so nothing the
// app says while booting escapes the record. `sinkOf` names the record: a shared page's is
// whichever test holds it at the moment (ADR-0048).
function listen(page, sinkOf) {
    page.on('console', (m) => {
        sinkOf().messages.push({ type: m.type(), text: m.text(), url: m.location().url ?? '' });
    });
    page.on('pageerror', (e) => sinkOf().pageErrors.push(String(e)));
}

// The console verdict every test ends with, and its entry in console.json.
function verdict({ messages, pageErrors }, hostLogFrom, { expectedHostLog, expectedWarnings }, testInfo) {
    const errors = messages.filter((m) => m.type === 'error');
    const warnings = messages.filter((m) => m.type === 'warning');
    // CON-6. The DemoHost is Blazor WebAssembly: its host runs in the page, and the host's
    // log — the renderer's "Unhandled exception rendering component" among it — is written
    // to the browser console, at whatever level the logger chose. So every level is read
    // here, not only errors. What the dev server prints is only static-file serving;
    // playwright.config.mjs pipes it into the run's own output, where §22 Step 4's tee
    // keeps it.
    //
    // On the Server host the renderer runs in the host's process, and its log is the host's
    // (SERVER in hosting.mjs): every Error or Critical line it appended while the test ran
    // counts, as well as any line naming an unhandled exception.
    const hostLog = hostLogSince(hostLogFrom);
    const expected = (line) => expectedHostLog.some((pattern) => pattern.test(line));
    const unhandled = [...messages.map((m) => m.text), ...pageErrors, ...hostLog.filter((l) => !expected(l))]
        .filter((text) => /unhandled exception|^\S+ (Error|Critical) /i.test(text));
    for (const pattern of expectedHostLog) {
        expect(hostLog.some((line) => pattern.test(line)), `the host log names ${pattern}`).toBe(true);
    }

    // console.json keeps each test's errors and warnings under its title, so a rerun
    // replaces its own entry instead of appending a second copy. A test that said nothing
    // leaves no entry: an empty file is CON-1's pass. Third-party warnings stay in it for
    // results.md to list, as CON-3 asks. Each entry is stamped, so one left by a test
    // renamed since, earlier the same day, shows itself as older than the run.
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
}

// What a test changed through patchPage, per page, undone as the test ends (ADR-0048).
const undos = new WeakMap();

/**
 * Changes the page outside the test's own grids — a global, a listener on window or document, the
 * head, body, <html> or #app — and has the harness put it back when the test ends (ADR-0048).
 * `patch` runs in the page and returns the function that undoes what it did.
 */
export async function patchPage(page, patch, arg) {
    const undo = await page.evaluateHandle(patch, arg);
    if (!await undo.evaluate((f) => typeof f === 'function')) {
        throw new Error('patchPage: the patch must return the function that undoes it');
    }
    if (!undos.has(page)) {
        undos.set(page, []);
    }
    undos.get(page).push(undo);
}

// Undoes what patchPage did, the latest first. A patch whose document is gone — the test
// reloaded, or the page closed — has nothing left to undo.
async function undoPatches(page) {
    const list = undos.get(page) ?? [];
    undos.delete(page);
    for (const undo of list.reverse()) {
        await undo.evaluate((f) => f()).catch(() => { });
        await undo.dispose().catch(() => { });
    }
}

// The natives a test is likely to stub to stand in for the browser. Each must still be the one
// the app booted with when the test ends; one replaced outside patchPage would reach every later
// test of the file (ADR-0048: CP-23's stub did).
const WATCHED_NATIVES = [
    'navigator.clipboard.read', 'navigator.clipboard.readText',
    'navigator.clipboard.write', 'navigator.clipboard.writeText',
    'fetch', 'setTimeout', 'clearTimeout', 'setInterval', 'clearInterval',
    'requestAnimationFrame', 'cancelAnimationFrame', 'open', 'alert', 'confirm', 'prompt',
    'ResizeObserver', 'IntersectionObserver', 'MutationObserver',
    'Document.prototype.execCommand', 'Document.prototype.hasFocus',
    'EventTarget.prototype.addEventListener', 'EventTarget.prototype.removeEventListener',
    'EventTarget.prototype.dispatchEvent',
    'HTMLElement.prototype.focus', 'HTMLElement.prototype.blur', 'HTMLElement.prototype.click',
    'Element.prototype.getBoundingClientRect', 'Element.prototype.scrollIntoView',
    'Performance.prototype.now', 'Date.now',
];

// Page and context methods whose effect outlives a navigation. A test that calls one has made
// the page its own: its navigations are real from then on, and its page is not handed on.
const PAGE_STATE = ['addInitScript', 'addScriptTag', 'addStyleTag', 'emulateMedia', 'exposeBinding',
    'exposeFunction', 'route', 'routeFromHAR', 'routeWebSocket', 'setDefaultNavigationTimeout',
    'setDefaultTimeout', 'setExtraHTTPHeaders'];
const CONTEXT_STATE = ['addCookies', 'addInitScript', 'exposeBinding', 'exposeFunction', 'newCDPSession',
    'route', 'routeFromHAR', 'routeWebSocket', 'setDefaultNavigationTimeout', 'setDefaultTimeout',
    'setExtraHTTPHeaders', 'setGeolocation', 'setHTTPCredentials', 'setOffline'];

// The document outside a test's page, as the index shows it. Comments aside: they are
// Blazor's markers, which differ between a prerendered render and an interactive one. And an
// empty style or class aside, which is what removing the last property or class leaves.
const markupAtIndex = (page) => page.evaluate(() => document.documentElement.outerHTML
    .replace(/<!--[\s\S]*?-->/g, '')
    .replace(/ (?:style|class)=""/g, ''));

const nativesOf = (page) => page.evaluateHandle((paths) => {
    const read = (path) => path.split('.').reduce((owner, key) => owner?.[key], window);
    return new Map(paths.map((path) => [path, read(path)]));
}, WATCHED_NATIVES);

// What a test left behind outside its page, as sentences that name it.
async function leaksSince(app) {
    const leaks = [];
    const markup = await markupAtIndex(app.page);
    if (markup !== app.markup) {
        let at = 0;
        while (at < markup.length && at < app.markup.length && markup[at] === app.markup[at]) {
            at++;
        }
        const around = (text) => text.slice(Math.max(0, at - 40), at + 160);
        leaks.push(`back at the index, the document is not the one the file booted: `
            + `…${around(markup)}… where it was …${around(app.markup)}… — change it with patchPage`);
    }
    const replaced = await app.natives.evaluate((natives) => {
        const read = (path) => path.split('.').reduce((owner, key) => owner?.[key], window);
        return [...natives].filter(([path, original]) => read(path) !== original).map(([path]) => path);
    });
    for (const path of replaced) {
        leaks.push(`${path} is not the one the app booted with — stub it with patchPage, which puts it back`);
    }
    return leaks;
}

// Navigates the booted app to `path` without a boot. The router has swapped the page when the
// index has come or gone: every test's page is reached from the index, so its leaving is the
// signal that the new page is in.
async function navigateInApp(page, path) {
    const href = new URL(path, BASE_URL).href;
    await page.evaluate((to) => window.Blazor.navigateTo(to), href);
    await page.waitForURL(href);
    await page.locator('#demo-index').waitFor({ state: new URL(href).pathname === '/' ? 'attached' : 'detached' });
}

// Reaches `target` the way every test after a file's first does: back to the index — so the
// last page, and every grid on it, is disposed and the next is a new instance — then on to the
// target. At the index, what a fresh load would have is restored: the scroll, no text
// selected, the pointer in the corner, and the next Tab starting from the top of the document.
async function arriveAt(page, target) {
    const current = new URL(page.url());
    if (current.pathname !== '/' || current.search !== '') {
        await navigateInApp(page, '/');
    }
    await page.mouse.move(0, 0);
    await page.evaluate(() => {
        window.scrollTo(0, 0);
        getSelection()?.removeAllRanges();
        const start = document.createElement('span');
        start.tabIndex = -1;
        document.body.prepend(start);
        start.focus({ preventScroll: true });
        start.remove();
    });
    const path = target.pathname + target.search + target.hash;
    if (path !== '/') {
        await navigateInApp(page, path);
    }
    await ready(page);
}

// The worker's app: one browser context and page per spec file, booted at the index by the
// file's first navigation (ADR-0048).
async function openApp(app, browser, testInfo, viewport) {
    if (app.page && (app.file !== testInfo.file || app.page.isClosed())) {
        await closeApp(app);
    }
    if (app.page) {
        return;
    }
    app.file = testInfo.file;
    app.context = await browser.newContext({ baseURL: BASE_URL, viewport });
    app.page = await app.context.newPage();
    app.sink = { messages: [], pageErrors: [] };
    listen(app.page, () => app.sink);
}

async function closeApp(app) {
    await app.natives?.dispose().catch(() => { });
    await app.context?.close().catch(() => { });
    Object.assign(app, { context: null, page: null, file: null, markup: null, natives: null });
}

// A test with a document of its own (ADR-0048): a context of its own, every navigation real.
async function ownPage(page, use, options, testInfo) {
    const sink = { messages: [], pageErrors: [] };
    listen(page, () => sink);
    const hostLogFrom = hostLogSize();
    for (const name of ['goto', 'reload']) {
        const navigate = page[name].bind(page);
        page[name] = async (...args) => {
            const response = await navigate(...args);
            await ready(page);
            return response;
        };
    }

    await use(page);

    await undoPatches(page);
    // A test that raised the round trip leaves it where the next one expects it.
    await setRoundTrip(0);
    for (const pattern of options.expectedLeaks) {
        expect(false, `the harness named ${pattern}: a document of the test's own is not checked`).toBe(true);
    }
    verdict(sink, hostLogFrom, options, testInfo);
}

async function sharedPage(app, context, viewport, use, options, testInfo) {
    const page = app.page;
    // What the app said since the last test ended — its boot, for the first — is this test's.
    const sink = app.sink;
    const hostLogFrom = hostLogSize();
    const size = page.viewportSize();
    if (size?.width !== viewport.width || size?.height !== viewport.height) {
        await page.setViewportSize(viewport);
    }

    const restore = [];
    const wrap = (target, name, replacement) => {
        const original = target[name];
        target[name] = replacement(original.bind(target));
        restore.push(() => { target[name] = original; });
    };
    let owned = false;
    for (const name of PAGE_STATE) {
        wrap(page, name, (call) => (...args) => { owned = true; return call(...args); });
    }
    for (const name of CONTEXT_STATE) {
        wrap(context, name, (call) => (...args) => { owned = true; return call(...args); });
    }
    const held = new Set();
    wrap(page.keyboard, 'down', (down) => (key, ...rest) => { held.add(`the key ${key}`); return down(key, ...rest); });
    wrap(page.keyboard, 'up', (up) => (key, ...rest) => { held.delete(`the key ${key}`); return up(key, ...rest); });
    wrap(page.mouse, 'down', (down) => (o) => { held.add(`the ${o?.button ?? 'left'} button`); return down(o); });
    wrap(page.mouse, 'up', (up) => (o) => { held.delete(`the ${o?.button ?? 'left'} button`); return up(o); });
    wrap(page, 'goto', (goto) => async (url, gotoOptions) => {
        const target = new URL(url, BASE_URL);
        if (owned || target.origin !== new URL(BASE_URL).origin) {
            owned = true;
            const response = await goto(url, gotoOptions);
            await ready(page);
            return response;
        }
        if (!app.markup) {
            // The file's first navigation boots the app, at the index, and takes what the
            // document and the natives are there: what every later test is checked against.
            await goto('/', gotoOptions);
            await ready(page);
            app.markup = await markupAtIndex(page);
            app.natives = await nativesOf(page);
        }
        await arriveAt(page, target);
        return null;
    });
    wrap(page, 'reload', (reload) => async (...args) => {
        owned = true;
        const response = await reload(...args);
        await ready(page);
        return response;
    });
    const listenersBefore = new Map(page.eventNames().map((name) => [name, page.listeners(name)]));

    await use(page);

    for (const undo of restore.reverse()) {
        undo();
    }
    // A listener a test put on the Page object would hear every later test's page.
    for (const name of page.eventNames()) {
        for (const listener of page.listeners(name)) {
            if (!listenersBefore.get(name)?.includes(listener)) {
                page.removeListener(name, listener);
            }
        }
    }
    await undoPatches(page);
    await setRoundTrip(0);
    for (const other of context.pages().filter((p) => p !== page)) {
        await other.close().catch(() => { });
    }

    // Leaving the test's page disposes it here, inside the test, so what the disposal says is
    // this test's; then what it left outside its page is looked for.
    const failed = testInfo.status !== 'passed' && testInfo.status !== 'skipped';
    let handOn = !failed && !owned && held.size === 0 && !page.isClosed() && app.markup !== null;
    const leaks = [];
    if (handOn) {
        try {
            await navigateInApp(page, '/');
            // What the disposal left to a later frame — a wait that outlived its grid — runs
            // here, in the test that mounted the grid, not in the next one.
            await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
            leaks.push(...await leaksSince(app));
        } catch (error) {
            leaks.push(`the page could not be left for the index: ${error.message}`);
        }
    }
    // From here on, what the app says is the next test's.
    app.sink = { messages: [], pageErrors: [] };
    const reported = sink.pageErrors.length > 0
        || sink.messages.some((m) => m.type === 'error' || /unhandled exception/i.test(m.text));
    if (!handOn || leaks.length > 0 || reported) {
        await closeApp(app);
    }

    for (const pattern of options.expectedLeaks) {
        expect(leaks.some((leak) => pattern.test(leak)), `the harness named ${pattern}`).toBe(true);
    }
    expect(leaks.filter((leak) => !options.expectedLeaks.some((pattern) => pattern.test(leak))),
        'nothing left outside the test\'s own page (ADR-0048)').toEqual([]);
    verdict(sink, hostLogFrom, options, testInfo);
}

export const test = base.extend({
    // A browser's first page is not what any test is about. The first load after a launch
    // pays for the browser starting up and for the app's first boot in it, and on a CI
    // runner that has taken over half a minute — the second project's first two tests timed
    // out opening /features before their bodies ran, and every test after them passed. So
    // each worker, which is one browser, opens a grid page once before its first test,
    // under a timeout of its own, and the tests' own timeouts keep measuring the tests.
    // Nothing is asserted here: a page that never loads fails the first test that needs
    // it, by name, as before.
    warmedUp: [async ({ browser }, use) => {
        const context = await browser.newContext({ baseURL: BASE_URL });
        try {
            const page = await context.newPage();
            await page.goto('/features', { timeout: 150_000 });
            await page.locator('.ex-grid .ex-row').first()
                .waitFor({ state: 'visible', timeout: 150_000 })
                .catch(() => { });
        } catch {
            // Left to the tests to report: this is preparation, not a verdict.
        } finally {
            await context.close();
        }
        await use(true);
    }, { scope: 'worker', auto: true, timeout: 330_000 }],
    // The app a spec file boots once (ADR-0048), held by the worker from one test to the next.
    app: [async ({ }, use) => {
        const app = { context: null, page: null, file: null, markup: null, natives: null, sink: null };
        await use(app);
        await closeApp(app);
    }, { scope: 'worker' }],
    // A refusal the grid raises as an exception by decision — a bundled Grid Source
    // attached from a second circuit (ADR-0018) — reaches the host log as an unhandled
    // exception, and a test that provokes it on purpose names it here. Named lines are
    // not CON-6 failures; each must appear, so the refusal is asserted, not excused.
    // Anything else in the log still fails the test.
    expectedHostLog: [[], { option: true }],
    // A warning the grid writes by decision — the Stretch-height parent with no height it
    // names (ADR-0028) — is provoked on purpose by the test that pins it, and named here
    // the same way: not a CON-3 failure, and asserted to appear, in the console on
    // WebAssembly or in the host's log on the Server host.
    expectedWarnings: [[], { option: true }],
    // A change a test makes outside its own page without patchPage, provoked on purpose by the
    // harness's own tests (harness.spec.mjs) and named here: asserted to be reported, and not
    // a failure (ADR-0048).
    expectedLeaks: [[], { option: true }],
    // A document of the test's own: a context of its own and every navigation real (ADR-0048).
    freshDocument: [false, { option: true }],
    // The test's context is the file's app's, so what it grants reaches the page it drives;
    // what it granted is taken back as it ends.
    context: async ({ app, browser, viewport, freshDocument }, use, testInfo) => {
        if (viewport === null || freshDocument) {
            const context = await browser.newContext({ baseURL: BASE_URL, viewport });
            await use(context);
            await context.close();
            return;
        }
        await openApp(app, browser, testInfo, viewport);
        const context = app.context;
        await use(context);
        await context.clearPermissions().catch(() => { });
    },
    page: async ({ app, context, viewport, freshDocument, expectedHostLog, expectedWarnings, expectedLeaks }, use, testInfo) => {
        const options = { expectedHostLog, expectedWarnings, expectedLeaks };
        if (viewport === null || freshDocument) {
            await ownPage(await context.newPage(), use, options, testInfo);
        } else {
            await sharedPage(app, context, viewport, use, options, testInfo);
        }
    },
});

export { expect };

// Whether the next keydown of `key` — a key name, or a list of them — reaches the page with its
// default intact. The listener sits on document, past the grid's capture-phase one, and is
// installed BEFORE the press (the KB-15 lesson). A key the grid takes is prevented AND stopped, so
// the listener never hears it: `keySeenUntouched` answers null for taken, true for untouched.
// Bare modifiers go down first as keydowns of their own, which is why the key is named.
// `preventAfter` stops the browser's own action once the page has seen the key untouched — Ctrl+R
// would otherwise reload the page under the test, and Ctrl+D open a bookmark bubble.
export async function watchNextKey(page, key, { preventAfter = false } = {}) {
    const keys = Array.isArray(key) ? key : [key];
    await patchPage(page, ({ keys, preventAfter }) => {
        window.__keySeen = null;
        const listener = (e) => {
            if (!keys.includes(e.key)) {
                return;
            }
            window.__keySeen = !e.defaultPrevented;
            if (preventAfter) {
                e.preventDefault();
            }
            document.removeEventListener('keydown', listener);
        };
        document.addEventListener('keydown', listener);
        // A key the grid took never reaches the listener, which would then hear the next
        // test's keys: the harness takes it off as the test ends.
        return () => {
            document.removeEventListener('keydown', listener);
            delete window.__keySeen;
        };
    }, { keys, preventAfter });
}

export const keySeenUntouched = (page) => page.evaluate(() => window.__keySeen);
