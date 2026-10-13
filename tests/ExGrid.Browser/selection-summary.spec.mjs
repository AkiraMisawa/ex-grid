import { test, expect } from './fixtures.mjs';
import { openSheet, sheet, enter, clickCell, pressCell, cell } from './sheet-helpers.mjs';

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

// A Sheet answers from its own cells (ADR-0130). The seventeenth Windows run read Excel's status bar
// with all six figures ticked (verification/2026-10-11-windows-17, Part B): over 1, 2, =1/0, x and a
// blank, "Count: 4    Numerical Count: 2" and nothing else (b5, SM-7); with the error cleared, all six
// (b6). Column F of /sheet is empty, so the run's A1:A5 is typed there.

// Each figure as the status line shows it: the built-in Chrome's text, or ExGrid.MudBlazor's caption.
async function figureTexts(grid) {
    return grid.locator('.ex-summary .ex-summary-figure, .ex-summary .mud-ex-grid-summary-figure').allInnerTexts();
}

// Ticks a figure in the status line's menu and waits for the tick: the Sheet holds the choice, and
// a second press read against the choice before the first landed would undo the first.
async function tick(page, name) {
    const item = page.locator('.ex-summary-menu-item').filter({ hasText: new RegExp(`^\\W*${name}$`) });
    await expect(item).toHaveAttribute('aria-checked', 'false');
    await item.click();
    await expect(item).toHaveAttribute('aria-checked', 'true');
}

for (const chrome of ['builtin', 'mud']) {
    test.describe(`a Sheet under the ${chrome} Chrome`, () => {
        test('an error among the cells leaves Count and Numerical Count, as Excel\'s status bar (SM-7, ADR-0130, run 17 b5 and b6)', async ({ page }) => {
            await openSheet(page, chrome);
            const book = sheet(page);
            await enter(page, book, 'F1', '1');
            await enter(page, book, 'F2', '2');
            await enter(page, book, 'F3', '=1/0');
            await enter(page, book, 'F4', 'x');
            await expect(cell(book, 'F3')).toHaveText('#DIV/0!');

            // A press on F1, Shift+press on F5, as the run selected A1:A5.
            await pressCell(book, 'F1');
            await clickCell(book, 'F5', { modifiers: ['Shift'] });
            await expect.poll(() => figureTexts(book)).toEqual(['Count: 4']);

            // Excel's Customise Status Bar: Numerical Count, Min and Max ticked beside the default three.
            await book.locator('.ex-summary').click({ button: 'right' });
            await tick(page, 'Numerical Count');
            await tick(page, 'Min');
            await tick(page, 'Max');
            await page.keyboard.press('Escape');
            await expect(page.locator('.ex-summary-menu')).toHaveCount(0);

            await expect.poll(() => figureTexts(book)).toEqual(['Count: 4', 'Numerical Count: 2']);

            // b6: F3 cleared, F1:F5 again: every figure is back.
            await pressCell(book, 'F3');
            await page.keyboard.press('Delete');
            await expect(cell(book, 'F3')).toHaveText('');
            await pressCell(book, 'F1');
            await clickCell(book, 'F5', { modifiers: ['Shift'] });
            await expect.poll(() => figureTexts(book))
                .toEqual(['Average: 1.5', 'Count: 3', 'Numerical Count: 2', 'Min: 1', 'Max: 2', 'Sum: 3']);
        });

        test('a refused cell among the cells leaves no figure, and the line says why (ADR-0130, 2026-10-13)', async ({ page }) => {
            await openSheet(page, chrome);
            const book = sheet(page);
            await enter(page, book, 'F1', '1');
            // MROUND-001: a fractional multiple ExSheet refuses, as it cannot answer it as Excel does.
            await enter(page, book, 'F2', '=MROUND(6.05,0.1)');
            await expect(cell(book, 'F2')).toHaveText('#VALUE!');

            await pressCell(book, 'F1');
            await clickCell(book, 'F2', { modifiers: ['Shift'] });

            await expect(book.locator('.ex-summary')).toHaveText(
                "No figures: ExSheet refused the Formula in F2 with #VALUE! because it cannot answer it as Excel does, so a figure over it would not be Excel's.");
            await expect.poll(() => figureTexts(book)).toEqual([]);

            // F2 typed over with a number: the figures are back.
            await enter(page, book, 'F2', '3');
            await pressCell(book, 'F1');
            await clickCell(book, 'F2', { modifiers: ['Shift'] });
            await expect.poll(() => figureTexts(book)).toEqual(['Average: 2', 'Count: 2', 'Sum: 4']);
        });
    });
}
