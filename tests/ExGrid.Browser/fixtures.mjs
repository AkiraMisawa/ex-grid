import { test as base, expect } from '@playwright/test';
import fs from 'node:fs';
import { BASE_URL, HOST_LOG, LATENCY_CONTROL_URL, REACT, SERVER } from './hosting.mjs';

// What every spec shares: the console capture that CON-1/2/3/6 are read from, the two
// records a run writes — console.json and metrics.json — under one directory, and the app a
// spec file boots once for all its tests (ADR-0056).

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
// of the reasons it does (ADR-0056). If that ever relaxes, this needs a lock, and the
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

/**
 * The widened run (ADR-0220 §5): with EXGRID_CPU_THROTTLE set, every page a context opens runs
 * that many times slower, as Chrome's DevTools throttle does, so a race that needs the browser to
 * be slow fails nearly every run instead of one in forty. Unset, nothing changes. The round trip
 * is raised by the latency proxy itself (EXGRID_RTT_FLOOR, latency-proxy.mjs).
 */
const CPU_THROTTLE = Number(process.env.EXGRID_CPU_THROTTLE ?? 1);
if (!(CPU_THROTTLE >= 1)) {
    throw new Error(`EXGRID_CPU_THROTTLE is "${process.env.EXGRID_CPU_THROTTLE}"; it is a rate of 1 or more.`);
}

function widened(context) {
    if (CPU_THROTTLE > 1) {
        context.on('page', async (page) => {
            // A page closed before its session opens has nothing left to slow.
            await context.newCDPSession(page)
                .then((session) => session.send('Emulation.setCPUThrottlingRate', { rate: CPU_THROTTLE }))
                .catch(() => { });
        });
    }
    return context;
}

/**
 * Holds every answer from the Server host in the latency proxy, or lets the held ones go in
 * order (latency-proxy.mjs). A test that must act before the host's answer reaches the page
 * holds them across its inputs, instead of counting on the round trip outlasting them
 * (ADR-0220). Answers false on WebAssembly, where there is no circuit; the fixture lets go of
 * them after every test.
 */
export async function holdAnswers(held) {
    if (!SERVER) {
        return false;
    }
    const response = await fetch(`${LATENCY_CONTROL_URL}/hold?answers=${held ? 1 : 0}`, { method: 'POST' });
    if (!response.ok) {
        throw new Error(`the latency proxy refused to ${held ? 'hold' : 'let go of'} the host's answers: ${response.status}`
            + ' (one started from an older checkout does not know /hold, so stop it and run again)');
    }
    return true;
}

/**
 * The number the next connection through the latency proxy will take: taken before a test opens its
 * own page, it names that page's connections to dropCircuit, so a document another test left open
 * keeps its circuit. Null on WebAssembly, where there is no circuit.
 */
export async function connectionMark() {
    if (!SERVER) {
        return null;
    }
    return Number((await (await fetch(`${LATENCY_CONTROL_URL}/mark`)).text()).trim());
}

/**
 * Cuts the connections made since `from` (connectionMark) in the latency proxy and refuses new ones,
 * or lets the browser connect again (latency-proxy.mjs), as a network that goes away and comes back
 * does. The host keeps the circuit, so Blazor reconnects to it (ADR-0240 §11). Answers false on
 * WebAssembly, where there is no circuit; the fixture lets the browser connect again after every test.
 */
export async function dropCircuit(down, from = 0) {
    if (!SERVER) {
        return false;
    }
    const response = await fetch(`${LATENCY_CONTROL_URL}/drop?down=${down ? 1 : 0}&from=${from ?? 0}`, { method: 'POST' });
    if (!response.ok) {
        throw new Error(`the latency proxy refused to ${down ? 'cut' : 'restore'} the connection: ${response.status}`
            + ' (one started from an older checkout does not know /drop, so stop it and run again)');
    }
    return true;
}

/**
 * Returns once Blazor has opened its WebSocket again since dropCircuit(false), and the wire has gone
 * quiet behind it, so the circuit has been taken up again (ADR-0240 §11). Blazor retries on a backoff of
 * its own, so this waits for the opening, never for a fixed time.
 */
export async function circuitBack({ timeout = 60_000 } = {}) {
    const response = await fetch(`${LATENCY_CONTROL_URL}/reconnected?within=${timeout}`);
    if (!response.ok) {
        throw new Error(`the circuit did not come back: ${(await response.text()).trim()}`);
    }
    await circuitQuiet();
}

/** The round trip the latency proxy is set to, in ms; null on WebAssembly, which has none. */
export async function roundTrip() {
    if (!SERVER) {
        return null;
    }
    const response = await fetch(`${LATENCY_CONTROL_URL}/`);
    return Number((await response.text()).trim().replace('rtt=', ''));
}

/**
 * How long the wire between the browser and the Server host must stay still before
 * circuitQuiet says the host has said all it will. Longer than the longest delay the grid
 * renders after: 300 ms before an error message opens (PopoverDelay in ExGrid.razor), and
 * 150 ms before Placeholders fill and an announcement is made (SettleDelay). A render a timer
 * owes therefore lands inside the window and starts it again.
 */
export const CIRCUIT_QUIET_MS = 400;

/**
 * Returns once the browser and the Server host have stopped talking: nothing held in the
 * latency proxy or unsent on a socket, in either direction, and nothing crossed for
 * `quietFor` ms (latency-proxy.mjs). The browser acknowledges every render batch once it has
 * applied it, so a quiet wire also means the last batch is in the DOM. Await it before reading
 * what the host decides, where a fixed wait used to stand: a wait too short reads before the
 * answer, and a read after one that happened to be long enough passes before a late answer
 * could undo it. It knows nothing of a timer due after the window, nor of painting: a picture
 * still waits for its frames. On WebAssembly, where there is no wire, it returns at once.
 * Throws, saying what kept crossing, when the wire is not quiet within `timeout` ms.
 * SignalR's keep-alive pings, 15 s apart, delay it by one window at most.
 */
export async function circuitQuiet({ quietFor = CIRCUIT_QUIET_MS, timeout = 10_000 } = {}) {
    if (!SERVER) {
        return;
    }
    const response = await fetch(`${LATENCY_CONTROL_URL}/quiet?for=${quietFor}&within=${timeout}`);
    const answer = (await response.text()).trim();
    if (!response.ok) {
        throw new Error(`the circuit did not go quiet: ${answer}`);
    }
    // An older proxy answers any GET with its round trip, which would read as quiet at once.
    if (!answer.startsWith('quiet for')) {
        throw new Error(`the latency proxy at ${LATENCY_CONTROL_URL} does not know /quiet (it answered "${answer}"): `
            + 'it was started from an older checkout, so stop it and run again');
    }
}

// What the Server host logged while one test ran (CON-6): the file the host appends to, read
// from where the last reading stopped, so a line written between two tests is the next test's,
// as a console message is (ADR-0056). Answers the lines and where the reading stopped.
const hostLogSize = () => (fs.existsSync(HOST_LOG) ? fs.statSync(HOST_LOG).size : 0);
function hostLogFrom(offset) {
    if (!SERVER || !fs.existsSync(HOST_LOG)) {
        return { lines: [], end: offset };
    }
    const handle = fs.openSync(HOST_LOG, 'r');
    try {
        const end = fs.fstatSync(handle).size;
        // A log shorter than the offset is a new one: the runner clears it as a run starts.
        const from = end < offset ? 0 : offset;
        if (end === from) {
            return { lines: [], end };
        }
        const buffer = Buffer.alloc(end - from);
        fs.readSync(handle, buffer, 0, end - from, from);
        return { lines: buffer.toString('utf8').split('\n').filter((line) => line.length > 0), end };
    } finally {
        fs.closeSync(handle);
    }
}

// A grid is painted before it can hear a key: its listener attaches only once the module
// import has landed, and until then the grid is Prerendered — no tab stop, aria-busy
// (A11Y-20) — and a key pressed at it is lost. On the Server host the whole page is
// prerendered and deaf until its circuit connects as well. A user who acts before that loses
// the input — the grid says it is busy for exactly that reason — so every navigation here
// waits, as that user would, for the page to be interactive and no grid to be Prerendered (and
// no chart busy before its pointer entry holds its first drawing, ADR-0255). On
// WebAssembly this used to be skipped as immediate; it is not: rows paint before the import
// resolves, and a test that clicked and typed in that gap lost its keys on a slow runner.
async function ready(page) {
    await page.locator('#demo-interactive').waitFor({ state: 'attached' });
    await page.waitForFunction(() => !document.querySelector('.ex-grid[aria-busy], .ex-chart[aria-busy]'));
    // The HeadOutlet's first interactive render takes the head's static <title> and renders it as
    // its own, behind its comment (Blazor's getAndRemoveExistingTitle), on both hosts — on the
    // Server host a round trip after the page is interactive. Until it has, the head is not the
    // one the app keeps: read then at boot, the document differed from the same document read
    // after the file's first test (UX-2, CI on the Server host, 2026-10-02; ADR-0056). The React host
    // has no HeadOutlet: its head is index.html's, the same at boot and after every test.
    if (REACT) return;
    await page.waitForFunction(() => [...document.head.getElementsByTagName('title')]
        .every((title) => title.previousSibling instanceof Comment));
}

// A real navigation — goto or reload — that returns once the page is ready.
const thenReady = (page, navigate) => async (...args) => {
    const response = await navigate(...args);
    await ready(page);
    return response;
};

// What a page said, kept for the verdict of the test it is charged to (ADR-0056).
const newSink = () => ({ messages: [], pageErrors: [] });

// Every page a test drives is listened to from before its first navigation, so nothing the
// app says while booting escapes the record. `sinkOf` names the record: a shared page's is
// whichever test holds it at the moment.
function listen(page, sinkOf) {
    page.on('console', (m) => {
        sinkOf().messages.push({ type: m.type(), text: m.text(), url: m.location().url ?? '' });
    });
    page.on('pageerror', (e) => sinkOf().pageErrors.push(String(e)));
}

// The console verdict every test ends with, and its entry in console.json. Soft: every check
// is reported, and the record is written whatever the others found.
function verdict({ messages, pageErrors: allPageErrors }, hostLog, { expectedHostLog, expectedWarnings, expectedErrors = [], expectedPageErrors = [] }, testInfo) {
    // An uncaught error a library raises by design, provoked on purpose by the test that pins it (Blazor's
    // send on a connection that is down, ADR-0240 section 11), is named by expectedPageErrors, as an error
    // the browser writes is by expectedErrors.
    const pageErrors = allPageErrors.filter((text) => !expectedPageErrors.some((pattern) => pattern.test(text)));
    // A console error the browser writes by design, provoked on purpose by the test that pins it
    // (a script the import map's integrity refuses, ADR-0123), is named by expectedErrors: not a
    // CON-1 failure, and asserted to appear. Any other error still fails the test.
    const allErrors = messages.filter((m) => m.type === 'error');
    const errors = allErrors.filter((m) => !expectedErrors.some((pattern) => pattern.test(m.text)));
    // What a test names must appear, unless the test was skipped before it could be provoked.
    const ran = testInfo.status !== 'skipped';
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
    const expected = (line) => expectedHostLog.some((pattern) => pattern.test(line));
    const unhandled = [...messages.map((m) => m.text), ...pageErrors, ...hostLog.filter((l) => !expected(l))]
        .filter((text) => /unhandled exception|^\S+ (Error|Critical) /i.test(text));
    for (const pattern of ran ? expectedHostLog : []) {
        expect.soft(hostLog.some((line) => pattern.test(line)), `the host log names ${pattern}`).toBe(true);
    }
    for (const pattern of ran ? expectedErrors : []) {
        expect.soft(allErrors.some((m) => pattern.test(m.text)), `the browser wrote the error ${pattern}`).toBe(true);
    }
    for (const pattern of ran ? expectedPageErrors : []) {
        expect.soft(allPageErrors.some((text) => pattern.test(text)), `the page raised the error ${pattern}`).toBe(true);
    }

    // console.json keeps each test's errors and warnings under its title, so a rerun
    // replaces its own entry instead of appending a second copy. A test that said nothing
    // leaves no entry: an empty file is CON-1's pass. Third-party warnings stay in it for
    // results.md to list, as CON-3 asks. Each entry is stamped, so one left by a test
    // renamed since, earlier the same day, shows itself as older than the run.
    const title = testInfo.titlePath.slice(1).join(' › ');
    update('console.json', (all) => {
        const project = (all[testInfo.project.name] ??= {});
        // An error the test named is asserted, not a finding: it stays out of `errors`, whose
        // emptiness across the file is CON-1's pass, and alone it writes no entry.
        if (errors.length || warnings.length || pageErrors.length) {
            project[title] = {
                at: new Date().toISOString(),
                errors: errors.map(({ text, url }) => ({ text, url })),
                expectedErrors: allErrors.filter((m) => !errors.includes(m)).map(({ text, url }) => ({ text, url })),
                warnings: warnings.map(({ text, url }) => ({ text, url, exgrid: isExGrids({ text, url }) })),
                pageErrors,
            };
        } else {
            delete project[title];
        }
    });

    expect.soft(errors.map((m) => m.text), 'zero console errors (CON-1)').toEqual([]);
    expect.soft(pageErrors, 'zero uncaught page errors (CON-2)').toEqual([]);
    const named = (text) => expectedWarnings.some((pattern) => pattern.test(text));
    for (const pattern of ran ? expectedWarnings : []) {
        expect.soft([...warnings.map((m) => m.text), ...hostLog].some((line) => pattern.test(line)),
            `the grid warned ${pattern}`).toBe(true);
    }
    expect.soft(warnings.filter(isExGrids).filter((m) => !named(m.text)).map((m) => m.text),
        'zero warnings from ExGrid\'s own code (CON-3)').toEqual([]);
    expect.soft(unhandled, 'no unhandled exception reaches the host log (CON-6)').toEqual([]);
    // CON-8: ExGrid logs nothing on a healthy path. Its only console writes are its failure
    // paths, which are errors and fail CON-1 above, and the warnings it names by decision, which
    // CON-3 holds to expectedWarnings. A log, an info, a debug or a trace line from its own code
    // is neither, at any level the console has.
    expect.soft(messages.filter((m) => !['error', 'warning'].includes(m.type)).filter(isExGrids)
        .map((m) => `${m.type}: ${m.text}`), 'nothing logged by ExGrid on a healthy path (CON-8)').toEqual([]);
}

// What a test changed through alterPage, per page, undone as the test ends (ADR-0056).
const undos = new WeakMap();

/**
 * Changes the page outside the test's own grids — a global, a listener on window or document, the
 * head, body, <html> or #app — and has the harness put it back when the test ends (ADR-0056).
 * `change` runs in the page and returns the function that undoes what it did.
 */
export async function alterPage(page, change, arg) {
    const undo = await page.evaluateHandle(change, arg);
    if (!await undo.evaluate((f) => typeof f === 'function')) {
        throw new Error('alterPage: the change must return the function that undoes it');
    }
    if (!undos.has(page)) {
        undos.set(page, []);
    }
    undos.get(page).push(undo);
}

// Undoes what alterPage did, the latest first. A change whose document is gone — the test
// reloaded, or the page closed — has nothing left to undo.
async function undoAlterations(page) {
    const list = undos.get(page) ?? [];
    undos.delete(page);
    for (const undo of list.reverse()) {
        await undo.evaluate((f) => f()).catch(() => { });
        await undo.dispose().catch(() => { });
    }
}

// The natives a test is likely to stub to stand in for the browser, each read the way a caller
// reads it — `document.hasFocus`, not `Document.prototype.hasFocus` — so a stub on the instance
// counts as much as one on the prototype. Each must still be the one the app booted with when the
// test ends; one replaced outside alterPage would reach every later test of the file (ADR-0056:
// CP-23's stub did).
const WATCHED_NATIVES = [
    'navigator.clipboard.read', 'navigator.clipboard.readText',
    'navigator.clipboard.write', 'navigator.clipboard.writeText', 'navigator.permissions.query',
    'fetch', 'setTimeout', 'clearTimeout', 'setInterval', 'clearInterval', 'queueMicrotask',
    'requestAnimationFrame', 'cancelAnimationFrame', 'open', 'alert', 'confirm', 'prompt', 'print',
    'getComputedStyle', 'matchMedia', 'scrollTo', 'scrollBy',
    'ResizeObserver', 'IntersectionObserver', 'MutationObserver',
    'document.execCommand', 'document.hasFocus', 'document.getSelection',
    'document.elementFromPoint', 'document.elementsFromPoint',
    'EventTarget.prototype.addEventListener', 'EventTarget.prototype.removeEventListener',
    'EventTarget.prototype.dispatchEvent',
    'HTMLElement.prototype.focus', 'HTMLElement.prototype.blur', 'HTMLElement.prototype.click',
    'Element.prototype.getBoundingClientRect', 'Element.prototype.scrollIntoView',
    'performance.now', 'Date.now',
];

// Page and context methods whose effect outlives a navigation. A test that calls one has made
// the page its own: its navigations are real from then on, and its page is not handed on.
const PAGE_METHODS_THAT_OUTLIVE_A_NAVIGATION = ['addInitScript', 'addScriptTag', 'addStyleTag',
    'emulateMedia', 'exposeBinding', 'exposeFunction', 'route', 'routeFromHAR', 'routeWebSocket',
    'setDefaultNavigationTimeout', 'setDefaultTimeout', 'setExtraHTTPHeaders'];
const CONTEXT_METHODS_THAT_OUTLIVE_A_NAVIGATION = ['addCookies', 'addInitScript', 'exposeBinding',
    'exposeFunction', 'newCDPSession', 'route', 'routeFromHAR', 'routeWebSocket',
    'setDefaultNavigationTimeout', 'setDefaultTimeout', 'setExtraHTTPHeaders', 'setGeolocation',
    'setHTTPCredentials', 'setOffline'];

// Returns once every stylesheet the document links has loaded: each <link rel="stylesheet"> has its
// sheet, which the browser sets when the file has arrived and been parsed, and not before. A link
// joins document.styleSheets only then. On WebAssembly the packages' stylesheets are root components
// the app renders into the head after it boots (ADR-0123), so a page that is ready can still be
// waiting for them: read then, ExGrid's and ExSheet's were in the markup but not in the stylesheets,
// and the reading after the file's first test, where they had arrived, named them as left behind
// (pivot.spec.mjs on WebAssembly, after #82; every time with the files held back 400 ms). What the
// page says is read — the sheet each link holds — never a wait or a retry (ADR-0056). A file that
// never loads is a hang, said by name; its console error fails the test besides (CON-1).
export async function stylesheetsLoaded(page) {
    try {
        await page.waitForFunction(() => [...document.querySelectorAll('link[rel~="stylesheet"]')]
            .every((link) => link.sheet !== null), null, { timeout: 60_000 });
    } catch (error) {
        const waiting = await page.evaluate(() => [...document.querySelectorAll('link[rel~="stylesheet"]')]
            .filter((link) => link.sheet === null).map((link) => link.href)).catch(() => []);
        throw new Error(`a stylesheet the document links never loaded: ${waiting.join(', ')}`, { cause: error });
    }
}

// What the document outside a test's page is, as the index shows it: its markup, the rules in its
// stylesheets, and the globals on window. Comments aside — they are Blazor's markers, which differ
// between a prerendered render and an interactive one — and an empty style or class aside, which
// is what removing the last property or class leaves. Read once the stylesheets it links have
// loaded, so that both readings — at boot and after each test — see the same set.
async function documentAtIndex(page) {
    await stylesheetsLoaded(page);
    return page.evaluate(() => ({
        markup: document.documentElement.outerHTML
            .replace(/<!--[\s\S]*?-->/g, '')
            .replace(/ (?:style|class)=""/g, ''),
        sheets: [...document.styleSheets].map((sheet) => {
            let rules;
            try {
                rules = sheet.cssRules.length;
            } catch {
                rules = 'unreadable';
            }
            return `${sheet.href ?? 'a <style>'}: ${rules} rules`;
        }).concat(`${document.adoptedStyleSheets.length} adopted`),
        globals: Object.getOwnPropertyNames(window),
    }));
}

const nativesOf = (page) => page.evaluateHandle((paths) => {
    const read = (path) => path.split('.').reduce((owner, key) => owner?.[key], window);
    return new Map(paths.map((path) => [path, read(path)]));
}, WATCHED_NATIVES);

// The app a spec file boots once (ADR-0056), held by the worker from one test to the next: one
// browser context and page per spec file, booted at the index by the file's first navigation,
// with what the document and the natives were there.
class FileApp {
    context = null;
    page = null;
    file = null;
    options = null;
    booted = null;
    natives = null;
    // What the app says between two tests is the next test's, so this outlives a close: what a
    // file's last page says as it goes is heard by the next file's first test.
    sink = newSink();
    hostLogAt = null;

    // Opens the app for the test unless it is open for the test's file with its context options.
    // Options other than the viewport, which the harness sets for each test, belong to the context:
    // a test asking for other ones gets a context of its own asking.
    async open(browser, testInfo, contextOptions, viewport) {
        const { viewport: _, ...options } = contextOptions;
        const asked = JSON.stringify(options);
        if (this.page && (this.file !== testInfo.file || this.options !== asked || this.page.isClosed())) {
            await this.close();
        }
        if (this.page) {
            return;
        }
        this.file = testInfo.file;
        this.options = asked;
        this.context = widened(await browser.newContext({ ...contextOptions, baseURL: BASE_URL, viewport }));
        this.page = await this.context.newPage();
        await watchForHang(this.page);
        listen(this.page, () => this.sink);
    }

    async close() {
        await this.natives?.dispose().catch(() => { });
        await this.context?.close().catch(() => { });
        this.context = this.page = this.file = this.options = this.booted = this.natives = null;
    }

    // The file's first navigation: a real one, to the index, where what every later test is
    // checked against is taken.
    async boot(goto, gotoOptions) {
        await goto('/', gotoOptions);
        await ready(this.page);
        // The record every later test is checked against is taken once the host has said all it will:
        // on the Server host the head's <title> comes in a render after the prerendered page, and read
        // before it lands, the record lacked it and the first test's return to the index was named a
        // leak (the widened run, 150 ms and the CPU at a quarter, ADR-0220 §5).
        await circuitQuiet();
        this.booted = await documentAtIndex(this.page);
        this.natives = await nativesOf(this.page);
    }

    // What a test left behind outside its page, as sentences that name it.
    async leaks() {
        const now = await documentAtIndex(this.page);
        const leaks = [];
        if (now.markup !== this.booted.markup) {
            let at = 0;
            while (at < now.markup.length && at < this.booted.markup.length && now.markup[at] === this.booted.markup[at]) {
                at++;
            }
            const around = (text) => text.slice(Math.max(0, at - 40), at + 160);
            leaks.push('back at the index, the document is not the one the file booted: '
                + `…${around(now.markup)}… where it was …${around(this.booted.markup)}… — change it with alterPage`);
        }
        if (now.sheets.join('\n') !== this.booted.sheets.join('\n')) {
            leaks.push(`the stylesheets are not the ones the file booted with: ${now.sheets.join('; ')} `
                + `where they were ${this.booted.sheets.join('; ')} — change them with alterPage`);
        }
        const booted = new Set(this.booted.globals);
        for (const name of now.globals.filter((g) => !booted.has(g))) {
            leaks.push(`window.${name} is new since the file booted — set it with alterPage, which takes it off`);
        }
        const replaced = await this.natives.evaluate((natives) => {
            const read = (path) => path.split('.').reduce((owner, key) => owner?.[key], window);
            return [...natives].filter(([path, original]) => read(path) !== original).map(([path]) => path);
        });
        for (const path of replaced) {
            leaks.push(`${path} is not the one the app booted with — stub it with alterPage, which puts it back`);
        }
        return leaks;
    }

    // Where the host log is read from: the first test's start, and after that where the last
    // test's reading stopped.
    startHostLog() {
        this.hostLogAt ??= hostLogSize();
    }

    hostLog() {
        const { lines, end } = hostLogFrom(this.hostLogAt);
        this.hostLogAt = end;
        return lines;
    }
}

// Navigates the booted app to `path` without a boot. The router has swapped the page when the
// index has come or gone: every test's page is reached from the index, so its leaving is the
// signal that the new page is in.
async function navigateInApp(page, path) {
    const href = new URL(path, BASE_URL).href;
    // The React host's router is its own, with the same entry (ADR-0350 section 13).
    await page.evaluate(([to, react]) => (react ? window.exDemo : window.Blazor).navigateTo(to), [href, REACT]);
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

/**
 * Holds the page's animation frames back until the function returned is called, then runs them in the
 * order they were asked for (ADR-0240's note of 2026-10-21). A wait the grid looks at once a frame then
 * does not look again of itself, so a key typed after the keyboard has arrived lands in the window a
 * frame would otherwise close one run in hundreds: a test of "typed the moment it arrived" is then
 * decided every time. Undone as the test ends if not let go before.
 */
export async function holdFrames(page) {
    await alterPage(page, () => {
        const request = window.requestAnimationFrame;
        const cancel = window.cancelAnimationFrame;
        const waiting = new Map();
        let next = 0;
        window.requestAnimationFrame = (callback) => {
            next += 1;
            waiting.set(next, callback);
            return next;
        };
        window.cancelAnimationFrame = (id) => {
            waiting.delete(id);
        };
        const letGo = () => {
            window.requestAnimationFrame = request;
            window.cancelAnimationFrame = cancel;
            delete window.exFramesLetGo;
            const callbacks = [...waiting.values()];
            waiting.clear();
            request((now) => callbacks.forEach((callback) => callback(now)));
        };
        window.exFramesLetGo = letGo;
        return () => {
            if (window.exFramesLetGo === letGo) letGo();
        };
    });
    return () => page.evaluate(() => window.exFramesLetGo?.());
}

/**
 * Holds back, in the page, the replies to the grid's calls of `method` on its core, until the function
 * returned is called, then lets them through in order (ADR-0240's note of 2026-10-21). The call itself
 * goes and is answered, and what it renders arrives: only the promise the grid's script waits on is
 * kept, so the render and the Settled Stamp come ahead of the reply, on either host, every time. On
 * WebAssembly Find's field held the keyboard a moment before Ctrl+F's reply, one run in many (CI,
 * PR #243). Taken before the page the test mounts: the core is reached through the reference its
 * attach revives. Undone as the test ends if not let go before.
 */
export async function holdReplies(page, method) {
    await alterPage(page, (name) => {
        const held = [];
        let active = true;
        let proto = null;
        let original = null;
        DotNet.attachReviver((key, value) => {
            const candidate = value !== null && typeof value === 'object' ? Object.getPrototypeOf(value) : null;
            if (active && proto === null && candidate !== null && candidate !== Object.prototype
                && Object.prototype.hasOwnProperty.call(candidate, 'invokeMethodAsync')) {
                proto = candidate;
                original = candidate.invokeMethodAsync;
                candidate.invokeMethodAsync = function (m, ...args) {
                    const reply = original.call(this, m, ...args);
                    return m === name && window.exRepliesLetGo !== undefined
                        ? new Promise((resolve, reject) => held.push(() => reply.then(resolve, reject)))
                        : reply;
                };
            }
            return value;
        });
        const letGo = () => {
            delete window.exRepliesLetGo;
            held.splice(0).forEach((release) => release());
        };
        window.exRepliesLetGo = letGo;
        return () => {
            active = false;
            if (window.exRepliesLetGo === letGo) letGo();
            if (proto !== null) proto.invokeMethodAsync = original;
        };
    }, method);
    return () => page.evaluate(() => window.exRepliesLetGo?.());
}

/** The frames in which what a disposal left to later — a wait that outlived its grid — runs. */
export const twoFrames = (page) => page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));

/**
 * A forced press on a node the grid has just built again lands as a user's press would (ADR-0220).
 * A cell takes no pointer events (ADR-0004), so a test presses one with `{ force: true }`, and
 * Playwright then reads the node it resolved twice: once to scroll it into view, once for the
 * place to press. A row component keyed by its instance is built again, every element in it new,
 * when its row is handed over as a new instance (ADR-0003/0140): on /sheet that is any row a
 * change names, and a value arriving that the row was still getting (`#GETTING_DATA`). Built again
 * between the two reads, the node read second is gone, and a forced press fails as "Element is not
 * visible" with no input sent (ED-22 on #147's run, the twelfth press). A user's press is made at a
 * place, where the new row stands just as the old one did. So when a forced press fails that way
 * and its locator names one node now laid out, the press is made once more, on that node: nothing
 * reached the page the first time, so this is the press the test asked for, made once. A second
 * failure, or a node that is not laid out, is the test's failure.
 */
const HELD_FORCED_PRESSES = ['click', 'dblclick', 'hover'];

function holdForcedPresses(page) {
    const prototype = Object.getPrototypeOf(page.locator('html'));
    if (prototype.__exForcedPressesHeld) {
        return;
    }
    prototype.__exForcedPressesHeld = true;
    for (const name of HELD_FORCED_PRESSES) {
        const press = prototype[name];
        prototype[name] = async function (options) {
            try {
                return await press.call(this, options);
            } catch (error) {
                if (!options?.force || !/Element is not visible/.test(String(error?.message))) {
                    throw error;
                }
                const laidOut = await this.evaluateAll((nodes) => nodes.length === 1 && nodes[0].getClientRects().length > 0)
                    .catch(() => false);
                if (!laidOut) {
                    throw error;
                }
                return await press.call(this, options);
            }
        };
    }
}

// A session of the harness's own on each page it opens, attached while the page still answers: a page
// whose main thread a script holds for good answers nothing, a session attached then included, but a
// session attached before can still end that script (endHungScript).
const hangSessions = new WeakMap();
async function watchForHang(page) {
    hangSessions.set(page, await page.context().newCDPSession(page).then(
        (session) => ({ session }),
        (error) => ({ refused: `its session could not be attached when the page opened: ${error.message}` })));
}

/**
 * Whether the page has stopped answering, and if it has, ends the script that holds it. A script that
 * never returns — a loop in the app's JavaScript or in its WebAssembly — holds the page's main thread, so
 * no reading comes back, and an assertion waiting on one reports what it last saw, or nothing: seen in CI
 * on WebAssembly (PR #348), where a page stopped answering after a click and its teardown then waited five
 * minutes more for nothing. Ended, the script lets the page answer again, and the input journal says which
 * inputs led there. Answers null for a page that answers, and otherwise what was done. How long a page may
 * take to answer bounds this diagnosis only, never an outcome: it is asked only of a test that has timed
 * out already.
 */
async function endHungScript(page, { wait = 5_000 } = {}) {
    const answers = async () => {
        let timer;
        const late = new Promise((resolve) => { timer = setTimeout(() => resolve(false), wait); });
        const answered = await Promise.race([page.evaluate(() => true).then(() => true, () => true), late]);
        clearTimeout(timer);
        return answered;
    };
    if (await answers()) {
        return null;
    }
    const watched = hangSessions.get(page) ?? { refused: 'the harness did not open it, and has no session on it' };
    if (watched.session === undefined) {
        return `the page stopped answering, and the script holding it could not be ended: ${watched.refused}`;
    }
    // Its answer is not awaited: it is the page's to give, once the script has ended; the page answering
    // again is what is read.
    const ended = watched.session.send('Runtime.terminateExecution').then(() => '', (error) => `: ${error.message}`);
    return await answers()
        ? 'the page stopped answering, and the script holding it was ended'
        : `the page stopped answering, and the script holding it could not be ended${await Promise.race([ended, ''])}`;
}

// A failed test carries the Input Journal of every grid on its page (ADR-0240 section 10): what the
// script numbered, held, let go and called the core with, and what the stamp answered, read from each
// root as the test ends, before anything navigates away. A passing test reads nothing.
async function readJournals(page, testInfo) {
    if (testInfo.status === testInfo.expectedStatus || testInfo.status === 'skipped' || page.isClosed()) {
        return null;
    }
    // A test that timed out may have timed out on a page that no longer answers: every reading of it,
    // the journal's included, would then wait until the teardown timed out too, and say nothing.
    const hung = testInfo.status === 'timedOut' ? await endHungScript(page) : null;
    if (hung !== null) {
        console.log(`hung-page ${hung}`);
        expect.soft(hung, 'the page still answers once the test has timed out').toBeNull();
        if (hung.includes('could not be ended')) {
            await page.close({ runBeforeUnload: false }).catch(() => { });
            return [{ unread: hung }];
        }
    }
    return page.evaluate(() => [...document.querySelectorAll('.ex-grid')].map((root) => ({
        root: root.id,
        journal: root[Symbol.for('ex-grid.input-journal')]?.() ?? null,
    }))).catch((error) => [{ unread: String(error) }]);
}

// With the core's side, which the Server host writes to its log under the journal's category; on
// WebAssembly the core runs in the page and logs nothing (CON-8), so the script's side is the record.
async function attachJournals(testInfo, journals, hostLog) {
    if (journals === null) {
        return;
    }
    const core = hostLog.filter((line) => line.includes(' ExGrid.InputOrder: '));
    const body = JSON.stringify({ script: journals, core }, null, 1);
    await testInfo.attach('input-journal.json', { contentType: 'application/json', body });
    // And in the run's own output, on one line, where a failure's report prints it: a run's log can be
    // read where its artifacts cannot be fetched.
    console.log(`input-journal ${JSON.stringify({ script: journals, core })}`);
}

// A test with a document of its own (ADR-0056): a context of its own, every navigation real.
async function ownPage(app, page, use, options, testInfo) {
    await watchForHang(page);
    holdForcedPresses(page);
    app.startHostLog();
    const sink = newSink();
    listen(page, () => sink);
    page.goto = thenReady(page, page.goto.bind(page));
    page.reload = thenReady(page, page.reload.bind(page));

    await use(page);
    const journals = await readJournals(page, testInfo);

    // A test that raised the round trip, or held the host's answers, leaves the wire as the
    // next one expects it.
    await holdAnswers(false);
    await dropCircuit(false);
    await undoAlterations(page);
    await setRoundTrip(0);
    for (const pattern of options.expectedLeaks) {
        expect.soft(false, `the harness named ${pattern}: a document of the test's own is not checked`).toBe(true);
    }
    const hostLog = app.hostLog();
    await attachJournals(testInfo, journals, hostLog);
    verdict(sink, hostLog, options, testInfo);
}

async function sharedPage(app, context, viewport, use, options, testInfo) {
    app.startHostLog();
    const page = app.page;
    // What the app said since the last test ended — its boot, for the first — is this test's.
    const sink = app.sink;
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
    for (const name of PAGE_METHODS_THAT_OUTLIVE_A_NAVIGATION) {
        wrap(page, name, (call) => (...args) => { owned = true; return call(...args); });
    }
    for (const name of CONTEXT_METHODS_THAT_OUTLIVE_A_NAVIGATION) {
        wrap(context, name, (call) => (...args) => { owned = true; return call(...args); });
    }
    const held = new Set();
    wrap(page.keyboard, 'down', (down) => (key, ...rest) => { held.add(`the key ${key}`); return down(key, ...rest); });
    wrap(page.keyboard, 'up', (up) => (key, ...rest) => { held.delete(`the key ${key}`); return up(key, ...rest); });
    wrap(page.mouse, 'down', (down) => (o) => { held.add(`the ${o?.button ?? 'left'} button`); return down(o); });
    wrap(page.mouse, 'up', (up) => (o) => { held.delete(`the ${o?.button ?? 'left'} button`); return up(o); });
    wrap(page, 'goto', (goto) => async (url, gotoOptions) => {
        const target = new URL(url, BASE_URL);
        // A navigation away from the app leaves it: the page is the test's own from then on.
        if (owned || target.origin !== new URL(BASE_URL).origin) {
            owned = true;
            return thenReady(page, goto)(url, gotoOptions);
        }
        if (!app.booted) {
            await app.boot(goto, gotoOptions);
        }
        await arriveAt(page, target);
        return null;
    });
    // A reload is a boot the file's record was not taken from: the page is the test's own.
    wrap(page, 'reload', (reload) => (...args) => {
        owned = true;
        return thenReady(page, reload)(...args);
    });
    holdForcedPresses(page);
    const listenersBefore = new Map(page.eventNames().map((name) => [name, page.listeners(name)]));

    await use(page);
    const journals = await readJournals(page, testInfo);

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
    await holdAnswers(false);
    await dropCircuit(false);
    await undoAlterations(page);
    await setRoundTrip(0);
    for (const other of context.pages().filter((p) => p !== page)) {
        await other.close().catch(() => { });
    }

    // Leaving the test's page disposes it here, inside the test, so what the disposal says —
    // now or in the frames after — is this test's; then what it left outside its page is
    // looked for.
    const failed = testInfo.status !== 'passed' && testInfo.status !== 'skipped';
    const handOn = !failed && !owned && held.size === 0 && !page.isClosed() && app.booted !== null;
    const leaks = [];
    if (handOn) {
        try {
            await navigateInApp(page, '/');
            await twoFrames(page);
            leaks.push(...await app.leaks());
        } catch (error) {
            leaks.push(`the page could not be left for the index: ${error.message}`);
        }
    }
    // From here on, what the app says is the next test's.
    app.sink = newSink();

    // Every check is soft, so a leak does not hide the console record; any that fails keeps the
    // page from the next test, as a failure in the test's own body does.
    const errorsBefore = testInfo.errors.length;
    for (const pattern of options.expectedLeaks) {
        expect.soft(leaks.some((leak) => pattern.test(leak)), `the harness named ${pattern}`).toBe(true);
    }
    expect.soft(leaks.filter((leak) => !options.expectedLeaks.some((pattern) => pattern.test(leak))),
        'nothing left outside the test\'s own page (ADR-0056)').toEqual([]);
    const hostLog = app.hostLog();
    await attachJournals(testInfo, journals, hostLog);
    verdict(sink, hostLog, options, testInfo);
    if (!handOn || leaks.length > 0 || testInfo.errors.length > errorsBefore) {
        await app.close();
    }
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
    // The app a spec file boots once (ADR-0056), held by the worker from one test to the next.
    app: [async ({ }, use) => {
        const app = new FileApp();
        await use(app);
        await app.close();
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
    // A console error the browser writes by design, provoked on purpose by the test that pins it,
    // and named here the same way: not a CON-1 failure, and asserted to appear.
    expectedErrors: [[], { option: true }],
    // Uncaught errors a test provokes on purpose, by pattern; each must appear.
    expectedPageErrors: [[], { option: true }],
    // A change a test makes outside its own page without alterPage, provoked on purpose by the
    // harness's own tests (harness.spec.mjs) and named here: asserted to be reported, and not
    // a failure (ADR-0056).
    //
    // Each of these three lists is given one pattern at a time — /a|b/ for two things. Given to
    // test.use, a list whose second item is an object, and a RegExp is one, is read as
    // Playwright's own [value, options] pair, and the option becomes the first pattern alone.
    expectedLeaks: [[], { option: true }],
    // A document of the test's own: a context of its own and every navigation real (ADR-0056).
    freshDocument: [false, { option: true }],
    // The test's context is the file's app's, so what it grants reaches the page it drives; what it
    // granted is taken back as it ends, and what its `use` grants is granted again for the next.
    // Every context is made with the test's own `use` options — Playwright's, as its own context
    // fixture makes them.
    context: async ({ app, browser, viewport, freshDocument, _combinedContextOptions }, use, testInfo) => {
        if (viewport === null || freshDocument) {
            const context = widened(await browser.newContext({ ..._combinedContextOptions, baseURL: BASE_URL, viewport }));
            await use(context);
            await context.close();
            return;
        }
        await app.open(browser, testInfo, _combinedContextOptions, viewport);
        const context = app.context;
        if (_combinedContextOptions.permissions?.length) {
            await context.grantPermissions(_combinedContextOptions.permissions);
        }
        await use(context);
        await context.clearPermissions().catch(() => { });
    },
    page: async ({ app, context, viewport, freshDocument, expectedHostLog, expectedWarnings, expectedErrors, expectedPageErrors, expectedLeaks }, use, testInfo) => {
        const options = { expectedHostLog, expectedWarnings, expectedErrors, expectedPageErrors, expectedLeaks };
        if (viewport === null || freshDocument) {
            await ownPage(app, await context.newPage(), use, options, testInfo);
        } else {
            await sharedPage(app, context, viewport, use, options, testInfo);
        }
    },
});

/**
 * Ticks or unticks a pivot Field List entry with a click, and reads it once it shows the state. A
 * press on an entry, which the browser may drag, is let through behind an input still being answered,
 * but its click waits for its turn among the held inputs (ADR-0362), and the tick turns only then.
 * Playwright's `check()` reads the box straight after its click, which is too early.
 */
export async function setTicked(box, ticked = true) {
    if (await box.isChecked() !== ticked) {
        await box.click();
    }
    await expect(box).toBeChecked({ checked: ticked });
}

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
    await alterPage(page, ({ keys, preventAfter }) => {
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

// Waits until a grid (its `.ex-grid` root) has been told its Layout Ceiling (ADR-0053): its spacer
// is declared no taller than the browser lays its probe out. The ceiling is told after attach, by
// a ResizeObserver, so it can arrive after the grid stopped being busy — ADR-0053 records it doing
// so on a circuit, and rejected keeping the grid busy until it has. Until then the grid computes
// through the scale-1 ceiling, which compresses nothing: at 150% the browser clamps the declared
// 28,000,028 px spacer, and a reveal or a scroll aimed through that geometry lands where the grid
// is not painting. CI hit exactly that on the first test of a shard, on both hosts: the spacer was
// still 28,000,028 px when Ctrl+End was pressed. A test about what a grid does once it knows its
// geometry waits here first. At scale 1 nothing is compressed and this returns at once. The
// length is read from the style attribute, never the CSSOM, which rounds it (ADR-0053).
/**
 * Returns once the grid says it has settled (ADR-0220): its root carries `data-ex-settled` while
 * every input its key gate has taken — a key, a press on the rows, a paste, a composition, a copy —
 * has been handled and its effect is in the DOM, with nothing held, no hand-off of the keyboard under
 * way, no render owed on a timer of its own, and a reveal's write and repaint over. Await it after the
 * inputs, before a reading: it stands for `circuitQuiet()` and for `revealRepainted` there, on both
 * hosts, and it waits for the grid's own state rather than for the wire to go still.
 *
 * Text typed into a field (the Cell Editor, the Formula Bar, the Name Box) and a click Blazor hears
 * on its own (a heading, a menu, a button of the Chrome) are not the gate's: read those with an
 * expectation, then `circuitQuiet()`. Nor is an effect a Consumer carries to another grid: await that
 * grid's mark once an expectation has seen the effect begin there.
 *
 * `grid` is the grid's root (`.ex-grid`), or a locator inside one, whose nearest root is meant.
 */
export async function settled(grid, { timeout = 10_000 } = {}) {
    const root = grid.locator('xpath=ancestor-or-self::*[contains(concat(" ", normalize-space(@class), " "), " ex-grid ")][1]');
    await expect(root, 'the grid has settled (data-ex-settled, ADR-0220)').toHaveAttribute('data-ex-settled', '', { timeout });
}

/**
 * Returns once a reveal's repaint is over (ADR-0012, 2026-10-08): after a reveal's write the grid
 * moves the offset a pixel in the next animation frame and back in the one after. A measurement
 * made as soon as the offset reads the target can land between the two and see the pixel. Called
 * once the reveal's write has been seen, the two frames awaited here come after the repaint's
 * own, since animation frame callbacks run in the order they were asked for.
 */
export async function revealRepainted(page) {
    await page.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve))));
}

export async function layoutCeilingTold(grid) {
    await expect.poll(() => grid.evaluate((root) => {
        const spacer = root.querySelector(':scope > .ex-scroller > .ex-spacer');
        const declared = Number(/(?:^|[;\s])height:\s*([\d.]+)px/.exec(spacer.getAttribute('style') ?? '')?.[1]);
        const ceiling = root.querySelector(':scope > .ex-ceiling-probe > div').getBoundingClientRect().height;
        return declared <= ceiling;
    }), { message: 'the grid was never told its Layout Ceiling (ADR-0053)', timeout: 10_000 }).toBe(true);
}

// The rows a grid's ARIA count holds before its first data row (ADR-0033, decided with the user
// 2026-10-12): its header, when that is one row; a row per tier of Header Groups and the leaf row,
// when the header is a rowgroup; none while the header is hidden. A data row's aria-rowindex is its
// 0-based position plus this plus one, and aria-rowcount the data rows' total plus this. Read from
// the grid's own header, never one of a grid nested in its cells.
export const headerRowsOf = (grid) => grid.evaluate((root) => {
    const header = root.querySelector(':scope > .ex-scroller > .ex-spacer > .ex-header');
    return !header ? 0 : header.getAttribute('role') === 'row' ? 1 : header.querySelectorAll(':scope > [role=row]').length;
});

// How many data rows a grid says it has: its aria-rowcount less its header rows (ADR-0033).
export const dataRowCount = (grid) => grid.evaluate((root) => {
    const header = root.querySelector(':scope > .ex-scroller > .ex-spacer > .ex-header');
    const headerRows = !header ? 0 : header.getAttribute('role') === 'row' ? 1 : header.querySelectorAll(':scope > [role=row]').length;
    return Number(root.getAttribute('aria-rowcount')) - headerRows;
});

// The painted row at `row`, 0-based among the data rows, of a grid with `headerRows` header rows
// (headerRowsOf): its aria-rowindex counts the header's rows first (ADR-0033).
export const rowAt = (grid, row, headerRows) => grid.locator(`.ex-row[aria-rowindex="${row + 1 + headerRows}"]`);

// Scrolls a grid (its `.ex-grid` root) so that `row`, 0-based among the rows its scrollbar spans,
// is the top row of the readable area — through ADR-0053's mapping, never as row × row height.
// Above the Layout Ceiling the spacer is compressed and a scroll offset s shows the content offset
// c(s) = s × k, k = (H − V) / (S − V − 2): H the true height (the data rows × row height), S the
// rows' part of the spacer, V the readable height, the last two less the header band. At 150% a
// Sheet's scrollTop of 99 × 28 showed row 130, not row 100 (verification/2026-09-28-windows-3).
// S and the row height are read from the style attributes the grid writes, never through the
// CSSOM, which rounds a length to six significant figures (ADR-0053). Below the ceiling k is 1 and
// the offset is row × row height exactly.
//
// Compressed, the target is one pixel into the row rather than its top edge: V as the page reads
// it (clientHeight, whole pixels) can differ from the grid's by a fraction of a pixel at a
// fractional scale, which moved k by 3e-9 on /wide at 150% and c(s) by 0.04 px at row 500,000 —
// enough to put row 499,999 first when aimed at the edge. And scrollTop is quantised to device
// pixels, so the offset is rounded up and nudged while the browser holds it short. Returns the
// offset the browser holds and k. The spacer read is the one the told ceiling gives, so this
// waits for it (layoutCeilingTold): read before, k would come out 1 at 150%.
//
// And it returns once the grid has painted the row there. The browser scrolls the rows it has at
// once; the grid paints the new slice when it is told of the scroll, over the circuit on the
// Server host, and compressed it moves the rows then, by up to (1 − 1/k) of the content offset.
// Until it has, a row stands where the old slice put it: a box read in that moment and a picture
// taken after it were rows apart, and at 150% on the Server host the Sheet's lines read nothing
// where they were (CI, 2026-10-01; locally behind an 80 ms round trip, 22 of 26 line tests).
export async function scrollRowToTop(grid, row) {
    await layoutCeilingTold(grid);
    const headerRows = await headerRowsOf(grid);
    const scrolled = await grid.evaluate((root, { row, headerRows }) => {
        const px = (style, name) => {
            const m = new RegExp(`(?:^|[;\\s])${name}:\\s*([\\d.]+)px`).exec(style ?? '');
            if (!m) throw new Error(`no ${name} in the style attribute "${style}"`);
            return Number(m[1]);
        };
        const scroller = root.querySelector(':scope > .ex-scroller');
        const spacer = scroller.querySelector(':scope > .ex-spacer');
        const header = spacer.querySelector(':scope > .ex-header');
        const band = header ? header.getBoundingClientRect().height : 0;
        const rowHeight = px(root.getAttribute('style'), '--ex-row-height');
        const contentHeight = (Number(root.getAttribute('aria-rowcount')) - headerRows) * rowHeight;
        const scrollHeight = px(spacer.getAttribute('style'), 'height') - band;
        const readable = scroller.clientHeight - band;
        const k = contentHeight > scrollHeight ? (contentHeight - readable) / (scrollHeight - readable - 2) : 1;
        const content = row * rowHeight + (k > 1 ? 1 : 0);
        scroller.scrollTop = Math.ceil(content / k);
        for (let i = 0; i < 4 && scroller.scrollTop * k < content; i++) {
            scroller.scrollTop = Math.ceil(scroller.scrollTop) + 1;
        }
        return { scrollTop: scroller.scrollTop, k };
    }, { row, headerRows });
    // Where the row stands is no witness: scrolled to row 0 at 150% it moves 0.42 px, and the slice
    // is rounded to a device pixel, a third of a pixel either way. The grid's own word is: the
    // offset it wrote on the Viewport before rounding (ExGrid.razor, ViewportStyle), which with the
    // row's place inside the Viewport is ADR-0053's r × h − c(s) + s for the offset it painted.
    await expect.poll(() => grid.evaluate((root, { row, k, headerRows }) => {
        const scroller = root.querySelector(':scope > .ex-scroller');
        const viewport = scroller.querySelector(':scope > .ex-spacer > .ex-viewport');
        const painted = viewport?.querySelector(`.ex-row[aria-rowindex="${row + 1 + headerRows}"]`);
        const written = /translateY\(round\(nearest,\s*(-?[\d.]+(?:[eE][-+]?\d+)?)px/.exec(viewport?.getAttribute('style') ?? '');
        if (!painted || !written) return false;
        const rowHeight = Number(/(?:^|[;\s])--ex-row-height:\s*([\d.]+)px/.exec(root.getAttribute('style'))[1]);
        const inside = painted.getBoundingClientRect().top - viewport.getBoundingClientRect().top;
        const s = scroller.scrollTop;
        return Math.abs(Number(written[1]) + inside - (row * rowHeight - s * k + s)) < 0.1;
    }, { row, k: scrolled.k, headerRows }), { message: `the grid never painted row ${row} for the offset the browser holds (ADR-0053)`, timeout: 10_000 }).toBe(true);
    return scrolled;
}
