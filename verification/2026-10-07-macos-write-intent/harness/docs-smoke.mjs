import { createRequire } from 'node:module';
import fs from 'node:fs';
const require = createRequire(new URL('../../../tests/ExGrid.Browser/package.json', import.meta.url));
const { chromium, expect } = require('@playwright/test');
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const context = await browser.newContext({ viewport: { width: 1400, height: 1100 } });
const page = await context.newPage();
const result = { browser: browser.version(), errors: [], observations: [] };
page.on('console', message => { if (message.type() === 'error' || message.type() === 'warning') result.errors.push(message.text()); });
page.on('pageerror', error => result.errors.push(error.message));
try {
    for (const iteration of [1, 2, 3]) {
    for (const chrome of ['Built-in', 'MudBlazor']) {
        await page.goto('http://localhost:5697/exgrid/editing');
        const example = page.locator('.docs-example').filter({
            has: page.getByRole('heading', { name: 'A feed, a user edit and a Consumer decision', exact: true }),
        });
        const choice = example.getByRole('checkbox', { name: chrome, exact: true });
        if (await choice.getAttribute('aria-checked') !== 'true') await choice.click();
        await expect(example.locator('.docs-example-live')).toHaveClass(new RegExp(chrome === 'Built-in' ? 'docs-example-builtin' : 'docs-example-mud'));
        const grid = example.locator('.ex-grid');
        const price = grid.locator("[id$='r0c2']");
        const editor = grid.locator('input.ex-editor, input.mud-ex-editor');
        await expect(price).toHaveText('100.00');
        await price.click({ force: true });
        await page.keyboard.type('125');
        await page.keyboard.press('F9');
        await expect(example.locator('.docs-example-live')).toContainText('Feed update 1');
        await expect(editor).toHaveValue('125');
        await page.keyboard.press('Enter');
        await expect(price).toHaveText('125.00');
        await expect(editor).toHaveCount(0);
        await expect(example.locator('p[role=status]')).toHaveText('Written: Price=125 on position 5001.');
        await price.click({ force: true });
        await page.keyboard.type('abc');
        await page.keyboard.press('Enter');
        await expect(editor).toHaveValue('abc');
        await expect(editor).toHaveAttribute('aria-invalid', 'true');
        await expect(grid.locator('.ex-announce')).toContainText('Enter a positive price.');
        await page.keyboard.press('Escape');
        await price.click({ force: true });
        await page.keyboard.type('175');
        await page.keyboard.press('Enter');
        await expect(price).toHaveText('175.00');
        await grid.locator("[id$='r0c3'] .ex-action").click();
        await expect(example.locator('p[role=status]')).toHaveText(
            "Not approved: position 5001 exceeds this desk's price limit of 150. Request 1.");
        await example.getByRole('button', { name: 'Show code', exact: true }).click();
        await expect(example.getByRole('tab', { name: chrome === 'Built-in' ? 'LiveWrites.razor' : 'LiveWritesMud.razor', exact: true })).toBeVisible();
        await expect(example.getByRole('tab', { name: 'Position.cs', exact: true })).toBeVisible();
        result.observations.push({ iteration, chrome, commitAfterFeed: '125.00', rejectedInput: 'abc',
            consumerActionDecision: await example.locator('p[role=status]').textContent(), generatedSourceShown: true });
    }
    }
    expect(result.errors).toEqual([]);
} finally {
    fs.writeFileSync(new URL('../raw/docs-smoke.json', import.meta.url), JSON.stringify(result, null, 2));
    await browser.close();
}
