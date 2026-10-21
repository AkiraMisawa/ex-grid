import fs from 'node:fs';
import { execFile } from 'node:child_process';

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
    const save = (name, text) => fs.writeFileSync(testInfo.outputPath(name), text);
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
