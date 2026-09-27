import { chromium } from '@playwright/test';
const hosts = process.argv.slice(2);
const browser = await chromium.launch({ channel: 'chrome', headless: false });
const out = [];
for (const base of hosts) {
    for (const pause of (process.env.PAUSES ?? "0,300").split(",").map(Number)) {
        for (const trial of [1, 2, 3]) {
            const page = await browser.newPage();
            await page.goto(`${base}/sheet`);
            await page.locator('#demo-interactive').waitFor({ state: 'attached' }).catch(() => {});
            await page.waitForFunction(() => !document.querySelector('.ex-grid[aria-busy]'));
            const grid = page.locator('.ex-grid').first();
            await grid.locator("[id$='-r0c0']").waitFor();
            for (const [row, v] of [[0, '1'], [1, '2'], [2, '3'], [6, '7']]) {
                await grid.locator(`[id$='-r${row}c5']`).click({ force: true });
                if (pause) await page.waitForTimeout(pause);
                await page.keyboard.type(v);
                await page.keyboard.press('Enter');
                if (pause) await page.waitForTimeout(pause);
            }
            await page.waitForTimeout(1500);
            const f = await grid.evaluate((root) => [0, 1, 2, 3, 4, 5, 6, 7].map((r) => root.querySelector(`[id$='-r${r}c5']`)?.textContent.trim()));
            const nameBox = await grid.locator('input.ex-name-box').inputValue();
            out.push({ base, pause, trial, F1_F8: f, nameBox });
            await page.close();
        }
    }
}
await browser.close();
console.log(JSON.stringify(out));
