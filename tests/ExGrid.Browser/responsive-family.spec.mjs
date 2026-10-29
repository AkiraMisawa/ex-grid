import { test, expect, circuitQuiet, settled } from './fixtures.mjs';
import { expectActiveDescendant } from './keyboard.mjs';
import { expectCovers } from './sheet-helpers.mjs';

test.use({ viewport: { width: 1280, height: 1100 } });

async function scrollColumnToUnpinnedEdge(scroller, offset) {
    await scroller.evaluate((element, offset) => {
        const left = element.getBoundingClientRect().left;
        const pinned = [...element.querySelector('.ex-row').querySelectorAll('.ex-pinned, .ex-row-heading')];
        const right = Math.max(left, ...pinned.map(cell => cell.getBoundingClientRect().right));
        element.scrollLeft = Math.max(0, offset - (right - left));
    }, offset);
}

// A cell may be wider than the box (notably a Pivot label). Compare the part the
// scrollport can paint, so a stale narrow outline cannot pass after restoration.
async function expectPreservedFocus(grid) {
    await expectActiveDescendant(grid, /-r0c0$/);
    await expect.poll(() => grid.evaluate(root => {
        const cell = root.querySelector('[id$="-r0c0"]');
        const focus = root.querySelector('.ex-focus');
        const scroller = root.querySelector('.ex-scroller');
        if (!cell || !focus) return 'the focused cell or outline is not painted';
        const viewport = scroller.getBoundingClientRect();
        const clip = element => {
            const box = element.getBoundingClientRect();
            return {
                left: Math.max(box.left, viewport.left),
                top: Math.max(box.top, viewport.top),
                right: Math.min(box.right, viewport.left + scroller.clientWidth),
                bottom: Math.min(box.bottom, viewport.top + scroller.clientHeight),
            };
        };
        const want = clip(cell);
        const actual = clip(focus);
        if (want.right <= want.left || want.bottom <= want.top) return 'the focused cell has no visible area';
        return Object.keys(want).every(edge => Math.abs(want[edge] - actual[edge]) <= 1.5)
            ? 'covers the visible cell' : JSON.stringify({ actual, want });
    })).toBe('covers the visible cell');
}

// Public consumer fixture: no breakpoint changes product parameters. Only the enclosing box
// changes. Each case keeps its document through all layouts, widths and supported pin choices.
for (const product of ['grid', 'sheet', 'pivot']) {
    for (const chrome of ['builtin', 'mud']) {
        test(`RT-1 family ${product} ${chrome}: parent sizing preserves precision, geometry and View State (ADR-0045)`, async ({ page }) => {
            await page.goto(`/responsive-family?product=${product}&chrome=${chrome}`);
            const grid = page.locator('#family-box .ex-grid');
            const scroller = grid.locator('.ex-scroller');
            const cell = column => grid.locator(`[id$="-r0c${column}"]`);
            const lastColumn = product === 'pivot' ? 2 : 3;
            const fraction = product === 'sheet' ? '0.000000123456789' : '0.000000000000000123';
            await expect(cell(0)).toBeVisible();
            await settled(grid);
            await cell(0).click({ force: true });
            await settled(grid);
            const windowWidth = await page.evaluate(() => window.innerWidth);
            const rowCount = await grid.getAttribute('aria-rowcount');
            const columnCount = await grid.getAttribute('aria-colcount');
            const initial = await grid.evaluate(root => {
                const row = root.querySelector('.ex-row');
                return { height: row.getBoundingClientRect().height, font: getComputedStyle(row).fontSize };
            });
            const widths = await grid.locator('.ex-row').first().locator('.ex-cell[id]').evaluateAll(cells =>
                Object.fromEntries(cells.map(c => [c.id.match(/c(\d+)$/)[1], c.getBoundingClientRect().width])));
            // Fifteen significant digits in every product, the most the Script Core reads exactly (ADR-0350 section 16).
            const amount = '1234567890123.45';
            await expect(cell(lastColumn - 1)).toHaveText(amount);
            // The final column may be outside the initial painted slice (the Sheet's
            // character-based widths vary with the platform font). Reveal its leading
            // edge from the preceding column before reading its unchanged geometry.
            const targetOffset = await grid.evaluate((root, column) => {
                const element = root.querySelector(`[id$="-r0c${column}"]`);
                const scroller = root.querySelector('.ex-scroller');
                return element.getBoundingClientRect().right - scroller.getBoundingClientRect().left + scroller.scrollLeft;
            }, lastColumn - 1);
            await scrollColumnToUnpinnedEdge(scroller, targetOffset);
            await expect(cell(lastColumn)).toHaveText(fraction);
            await circuitQuiet();
            await settled(grid);
            // A virtualised cell node can be reused between a locator resolving and
            // its evaluation. Find the requested column and read it in one operation.
            await expect.poll(async () => {
                widths[lastColumn] = await grid.evaluate((root, column) =>
                    root.querySelector(`[id$="-r0c${column}"]`)?.getBoundingClientRect().width ?? 0, lastColumn);
                return widths[lastColumn];
            }).toBeGreaterThan(0);
            const expectedState = pin => `Product changes: 0; requested pinning: ${product === 'pivot' ? 'report labels' : (pin ? '2' : '0')}`;
            for (const pin of (product === 'pivot' ? [true] : [true, false])) {
                if (product !== 'pivot') await page.locator('#family-pin').setChecked(pin);
                for (const layout of ['flex', 'grid', 'split']) {
                    await page.locator('#family-layout').selectOption(layout);
                    for (const [name, width] of [['portrait', 320], ['landscape', 640], ['panel', 240], ['wide', 800]]) {
                        // Establish Focus at the previous size. The resize must preserve its
                        // geometry by itself, before any later cell input can repair it.
                        await scroller.evaluate(element => { element.scrollLeft = 0; });
                        await expect(cell(0)).toBeVisible();
                        await circuitQuiet();
                        await settled(grid);
                        await cell(0).click({ position: { x: 8, y: 8 }, force: true });
                        await expectActiveDescendant(grid, /-r0c0$/);
                        await settled(grid);
                        await page.locator(`#family-${name}`).click();
                        const available = width - (layout === 'split' ? 0 : 40);
                        await expect.poll(async () => (await scroller.boundingBox()).width).toBeCloseTo(available, 0);
                        await circuitQuiet();
                        await settled(grid);
                        await expectPreservedFocus(grid);
                        await expect(grid).toHaveAttribute('aria-rowcount', rowCount);
                        await expect(grid).toHaveAttribute('aria-colcount', columnCount);
                        if (name === 'panel' || !pin) await expect(grid.locator('.ex-cell.ex-pinned')).toHaveCount(0);
                        if (name === 'wide' && pin) await expect(cell(0)).toHaveClass(/ex-pinned/);
                        // The product must fit its given box; overflowing cells belong inside the
                        // scroller. A toolbar/Field List adaptation is tested separately from RT-1.
                        const fits = await page.locator('#family-box').evaluate(box => {
                            const bounds = box.getBoundingClientRect();
                            const product = box.querySelector('.mud-ex-grid') ?? box.lastElementChild;
                            const frame = product.getBoundingClientRect();
                            return frame.right <= bounds.right + 0.5 && frame.left >= bounds.left;
                        });
                        expect(fits).toBe(true);
                        const style = await grid.evaluate(root => {
                            const row = root.querySelector('.ex-row');
                            return { height: row.getBoundingClientRect().height, font: getComputedStyle(row).fontSize };
                        });
                        expect(style).toEqual(initial);
                        await expect.poll(() => grid.evaluate((root, expected) => {
                            const cells = root.querySelector('.ex-row').querySelectorAll('.ex-cell[id]');
                            return [...cells].filter(cell => {
                                const column = Number(cell.id.match(/c(\d+)$/)[1]);
                                return column <= expected.lastColumn && cell.getBoundingClientRect().width !== expected.widths[column];
                            }).map(cell => cell.id);
                        }, { lastColumn, widths })).toEqual([]);
                        // Native scrolling reaches the unchanged column even in a narrow box.
                        // Keyboard reveal has separate Input Order regression coverage.
                        await scrollColumnToUnpinnedEdge(scroller, targetOffset);
                        await expect(cell(lastColumn)).toHaveText(fraction);
                        await circuitQuiet();
                        await settled(grid);
                        let targetPoint;
                        await expect.poll(async () => {
                            targetPoint = await grid.evaluate((root, expected) => {
                                const cell = root.querySelector(`[id$="-r0c${expected.column}"]`);
                                if (!cell) return null;
                                const scroller = root.querySelector('.ex-scroller');
                                const box = cell.getBoundingClientRect();
                                if (box.width !== expected.width) return null;
                                const viewport = scroller.getBoundingClientRect();
                                const pinnedRight = Math.max(viewport.left,
                                    ...[...root.querySelector('.ex-row').querySelectorAll('.ex-pinned, .ex-row-heading')]
                                        .map(cell => cell.getBoundingClientRect().right));
                                const left = Math.max(box.left, viewport.left, pinnedRight);
                                const right = Math.min(box.right, viewport.left + scroller.clientWidth);
                                return right > left ? { x: (left + right) / 2, y: box.top + box.height / 2 } : null;
                            }, { column: lastColumn, width: widths[lastColumn] });
                            return targetPoint !== null;
                        }, { message: 'the unchanged column is painted inside the scrollport' }).toBe(true);
                        await page.mouse.click(targetPoint.x, targetPoint.y);
                        await expectActiveDescendant(grid, new RegExp(`-r0c${lastColumn}$`));
                        await settled(grid);
                        // Selection after native scrolling also paints at the selected column.
                        // The resize-only assertion above ran before this scroll and click.
                        if (name === 'wide') {
                            const address = product === 'pivot' ? 'C1' : 'D1';
                            await expectCovers(grid.locator('.ex-focus'), grid, address, address);
                        }
                        await expect(page.locator('#family-state')).toHaveText(expectedState(pin));
                        expect(await page.evaluate(() => window.innerWidth)).toBe(windowWidth);
                    }
                }
            }
        });
    }
}
