import { spawn } from 'node:child_process';
import path from 'node:path';
import readline from 'node:readline';
import { alterPage, expect } from './fixtures.mjs';

// Keys through Windows' own input queue (ADR-0450), for the rows the Definition of Done once checked by
// hand on a Windows desktop: whether the browser's own shortcuts and menu meet a key before the page
// takes it, and what a real IME makes of the keys (ED-30). A key Playwright sends is dispatched to the
// page alone, so a test of it passes whether or not the browser would have taken the key first, and no
// IME ever sees it; one sent here reaches the browser as a keyboard's does. So does a press sent here,
// which the browser, and an IME composing in it, meet before the page. windows/os-keys.ps1 does the
// sending, one process for the worker, started at the first key.

/** Whether keys can go through the OS input queue here: a Windows desktop session, as CI's runner has. */
export const onWindowsDesktop = process.platform === 'win32';

let helper = null;

function start() {
    const child = spawn('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', path.join(import.meta.dirname, 'windows', 'os-keys.ps1')],
        { stdio: ['pipe', 'pipe', 'inherit'] });
    const lines = readline.createInterface({ input: child.stdout });
    const waiting = [];
    // What went wrong outside an answer: a line nobody asked for, the helper failing to start or its pipe
    // breaking. Thrown from a stream's callback it would end the worker, and every test left with it; held
    // here, the next key sent fails with it, by name.
    let broken = null;
    const end = (why) => {
        broken ??= why;
        for (const answer of waiting.splice(0)) answer(`err ${why}`);
    };
    lines.on('line', (line) => {
        const answer = waiting.shift();
        if (answer === undefined) {
            end(`os-keys.ps1 said what nobody asked: ${line}`);
            child.kill();
            return;
        }
        answer(line);
    });
    child.on('error', (error) => end(`os-keys.ps1 could not run: ${error.message}`));
    child.stdin.on('error', (error) => end(`os-keys.ps1's input broke: ${error.message}`));
    child.on('exit', (code) => end(`os-keys.ps1 ended with ${code}`));
    // The helper reads until its input closes, which the worker's exit does; it never holds the worker open.
    child.unref();
    child.stdin.unref();
    child.stdout.unref();
    const send = (command) => new Promise((resolve) => {
        if (broken !== null) {
            resolve(`err ${broken}`);
            return;
        }
        waiting.push(resolve);
        child.stdin.write(`${command}\n`);
    });
    return { child, send };
}

async function ask(command) {
    if (!onWindowsDesktop) throw new Error('Keys through the OS input queue need a Windows desktop session (ADR-0450).');
    helper ??= start();
    const line = await helper.send(command);
    if (!line.startsWith('ok ')) throw new Error(`os-keys.ps1 refused "${command}": ${line}`);
    return line.slice(3);
}

let calls = 0;

/**
 * Starts recording the events of `type` that `page` sees, in the capture phase on its window, before
 * any of the page's own listeners: of each, `property`, or with several, their values in that order.
 * Answers a function that reads them, whose `stop` ends the recording early; the harness ends it, and
 * takes the record away, as the test ends (alterPage).
 */
export async function eventsSeenBy(page, type, ...properties) {
    const name = `exOsSeen${++calls}`;
    await alterPage(page, ({ record, kind, read }) => {
        const seen = [];
        window[record] = seen;
        const note = (event) => seen.push(read.length === 1 ? event[read[0]] : read.map((p) => event[p]));
        const stop = () => window.removeEventListener(kind, note, true);
        window[`${record}Stop`] = stop;
        window.addEventListener(kind, note, true);
        return () => {
            stop();
            delete window[record];
            delete window[`${record}Stop`];
        };
    }, { record: name, kind: type, read: properties });
    const read = () => page.evaluate((record) => window[record], name);
    read.stop = () => page.evaluate((record) => window[`${record}Stop`](), name);
    return read;
}

/**
 * Starts recording the keys `page` sees, by `key`, in the capture phase on its window, before any of
 * the page's own listeners: their `keydown`, or with `type` their `keyup`. Answers a function that reads
 * them.
 */
export function keysSeenBy(page, kind = 'keydown') {
    return eventsSeenBy(page, kind, 'key');
}

/**
 * Sends each chord through the OS input queue, all in one SendInput call, refusing when the window in
 * front is not the page's. Waits for nothing: what the keys did is the caller's to read. It leaves the
 * window as it is, so a key that follows one the browser took meets what that key opened: bringing the
 * page to the front would give it the keyboard back.
 */
export async function osKeysThen(page, ...chords) {
    await inFront(page, 'keys');
    await ask(`keys ${chords.join(' ')}`);
}

/** Refuses unless the window in front is the page's: what is sent through the OS goes to that window. */
async function inFront(page, what) {
    const title = await page.title();
    if (title === '') {
        throw new Error('The page has no title, so the window in front cannot be told to be its own: give it one.');
    }
    const front = await ask('foreground');
    if (!front.startsWith(title)) {
        throw new Error(`The window in front is "${front}", not the page's "${title}": ${what} sent now would go there.`);
    }
}

/** Brings the page's tab and window to the front, then sends the chords as osKeysThen does. */
export async function osKeysSent(page, ...chords) {
    await page.bringToFront();
    await osKeysThen(page, ...chords);
}

async function landOn(send, from, to, chords) {
    const sentinel = chords.at(-1);
    if (sentinel === undefined || !/^f\d+$/i.test(sentinel)) {
        throw new Error(`osKeysLandOn ends on an F key nothing binds, so where it lands is read; it was given ${chords.join(', ')}`);
    }
    const last = sentinel.toUpperCase();
    const seenFrom = await keysSeenBy(from);
    const seenTo = to === from ? seenFrom : await keysSeenBy(to);
    await send(from, ...chords);
    await expect.poll(async () => (await seenTo()).includes(last), {
        message: `${chords.join(', ')}, sent through the OS: ${last} never reached ${to === from ? 'the page' : 'the other page'}, so a key before it went elsewhere`,
    }).toBe(true);
    return { from: await seenFrom(), to: await seenTo() };
}

/**
 * Sends the chords from `from`'s tab, brought to the front, then waits until `to` has seen the last key,
 * an F key nothing binds: every key before it has reached the browser by then. Answers the keys each page
 * saw, by `key`.
 */
export function osKeysLandOn(from, to, ...chords) {
    return landOn(osKeysSent, from, to, chords);
}

/** As osKeysLandOn, from the window as it is (osKeysThen). */
export function osKeysThenLandOn(from, to, ...chords) {
    return landOn(osKeysThen, from, to, chords);
}

/**
 * Brings the page's tab and window to the front, then sends each chord ('ctrl+1', 'alt', 'f10', 'h')
 * through the OS input queue, all in one SendInput call: the keys of a person typing at machine speed.
 * Then F24, which nothing binds, and waits until the page has seen it: the keys before it have all
 * reached the browser by then, and a key the browser took at once (a tab switched to) left F24 to
 * another tab, so the wait fails naming the chords. A key the page left to the browser can also be acted
 * on only once the page has answered it, after the keys sent with it have landed: Alt released alone
 * opened Edge's menu after the next key had reached the page (ADR-0450's control, 2026-11-02). So a
 * second F24 goes in a call of its own once the first has landed, and must land too: a menu opened late
 * takes it. Answers the keys the page saw, by `key`, before the first F24.
 */
export async function osKeys(page, ...chords) {
    const { from: seen } = await osKeysLandOn(page, page, ...chords, 'f24');
    await osKeysThenLandOn(page, page, 'f24');
    return seen.slice(0, seen.indexOf('F24'));
}

// The physical key each name os-keys.ps1 sends is, as the page's `code` names it: a key the IME takes
// reaches the page as `Process`, by `key`, but keeps its `code`.
const CODES = {
    ctrl: 'ControlLeft', shift: 'ShiftLeft', alt: 'AltLeft', space: 'Space', enter: 'Enter', escape: 'Escape',
    tab: 'Tab', backspace: 'Backspace', delete: 'Delete', plus: 'Equal', minus: 'Minus', left: 'ArrowLeft',
    up: 'ArrowUp', right: 'ArrowRight', down: 'ArrowDown', home: 'Home', end: 'End', pageup: 'PageUp',
    pagedown: 'PageDown',
};

/** The `code` of each key a chord releases, in the order it releases them; none for the IME's own keys. */
function released(chord) {
    return chord.toLowerCase().split('+').reverse().flatMap((key) => {
        if (key === 'imeon' || key === 'imeoff') return [];
        if (key in CODES) return [CODES[key]];
        if (/^f\d+$/.test(key)) return [key.toUpperCase()];
        if (/^[a-z]$/.test(key)) return [`Key${key.toUpperCase()}`];
        if (/^\d$/.test(key)) return [`Digit${key}`];
        throw new Error(`osTyped has no code for the key ${key}`);
    });
}

/** Whether `wanted` appears in `seen` in order, other keys between them or not. */
function inOrder(wanted, seen) {
    let at = 0;
    for (const code of seen) {
        if (code === wanted[at]) at++;
    }
    return at === wanted.length;
}

const keyups = new WeakMap();

/**
 * Sends the chords as osKeysThen does, then waits until the page has seen each of their keys released,
 * in order, by `code`: a key an IME takes reaches the page as `Process`, and the composition it changes
 * is told before its keyup, so what the keys composed can be read once this returns. VK_IME_ON and
 * VK_IME_OFF ('imeon', 'imeoff') are the IME's alone and are not waited for; the keys after them are, and
 * the IME takes keys in the order they were sent. What the page then did with them is the caller's to read.
 */
export async function osTyped(page, ...chords) {
    if (!keyups.has(page)) {
        keyups.set(page, await eventsSeenBy(page, 'keyup', 'code'));
    }
    const seen = keyups.get(page);
    const wanted = chords.flatMap(released);
    const from = (await seen()).length;
    await osKeysThen(page, ...chords);
    await expect.poll(async () => inOrder(wanted, (await seen()).slice(from)), {
        message: `${chords.join(', ')}, sent through the OS: the page has not seen ${wanted.join(', ')} released, in that order`,
    }).toBe(true);
}

/**
 * Brings the page's tab and window to the front and switches the window to the Japanese keyboard and the
 * Microsoft IME (ED-30), which windows/japanese-input.ps1 installs, with the IME off, and reads where the
 * page is on the screen for osPressed. The IME's open status is the browser window's thread's, which an
 * earlier test's window may have left on, so it is switched off here; 'imeon' sent through osTyped
 * switches it on.
 */
export async function osJapanese(page) {
    await page.bringToFront();
    await inFront(page, 'the keyboard\'s switch');
    await ask('japanese');
    await ask('keys imeoff');
    // Where the page is on the screen, read now rather than at the first press, which may come in
    // the middle of a composition.
    await originOf(page);
}

const origins = new WeakMap();

/**
 * Where the page's client point (0, 0) is on the screen, in physical pixels, with the page's
 * devicePixelRatio: a first guess from the window's own figures, then put right by where the pointer
 * moved to lands in the page, until a point lands where it was aimed. Read once a page. The window
 * stays where it is, so it holds for every press after.
 */
async function originOf(page) {
    const known = origins.get(page);
    if (known !== undefined) {
        return known;
    }
    const moves = await eventsSeenBy(page, 'mousemove', 'clientX', 'clientY');
    const frame = await page.evaluate(() => ({ x: screenX, y: screenY, outer: [outerWidth, outerHeight], inner: [innerWidth, innerHeight], dpr: devicePixelRatio }));
    // The window's frame is as wide on each side as below the page; the rest of its height is above it.
    const side = (frame.outer[0] - frame.inner[0]) / 2;
    let x = (frame.x + side) * frame.dpr;
    let y = (frame.y + frame.outer[1] - frame.inner[1] - side) * frame.dpr;
    const landed = [];
    // Each try aims at a point the pointer is not at, so the pointer always moves and the page always
    // hears it: a pointer put where it already is sends no move, and an earlier test's calibration left
    // it at the middle of a window placed where this one is.
    let [atX, atY] = (await ask('pointer')).split(' ').map(Number);
    let step = 0;
    for (let tries = 0; tries < 4; tries++) {
        let aim;
        let screen;
        do {
            aim = [Math.round(frame.inner[0] / 2) + step * 7, Math.round(frame.inner[1] / 2) + step * 5];
            screen = [Math.round(x + aim[0] * frame.dpr), Math.round(y + aim[1] * frame.dpr)];
            step++;
        } while (screen[0] === atX && screen[1] === atY);
        const heard = (await moves()).length;
        await inFront(page, 'a pointer move');
        await ask(`move ${screen[0]} ${screen[1]}`);
        // Where the pointer went, which the screen's edge may have moved it from.
        [atX, atY] = (await ask('pointer')).split(' ').map(Number);
        await expect.poll(async () => (await moves()).length, { message: `the pointer, moved to the page's ${aim}, never reached the page: it landed outside it, after ${JSON.stringify(landed)}` })
            .toBeGreaterThan(heard);
        const at = (await moves()).at(-1);
        landed.push({ aim, at });
        if (at[0] === aim[0] && at[1] === aim[1]) {
            const origin = { x, y, dpr: frame.dpr };
            origins.set(page, origin);
            await moves.stop();
            return origin;
        }
        x += (aim[0] - at[0]) * frame.dpr;
        y += (aim[1] - at[1]) * frame.dpr;
    }
    throw new Error(`The pointer never landed where it was aimed in the page: ${JSON.stringify(landed)}`);
}

/**
 * A left press through the OS, at the middle of where `locator` is painted, or at `position` within it
 * (as Playwright's), as a mouse makes it: the browser, and an IME composing in it, meet it before the
 * page does. The element is brought into view and its place read in one step, as sheet-helpers.mjs's
 * pressAt reads it. It does not bring the window to the front, which could end a composition: the
 * window in front must already be the page's. What the press did is the caller's to read.
 */
export async function osPressed(locator, position = null) {
    const page = locator.page();
    const origin = await originOf(page);
    // Waits until it is painted, as sheet-helpers.mjs's pressAt does: on a circuit a press can come before
    // the render that paints what it presses.
    let box = null;
    await expect.poll(async () => {
        box = await locator.evaluate((element) => {
            element.scrollIntoView({ block: 'nearest', inline: 'nearest' });
            const rect = element.getBoundingClientRect();
            return { x: rect.x, y: rect.y, width: rect.width, height: rect.height };
        });
        return box.width > 0 && box.height > 0;
    }, { message: `${locator} is painted, to be pressed` }).toBe(true);
    const x = box.x + (position ? position.x : box.width / 2);
    const y = box.y + (position ? position.y : box.height / 2);
    // A mouse presses whatever is on top at the point, which may not be the element aimed at: one drawn over it
    // takes the press, and the test would read what that did as the element's doing. The element itself, one inside
    // it, or one it sits in (a cell takes no presses: its viewport takes them at the cell's place) is what was aimed at.
    const hit = await locator.evaluate((element, [px, py]) => {
        const top = document.elementFromPoint(px, py);
        const name = (node) => (node === null ? 'nothing'
            : `${node.tagName.toLowerCase()}${node.id ? `#${node.id}` : ''}.${[...node.classList].join('.')}`);
        if (top !== null && (element.contains(top) || top.contains(element))) {
            return null;
        }
        // Where each stands, and its parent's children: what put one over the other.
        const box = (node) => {
            const r = node.getBoundingClientRect();
            return `${name(node)} at (${Math.round(r.x)}, ${Math.round(r.y)}) ${Math.round(r.width)}x${Math.round(r.height)}`;
        };
        return [`${top === null ? 'nothing' : box(top)}, which is drawn over it`, `aimed at ${box(element)}`,
            ...(element.parentElement === null ? [] : [...element.parentElement.parentElement?.children ?? []].map(box))].join('; ');
    }, [x, y]);
    if (hit !== null) {
        throw new Error(`A press aimed at ${locator} at (${x}, ${y}) would land on ${hit}`);
    }
    await inFront(page, 'a press');
    await ask(`press ${Math.round(origin.x + x * origin.dpr)} ${Math.round(origin.y + y * origin.dpr)}`);
}
