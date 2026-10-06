import { createRequire } from 'node:module';
import fs from 'node:fs';
const require = createRequire(new URL('../../../tests/ExGrid.Browser/package.json', import.meta.url));
const { chromium, expect } = require('@playwright/test');
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const context = await browser.newContext({ viewport: { width: 1400, height: 1100 } });
const page = await context.newPage();
const result = { browser: browser.version(), errors: [], observations: [] };
page.on('console', message => { if (message.type() === 'error') result.errors.push(message.text()); });
page.on('pageerror', error => result.errors.push(error.message));
try {
    for (const chrome of ['Built-in', 'MudBlazor']) {
        await page.goto('http://localhost:5697/expivot/sources');
        const choice = page.getByRole('checkbox', { name: chrome, exact: true }).first();
        if (await choice.getAttribute('aria-checked') !== 'true') await choice.click();
        const remote = page.locator('.docs-example-live').last();
        await expect(remote).toHaveClass(new RegExp(chrome === 'Built-in' ? 'docs-example-builtin' : 'docs-example-mud'));
        const cell = remote.locator('.ex-pivot-sheet [id$="r0c1"]');
        await expect(cell).not.toHaveText('', { timeout: 30000 });
        await cell.dblclick({ force: true });
        await expect(remote.locator('.ex-pivot-details-panel .ex-row').first()).toBeVisible({ timeout: 30000 });
        result.observations.push({ page: 'sources', chrome, detailsRows: await remote.locator('.ex-pivot-details-panel .ex-row').count() });
        await page.goto('http://localhost:5697/expivot/live');
        const liveChoice = page.getByRole('checkbox', { name: chrome, exact: true }).first();
        if (await liveChoice.getAttribute('aria-checked') !== 'true') await liveChoice.click();
        const examples = page.locator('.docs-example-live');
        await expect(examples).toHaveCount(2);
        for (let i = 0; i < 2; i++) {
            const report = examples.nth(i).locator('.ex-pivot-sheet .ex-viewport');
            await expect(report.locator('.ex-row').first()).toBeVisible({ timeout: 30000 });
            const before = await report.textContent();
            await expect(report).not.toHaveText(before, { timeout: 10000 });
            result.observations.push({ page: 'live', chrome, example: i, changed: true });
        }
    }
    expect(result.errors).toEqual([]);
} finally {
    fs.writeFileSync(new URL('../raw/docs-smoke.json', import.meta.url), JSON.stringify(result, null, 2));
    await browser.close();
}
