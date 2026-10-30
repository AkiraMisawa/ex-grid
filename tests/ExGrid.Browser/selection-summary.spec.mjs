import { test, expect, alterPage, chromeHost } from './fixtures.mjs';
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

        test(`a selection of numbers shows Average, Count and Sum from every selected cell (SM-9/SM-11, ADR-0130) ${chromeHost(chrome)}`, async ({ page }) => {
            await grid(page).locator("[id$='r0c2']").click({ force: true });
            await page.keyboard.press('Shift+ArrowDown');
            await page.keyboard.press('Shift+ArrowDown');

            const texts = [];
            for (const row of [0, 1, 2]) {
                // spec-lint: early-read: the cells' own text, which a change of selection cannot change; only the summary waits for the grid
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

        test(`selecting does not move the Viewport: the strip stands before the first selection (SM-9, ADR-0130) ${chromeHost(chrome)}`, async ({ page }) => {
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
        test(`an error among the cells leaves Count and Numerical Count, as Excel's status bar (SM-7, ADR-0130, run 17 b5 and b6) ${chromeHost(chrome)}`, async ({ page }) => {
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
    });
}

// A grid whose summary the page answers itself (/features?summary=held, ADR-0130): each question
// waits for the page's Answer button, and a selection of more than four cells is declined at once
// with a reason. The pending mark (SM-2) and the reason (SM-4) are the core's behaviour, so under
// ExGrid.MudBlazor's Chrome they come out exactly as under the built-in one (SM-11): the same text
// in the same core box, and no figure beside either.
const DECLINED = 'Not summed: this page sums 4 cells at most.';

for (const chrome of ['builtin', 'mud']) {
    test.describe(`a summary the page answers, under the ${chrome} Chrome`, () => {
        const held = (page) => page.locator('#held-summary .ex-grid');
        const summary = (page) => held(page).locator('.ex-summary');
        const notional = (page, row) => held(page).locator(`[id$='r${row}c1']`);
        const sumOf = async (page, rows) => {
            const texts = [];
            for (let row = 0; row < rows; row++) {
                texts.push(await notional(page, row).innerText());
            }
            return exactSum(texts);
        };

        // The page's Answer button, pressed without taking the keyboard from the grid: the keys
        // after it go on extending the grid's selection.
        const answer = (page) => page.locator('#summary-answer').dispatchEvent('click');

        test.beforeEach(async ({ page }) => {
            await page.goto(`/features?chrome=${chrome}&summary=held`);
            await expect(held(page).locator('.ex-row').first()).toBeVisible();
        });

        test(`SM-11 / ADR-0130 (SM-2): a change of selection clears the figures at once for the pending mark, and the question it replaced is cancelled (${chrome}) ${chromeHost(chrome)}`, async ({ page }) => {
            // Every render of the grid as the browser applies it: how many rows the selection
            // covers, and what the summary says beside it.
            await alterPage(page, () => {
                const root = document.querySelector('#held-summary .ex-grid');
                const seen = [];
                const look = () => {
                    const range = root.querySelector('.ex-selection .ex-range');
                    const row = root.querySelector('.ex-viewport .ex-row');
                    seen.push({
                        rows: range && row ? Math.round(range.getBoundingClientRect().height / row.getBoundingClientRect().height) : 0,
                        text: root.querySelector('.ex-summary')?.innerText ?? '',
                    });
                };
                const observer = new MutationObserver(look);
                observer.observe(root, { subtree: true, childList: true, characterData: true, attributes: true });
                window.__heldSummarySeen = seen;
                return () => { observer.disconnect(); delete window.__heldSummarySeen; };
            });

            await notional(page, 0).click({ force: true });
            await page.keyboard.press('Shift+ArrowDown');
            await expect(summary(page)).toHaveClass(/\bex-summary-pending\b/);
            await expect(summary(page)).toHaveText('Calculating…');
            expect(await figureTexts(held(page))).toEqual([]);
            await expect(page.locator('#summary-asked')).toHaveText('Asked: 1; cancelled: 0; answered: 0');

            await answer(page);
            await expect.poll(() => figureTexts(held(page))).toContain(`Sum: ${await sumOf(page, 2)}`);
            expect(await figureTexts(held(page))).toContain('Count: 2');
            await expect(summary(page)).not.toHaveClass(/\bex-summary-pending\b/);

            // A third row: the two rows' figures go in the render that shows the third, the mark
            // in their place.
            await page.keyboard.press('Shift+ArrowDown');
            await expect(summary(page)).toHaveText('Calculating…');
            expect(await figureTexts(held(page))).toEqual([]);
            await expect(page.locator('#summary-asked')).toHaveText('Asked: 2; cancelled: 0; answered: 1');

            // A fourth while the third's question is waiting: that question is cancelled, and the
            // answer given is the fourth's.
            await page.keyboard.press('Shift+ArrowDown');
            await expect(page.locator('#summary-asked')).toHaveText('Asked: 3; cancelled: 1; answered: 1');
            await expect(summary(page)).toHaveText('Calculating…');
            await answer(page);
            await expect.poll(() => figureTexts(held(page))).toContain(`Sum: ${await sumOf(page, 4)}`);
            expect(await figureTexts(held(page))).toContain('Count: 4');

            // At no render did figures stand beside a selection they were not taken over.
            const seen = await page.evaluate(() => window.__heldSummarySeen);
            expect(seen.some(({ text }) => /Count: \d+/.test(text)), 'a render with figures was observed').toBe(true);
            for (const { rows, text } of seen) {
                const count = /Count: (\d+)/.exec(text);
                if (count) {
                    expect(Number(count[1]), `figures for ${count[1]} cells beside ${rows} rows selected: "${text}"`).toBe(rows);
                }
            }
        });

        test(`SM-11 / ADR-0130 (SM-2): the answer to a question a new selection replaced, given after the grid cancelled it, is shown nowhere (${chrome}) ${chromeHost(chrome)}`, async ({ page }) => {
            // The page ignores the token and keeps the replaced question, as a careless Consumer may.
            await page.goto(`/features?chrome=${chrome}&summary=held&late=1`);
            await expect(held(page).locator('.ex-row').first()).toBeVisible();
            await alterPage(page, () => {
                const root = document.querySelector('#held-summary .ex-grid');
                const seen = [];
                const observer = new MutationObserver(() => seen.push(root.querySelector('.ex-summary')?.innerText ?? ''));
                observer.observe(root, { subtree: true, childList: true, characterData: true, attributes: true });
                window.__heldLateSeen = seen;
                return () => { observer.disconnect(); delete window.__heldLateSeen; };
            });

            // Two rows asked, then a third before the answer: the two rows' question is replaced.
            await notional(page, 0).click({ force: true });
            await page.keyboard.press('Shift+ArrowDown');
            await expect(page.locator('#summary-asked')).toHaveText('Asked: 1; cancelled: 0; answered: 0');
            await page.keyboard.press('Shift+ArrowDown');
            await expect(page.locator('#summary-asked')).toHaveText('Asked: 2; cancelled: 1; answered: 0');

            // Answered now, with the two rows' figures: the grid has already let that question go.
            await page.locator('#summary-answer-late').dispatchEvent('click');
            await expect(page.locator('#summary-late')).toHaveText('Answered late: 1');
            // The answer to the question standing comes after it and is shown; by then the late one
            // has had its turn, so what stood in between is read off every render.
            await answer(page);
            await expect.poll(() => figureTexts(held(page))).toContain(`Sum: ${await sumOf(page, 3)}`);
            expect(await figureTexts(held(page))).toContain('Count: 3');

            const seen = await page.evaluate(() => window.__heldLateSeen);
            expect(seen.some((text) => /Count: 3/.test(text)), 'the standing answer was observed').toBe(true);
            expect(seen.filter((text) => /Count: 2\b/.test(text)), 'the replaced question\'s figures, shown').toEqual([]);
        });

        test(`SM-11 / ADR-0130 (SM-4): a declined question shows its reason in place of the figures, and no figure with it (${chrome}) ${chromeHost(chrome)}`, async ({ page }) => {
            await notional(page, 0).click({ force: true });
            for (let i = 0; i < 4; i++) {
                await page.keyboard.press('Shift+ArrowDown');
            }
            await expect(summary(page)).toHaveClass(/\bex-summary-declined\b/);
            await expect(summary(page)).toHaveText(DECLINED);
            expect(await figureTexts(held(page))).toEqual([]);

            // Four cells again: the reason goes for the pending mark, and the answer replaces it.
            await page.keyboard.press('Shift+ArrowUp');
            await expect(summary(page)).toHaveText('Calculating…');
            await expect(summary(page)).not.toHaveClass(/\bex-summary-declined\b/);
            await answer(page);
            await expect.poll(() => figureTexts(held(page))).toContain(`Sum: ${await sumOf(page, 4)}`);
            expect(await summary(page).innerText()).not.toContain('Not summed');

            // And five once more: declined at once, the four cells' figures gone with it.
            await page.keyboard.press('Shift+ArrowDown');
            await expect(summary(page)).toHaveText(DECLINED);
            expect(await figureTexts(held(page))).toEqual([]);
        });
    });
}
