// Calibration for end-probe.mjs: /wide opened, the top right of its scroller captured, and the scroller's rect.
import { chromium } from '@playwright/test';
const browser = await chromium.launch({ channel: process.env.EXGRID_CHANNEL ?? 'chrome', headless: false, args: ['--window-position=40,40', '--window-size=1600,1000'] });
const page = await (await browser.newContext({ viewport: null })).newPage();
await page.goto(`${process.argv[2]}/wide`);
await page.locator('#demo-interactive').waitFor({ state: 'attached', timeout: 60_000 });
await page.waitForFunction(() => !document.querySelector('.ex-grid[aria-busy]'), null, { timeout: 60_000 });
await page.waitForTimeout(800);
const r = await page.evaluate(() => { const sc = document.querySelector('.ex-grid .ex-scroller'); const b = sc.getBoundingClientRect(); return { left: b.left, top: b.top, right: b.right, bottom: b.bottom, clientWidth: sc.clientWidth, clientHeight: sc.clientHeight, dpr: devicePixelRatio }; });
console.log(JSON.stringify(r));
const g = await page.evaluate(() => ({ sx: screenX, sy: screenY, ow: outerWidth, oh: outerHeight, iw: innerWidth, ih: innerHeight }));
const border = (g.ow - g.iw) / 2;
const ox = (g.sx + border) * r.dpr, oy = (g.sy + (g.oh - g.ih) - border) * r.dpr;
// The scroller's right 40 CSS px, top and bottom 120 CSS px, in screen pixels.
const rect = (y0, y1) => [Math.round(ox + (r.right - 40) * r.dpr), Math.round(oy + y0 * r.dpr), Math.round(ox + r.right * r.dpr), Math.round(oy + y1 * r.dpr)];
const { spawnSync } = await import('node:child_process');
for (const [name, [a, b]] of [['top', [r.top, r.top + 120]], ['bottom', [r.bottom - 120, r.bottom]], ['whole', [r.top - 10, r.bottom + 10]]]) {
    let [l, t, rr, bb] = rect(a, b);
    if (name === 'whole') { l = Math.round(ox + (r.left - 10) * r.dpr); rr = Math.round(ox + (r.right + 10) * r.dpr); }
    const res = spawnSync('powershell.exe', ['-NoProfile', '-Command', `Add-Type -AssemblyName System.Drawing; Add-Type -Namespace D -Name N -MemberDefinition '[DllImport(\"user32.dll\")] public static extern bool SetProcessDPIAware();'; [void][D.N]::SetProcessDPIAware(); Add-Type -Namespace C -Name M -MemberDefinition '[DllImport(\"user32.dll\")] public static extern bool SetCursorPos(int x, int y);'; [void][C.M]::SetCursorPos(${rr - 11}, ${Math.round((t + bb) / 2)}); Start-Sleep -Milliseconds 700; $b = New-Object Drawing.Bitmap ${rr - l}, ${bb - t}; $g = [Drawing.Graphics]::FromImage($b); $g.CopyFromScreen(${l}, ${t}, 0, 0, $b.Size); $b.Save('${process.argv[3].replace('.png', '-' + name + '.png')}'); 'ok'`], { encoding: 'utf8' });
    console.log(name, l, t, rr, bb, res.stdout.trim(), res.stderr.trim().slice(0, 200));
}
console.log(JSON.stringify({ ox, oy }));
await browser.close();
