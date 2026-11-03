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
 * Starts recording the keys `page` sees, by `key`, in the capture phase on its window, before any of
 * the page's own listeners: their `keydown`, or with `type` their `keyup`. Answers a function that reads
 * them.
 */
export async function keysSeenBy(page, kind = 'keydown') {
    const name = `exOsKeys${++calls}`;
    await alterPage(page, ({ record, type }) => {
        const seen = [];
        window[record] = seen;
        const note = (event) => seen.push(event.key);
        window.addEventListener(type, note, true);
        return () => {
            window.removeEventListener(type, note, true);
            delete window[record];
        };
    }, { record: name, type: kind });
    return () => page.evaluate((record) => window[record], name);
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

/**
 * Sends the chords as osKeysThen does, then waits until the page has seen each of their keys released:
 * a key an IME takes reaches the page as `Process`, and the composition it changes is told before its
 * keyup, so what the keys composed can be read once this returns. VK_IME_ON and VK_IME_OFF ('imeon',
 * 'imeoff') are the IME's alone and are not waited for; the keys after them are, and the IME takes keys
 * in the order they were sent. What the page then did with them is the caller's to read.
 */
export async function osTyped(page, ...chords) {
    const keys = chords.flatMap((chord) => chord.split('+')).filter((key) => key !== 'imeon' && key !== 'imeoff');
    const released = await keysSeenBy(page, 'keyup');
    await osKeysThen(page, ...chords);
    await expect.poll(async () => (await released()).length, {
        message: `${chords.join(', ')}, sent through the OS: the page saw fewer than ${keys.length} keys released`,
    }).toBeGreaterThanOrEqual(keys.length);
}

/**
 * Brings the page's tab and window to the front and switches the window to the Japanese keyboard and the
 * Microsoft IME (ED-30), which windows/japanese-input.ps1 installs, and reads where the page is on the
 * screen for osPressed. The IME starts off: 'imeon' sent through osTyped switches it on.
 */
export async function osJapanese(page) {
    await page.bringToFront();
    await inFront(page, 'the keyboard\'s switch');
    await ask('japanese');
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
    const name = `exOsMoves${++calls}`;
    await alterPage(page, ({ record }) => {
        const moves = [];
        window[record] = moves;
        const note = (event) => moves.push([event.clientX, event.clientY]);
        window.addEventListener('mousemove', note, true);
        return () => {
            window.removeEventListener('mousemove', note, true);
            delete window[record];
        };
    }, { record: name });
    const moves = () => page.evaluate((record) => window[record], name);
    const frame = await page.evaluate(() => ({ x: screenX, y: screenY, outer: [outerWidth, outerHeight], inner: [innerWidth, innerHeight], dpr: devicePixelRatio }));
    // The window's frame is as wide on each side as below the page; the rest of its height is above it.
    const side = (frame.outer[0] - frame.inner[0]) / 2;
    let x = (frame.x + side) * frame.dpr;
    let y = (frame.y + frame.outer[1] - frame.inner[1] - side) * frame.dpr;
    const landed = [];
    // Each try aims at a point of its own, so the pointer always moves and the page always hears it.
    for (let tries = 0; tries < 4; tries++) {
        const aim = [Math.round(frame.inner[0] / 2) + tries * 7, Math.round(frame.inner[1] / 2) + tries * 5];
        const heard = (await moves()).length;
        await inFront(page, 'a pointer move');
        await ask(`move ${Math.round(x + aim[0] * frame.dpr)} ${Math.round(y + aim[1] * frame.dpr)}`);
        await expect.poll(async () => (await moves()).length, { message: `the pointer, moved to the page's ${aim}, never reached the page: it landed outside it, after ${JSON.stringify(landed)}` })
            .toBeGreaterThan(heard);
        const at = (await moves()).at(-1);
        landed.push({ aim, at });
        if (at[0] === aim[0] && at[1] === aim[1]) {
            const origin = { x, y, dpr: frame.dpr };
            origins.set(page, origin);
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
    const box = await locator.evaluate((element) => {
        element.scrollIntoView({ block: 'nearest', inline: 'nearest' });
        const rect = element.getBoundingClientRect();
        return { x: rect.x, y: rect.y, width: rect.width, height: rect.height };
    });
    if (box.width === 0 || box.height === 0) {
        throw new Error(`Nothing is painted to press: ${locator}`);
    }
    const x = box.x + (position ? position.x : box.width / 2);
    const y = box.y + (position ? position.y : box.height / 2);
    await inFront(page, 'a press');
    await ask(`press ${Math.round(origin.x + x * origin.dpr)} ${Math.round(origin.y + y * origin.dpr)}`);
}
