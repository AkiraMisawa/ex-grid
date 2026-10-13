// Diagnostic only, never committed (ADR-0205's floor run): every focusin/focusout while the
// Field Settings panel opens under the mud Chrome, and where DOM focus ends.
import { test, expect } from './fixtures.mjs';

const describe = (e) => e && e.nodeType === 1
    ? `${e.tagName}${e.getAttribute('type') ? `[type=${e.getAttribute('type')}]` : ''}${e.getAttribute('role') ? `[role=${e.getAttribute('role')}]` : ''}.${(e.className?.toString() ?? '').split(' ').slice(0, 2).join('.')}`
    : String(e);

test('floor diagnostic: focus events while Field Settings opens', async ({ page }) => {
    await page.setViewportSize({ width: 1400, height: 1100 });
    await page.goto('/pivot?chrome=mud');
    const pane = page.getByRole('region', { name: 'PivotTable Fields' });
    const entry = pane.getByRole('button', { name: 'Options for Region', exact: true });
    await expect(entry).toBeVisible({ timeout: 30_000 });
    await entry.click();
    const item = page.getByRole('menuitem', { name: 'Field Settings…', exact: true });
    await expect(item).toBeVisible();
    await page.evaluate((src) => {
        const describe = new Function(`return ${src}`)();
        const log = [];
        document.__floorFocusLog = log;
        const t0 = performance.now();
        for (const type of ['focusin', 'focusout']) {
            document.addEventListener(type, (e) => log.push(`${(performance.now() - t0).toFixed(0)}ms ${type} ${describe(e.target)}`), true);
        }
        const observer = new MutationObserver((records) => {
            for (const r of records) {
                if (r.type === 'attributes' && r.attributeName === 'type' && r.target.tagName === 'INPUT' && r.target.closest('.mud-ex-pivot-sort')) {
                    log.push(`${(performance.now() - t0).toFixed(0)}ms input type ${r.oldValue} -> ${r.target.getAttribute('type')} (focused: ${document.activeElement === r.target})`);
                }
            }
        });
        observer.observe(document.body, { subtree: true, attributes: true, attributeOldValue: true, attributeFilter: ['type'] });
    }, describe.toString());
    await item.click();
    const settings = page.getByRole('dialog', { name: 'Field Settings…', exact: true });
    await expect(settings).toBeVisible();
    await page.waitForTimeout(2000);
    const log = await page.evaluate(() => document.__floorFocusLog);
    const active = await page.evaluate((src) => new Function(`return ${src}`)()(document.activeElement), describe.toString());
    console.log(`FLOOR-FOCUS active=${active}\n  ${log.join('\n  ')}`);
});

test('floor diagnostic: the filter panel Operator as focused, and where its role and name are', async ({ page }) => {
    await page.goto('/features?chrome=mud');
    const grid = page.locator('.ex-grid').first();
    await expect(grid.locator('.ex-row').first()).toBeVisible();
    await grid.locator("[id$='r1c2']").click({ force: true });
    await page.keyboard.press('Alt+ArrowDown');
    await expect.poll(() => page.evaluate(() => !!document.activeElement?.closest('[role=menu]'))).toBe(true);
    await page.keyboard.press('Tab');
    await expect.poll(() => page.evaluate(() => !!document.activeElement?.closest('.mud-ex-grid-filter-operator'))).toBe(true);
    const found = await page.evaluate(() => {
        const a = document.activeElement;
        const root = document.querySelector('.mud-ex-grid-filter-operator');
        const say = (e) => ({ tag: e.tagName, type: e.getAttribute('type'), tabindex: e.getAttribute('tabindex'),
            role: e.getAttribute('role'), label: e.getAttribute('aria-label'), focused: e === a });
        return [...root.querySelectorAll('input, [tabindex]')].filter((e) => e.tagName !== 'svg').map(say);
    });
    const byRole = await page.locator('.ex-popover').getByRole('combobox', { name: 'Operator' }).count();
    console.log(`FLOOR-OPERATOR elements=${JSON.stringify(found)} getByRole(combobox, Operator)=${byRole}`);
});
