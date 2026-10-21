import fs from 'node:fs';
import { execFile } from 'node:child_process';

// Temporary evidence for the unresolved Row Heading renderer stop. The original test and
// timeout remain the verdict. This probe changes no page state. If the gesture has not
// completed after ten seconds, it asks the debugger where the renderer is.
export async function watchRenderer(page, testInfo) {
    const rendererPids = new Set();
    if (process.platform === 'linux') {
        const browserSession = await page.context().browser().newBrowserCDPSession();
        try {
            const { processInfo } = await browserSession.send('SystemInfo.getProcessInfo');
            for (const process of processInfo) {
                if (process.type === 'renderer') {
                    rendererPids.add(String(process.id));
                }
            }
        } finally {
            await browserSession.detach();
        }
    }
    const session = await page.context().newCDPSession(page);
    await session.send('Debugger.enable');
    let stopped = false;
    let nativeWork = Promise.resolve();
    const write = (name, text) => {
        if (!stopped) {
            fs.writeFileSync(testInfo.outputPath(name), text);
        }
    };
    const run = (command, args) => new Promise((resolve) => {
        execFile(command, args, { timeout: 20_000, maxBuffer: 4 * 1024 * 1024 }, (error, stdout, stderr) => {
            resolve({ error, stdout, stderr });
        });
    });
    session.on('Debugger.paused', (event) => {
        write('renderer-paused.json', JSON.stringify({
            reason: event.reason,
            frames: event.callFrames.map(({ functionName, url, location }) => ({ functionName, url, location })),
        }, null, 2));
        void session.send('Debugger.resume').catch(() => {});
    });
    const timer = setTimeout(() => {
        write('renderer-probe.txt', 'The Row Heading gesture has not completed after ten seconds.\n');
        void session.send('Debugger.pause').catch((error) => write('renderer-pause-error.txt', String(error)));
        if (process.platform !== 'linux') {
            return;
        }
        nativeWork = (async () => {
            const { error, stdout, stderr } = await run('ps', ['-eo', 'pid,ppid,pcpu,rss,stat,wchan:32,args']);
            if (stopped) {
                return;
            }
            const rows = stdout.split('\n').filter((row) => rendererPids.has(row.trim().split(/\s+/)[0]));
            write('renderer-processes.txt', rows.join('\n') + '\n' + String(error ?? '') + stderr);
            await Promise.all(rows.map(async (row) => {
                const pid = row.trim().split(/\s+/)[0];
                if (!/^\d+$/.test(pid)) {
                    return;
                }
                const stack = await run('sudo', ['-n', 'gdb', '--batch', '-ex', 'set pagination off',
                    '-ex', 'thread apply all bt 20', '-p', pid]);
                write(`renderer-${pid}-native-stack.txt`, stack.stdout + stack.stderr + String(stack.error ?? ''));
            }));
        })().catch((error) => write('renderer-native-probe-error.txt', String(error)));
    }, 10_000);
    return async () => {
        stopped = true;
        clearTimeout(timer);
        // A diagnostic which did fire finishes before the shared document can reach the next
        // test. Each process is bounded above. A stopped renderer may never answer CDP, so that
        // cleanup is bounded too; the original failed test then tears its worker down.
        await nativeWork;
        let cleanupTimer;
        let detached = false;
        await Promise.race([
            (async () => {
                await session.send('Debugger.disable').catch(() => {});
                await session.detach();
                detached = true;
            })(),
            new Promise((resolve) => { cleanupTimer = setTimeout(resolve, 3_000); }),
        ]);
        clearTimeout(cleanupTimer);
        if (!detached) {
            throw new Error('The renderer diagnostic could not detach; the shared document cannot be reused.');
        }
    };
}

// Temporary native evidence for the Heading edge-drag stop, collected only after failure.
// No setup or inspection changes the gesture before its original assertion returns a verdict.
export async function captureFailedRenderer(page, testInfo) {
    if (process.platform !== 'linux') return;
    const rendererPids = new Set();
    const session = await page.context().browser().newBrowserCDPSession();
    try {
        const { processInfo } = await session.send('SystemInfo.getProcessInfo');
        for (const process of processInfo) {
            if (process.type === 'renderer') rendererPids.add(String(process.id));
        }
    } finally {
        await session.detach();
    }
    const save = (name, text) => fs.writeFileSync(testInfo.outputPath(`after-failure-${name}`), text);
    const run = (command, args) => new Promise((resolve) => {
        execFile(command, args, { timeout: 20_000, maxBuffer: 4 * 1024 * 1024 }, (error, stdout, stderr) => {
            resolve({ error, stdout, stderr });
        });
    });
    // The unchanged assertion has already failed. Native inspection cannot create that verdict.
    save('renderer-probe.txt', `Capturing after ${testInfo.status}: ${testInfo.error?.message ?? ''}\n`);
    const { error, stdout, stderr } = await run('ps', ['-eo', 'pid,ppid,pcpu,rss,stat,wchan:32,args']);
    const rows = stdout.split('\n').filter((row) => rendererPids.has(row.trim().split(/\s+/)[0]));
    save('renderer-processes.txt', rows.join('\n') + '\n' + String(error ?? '') + stderr);
    await Promise.all(rows.map(async (row) => {
        const pid = row.trim().split(/\s+/)[0];
        if (!/^\d+$/.test(pid)) return;
        // GNU timeout bounds the entire sudo/gdb process group, including a child
        // that sudo might otherwise leave behind when execFile times out.
        const stack = await run('timeout', ['--signal=TERM', '--kill-after=2s', '15s',
            'sudo', '-n', 'gdb', '--batch', '-ex', 'set pagination off',
            '-ex', 'thread apply all bt 40', '-ex', 'thread 1', '-ex', 'info registers',
            '-ex', 'x/16i $pc', '-ex', 'info proc mappings', '-p', pid]);
        save(`renderer-${pid}-native-stack.txt`, stack.stdout + stack.stderr + String(stack.error ?? ''));
    }));
}
