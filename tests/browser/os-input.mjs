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
    lines.on('line', (line) => {
        const answer = waiting.shift();
        if (answer === undefined) throw new Error(`os-keys.ps1 said what nobody asked: ${line}`);
        answer(line);
    });
    child.on('exit', (code) => {
        for (const answer of waiting.splice(0)) answer(`err os-keys.ps1 ended with ${code}`);
        helper = null;
    });
    const send = (command) => new Promise((resolve) => {
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
 * Brings the page's tab and window to the front, then sends each chord ('ctrl+1', 'alt', 'f10', 'h')
 * through the OS input queue, all in one SendInput call: the keys of a person typing at machine speed.
 * Then F24, which nothing binds, and waits until the page has seen it: the keys before it have all
 * reached the browser by then, and each one the browser took for itself — a tab switched to, its menu
 * opened — left F24 to another tab or to the menu, so the wait fails naming the chords. Answers the
 * keys the page saw, by `key`, before F24.
 */
export async function osKeys(page, ...chords) {
    const name = `exOsKeys${++calls}`;
    await alterPage(page, (record) => {
        const seen = [];
        window[record] = seen;
        const note = (event) => seen.push(event.key);
        window.addEventListener('keydown', note, true);
        return () => {
            window.removeEventListener('keydown', note, true);
            delete window[record];
        };
    }, name);
    await page.bringToFront();
    const title = await page.title();
    const front = await ask('foreground');
    if (!front.startsWith(title)) {
        throw new Error(`The window in front is "${front}", not the page's "${title}": keys sent now would go there.`);
    }
    await ask(`keys ${[...chords, 'f24'].join(' ')}`);
    await expect.poll(() => page.evaluate((record) => window[record].includes('F24'), name), {
        message: `${chords.join(', ')} then F24, sent through the OS: F24 never reached the page, so the browser took one of the keys before it`,
    }).toBe(true);
    const seen = await page.evaluate((record) => window[record], name);
    return seen.slice(0, seen.indexOf('F24'));
}

/** The title of the window in front: the browser's names its tab in front, so it says which tab that is. */
export function foregroundTitle() {
    return ask('foreground');
}
