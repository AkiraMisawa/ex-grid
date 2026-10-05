import { test, expect } from './fixtures.mjs';

// The Selection Summary with real keys (ADR-0130): select cells and the status line shows Excel's
// Average, Count and Sum, answered by GridSource.From — the reference — over every selected cell.
// /features is bound to GridSource.From. Run once per Chrome: the built-in text and
// ExGrid.MudBlazor's captions must give identical outcomes (SM-11). The strip stands whenever the
// grid can summarise, so selecting does not move the Viewport (SM-9).
//
// Column 2 is Notional, a decimal shown as its own text; column 1 is Trader, text.

function grid(page) {
    return page.locator('.ex-grid').first();
}

// Decimal texts summed exactly, as the grid sums them: scaled to integers, never through a float.
function exactSum(texts) {
    const places = 6;
    let total = 0n;
    for (const text of texts) {
        const [whole, fraction = ''] = text.trim().split('.');
        const negative = whole.startsWith('-');
        const digits = BigInt(whole.replace('-', '') + fraction.padEnd(places, '0'));
        total += negative ? -digits : digits;
    }
    const negative = total < 0n;
    const digits = (negative ? -total : total).toString().padStart(places + 1, '0');
    const whole = digits.slice(0, -places);
    const fraction = digits.slice(-places).replace(/0+$/, '');
    return (negative ? '-' : '') + whole + (fraction ? '.' + fraction : '');
}

async function figures(page) {
    return grid(page).locator('.ex-summary').innerText();
}

for (const chrome of ['builtin', 'mud']) {
    test.describe(`under the ${chrome} Chrome`, () => {
        test.beforeEach(async ({ page }) => {
            await page.goto(`/features?chrome=${chrome}`);
            await expect(grid(page).locator('.ex-row').first()).toBeVisible();
        });

        test('a selection of numbers shows Average, Count and Sum from every selected cell (SM-9/SM-11, ADR-0130)', async ({ page }) => {
            await grid(page).locator("[id$='r0c2']").click({ force: true });
            await page.keyboard.press('Shift+ArrowDown');
            await page.keyboard.press('Shift+ArrowDown');

            const texts = [];
            for (const row of [0, 1, 2]) {
                texts.push(await grid(page).locator(`[id$='r${row}c2']`).innerText());
            }
            const sum = exactSum(texts);
            await expect.poll(() => figures(page)).toContain(`Sum: ${sum}`);
            const shown = await figures(page);
            expect(shown).toContain('Count: 3');
            expect(shown).toContain('Average: ');
            // Excel's order: Average, Count, Sum.
            expect(shown.indexOf('Average')).toBeLessThan(shown.indexOf('Count'));
            expect(shown.indexOf('Count')).toBeLessThan(shown.indexOf('Sum'));
        });

        test('text is counted and never summed; one cell shows nothing (SM-6, ADR-0130)', async ({ page }) => {
            await grid(page).locator("[id$='r0c1']").click({ force: true });
            await expect.poll(() => figures(page)).toBe('');

            await page.keyboard.press('Shift+ArrowDown');
            await expect.poll(() => figures(page)).toContain('Count: 2');
            expect(await figures(page)).not.toContain('Sum');
        });

        test('selecting does not move the Viewport: the strip stands before the first selection (SM-9, ADR-0130)', async ({ page }) => {
            const scroller = grid(page).locator('.ex-scroller');
            const before = await scroller.boundingBox();
            const strip = await grid(page).locator('.ex-status').boundingBox();

            await grid(page).locator("[id$='r0c2']").click({ force: true });
            await page.keyboard.press('Shift+ArrowDown');
            await expect.poll(() => figures(page)).toContain('Sum: ');

            expect(await scroller.boundingBox()).toEqual(before);
            expect(await grid(page).locator('.ex-status').boundingBox()).toEqual(strip);
        });
    });
}
