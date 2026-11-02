import { spawn } from 'node:child_process';
import path from 'node:path';
import readline from 'node:readline';
import { alterPage, expect } from './fixtures.mjs';

// Keys through Windows' own input queue (ADR-0450), for the rows the Definition of Done once checked by
// hand on a Windows desktop: whether the browser's own shortcuts and menu meet a key before the page
// takes it. A key Playwright sends is dispatched to the page alone, so a test of it passes whether or
// not the browser would have taken the key first; one sent here reaches the browser as a keyboard's
// does. windows/os-keys.ps1 does the sending, one process for the worker, started at the first key.

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
    const title = await page.title();
    if (title === '') {
        throw new Error('The page has no title, so the window in front cannot be told to be its own: give it one.');
    }
    const front = await ask('foreground');
    if (!front.startsWith(title)) {
        throw new Error(`The window in front is "${front}", not the page's "${title}": keys sent now would go there.`);
    }
    await ask(`keys ${chords.join(' ')}`);
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
