import { test, expect, circuitQuiet, dataRowCount, headerRowsOf, scrollRowToTop } from './fixtures.mjs';
import { sheet, openSheet, at } from './sheet-helpers.mjs';

// The rows as ARIA counts them (ADR-0033, decided with the user 2026-10-12, after the seventeenth
// Windows run's NVDA reading). The header's rows come first: the header row is row 1, and each tier
// of Header Groups is a row of its own before it. The data rows are counted after them, and
// aria-rowcount holds both. Near the top NVDA counted the rows painted before a row: it said "row 11"
// for D10, where aria-rowindex said 10. aria-rowindex now writes that number, near the top and far
// down alike.
//
// The accessibility tree is what verifies it (ADR-0033, "Verified against the accessibility tree,
// not a screen reader", 2026-10-13): no screen reader is run, and what one says of a row far down is
// learned from users' reports. What is read here is Chromium's accessibility tree, over
// the DevTools protocol: which rows it holds, in its order, and which column headers or cells each
// holds. A tier's row and the leaf row under Header Groups are `display: contents` in the markup, so
// that the band lays out exactly as one element did (ADR-0032); the tree must still hold them as rows
// that own their headers, or the group labels would read as cells of no row.

test.use({ viewport: { width: 1280, height: 1000 } });

/**
 * The rows of the `index`-th element `selector` finds, as Chromium's accessibility tree holds them,
 * in its order: each row's aria-rowindex, the names of the column headers it owns, and how many cells.
 * Ignored nodes are passed through, as the tree's readers pass them; a row the tree ignores is
 * listed as ignored, so the test can say so.
 */
async function accessibleRows(page, selector, index = 0) {
    // The tree is read whole, then a DOM node per row. On the Server host the circuit goes quiet
    // first, so the reads see all the host will say (ADR-0056).
    await circuitQuiet();
    const client = await page.context().newCDPSession(page);
    try {
        await client.send('DOM.enable');
        await client.send('Accessibility.enable');
        const { result } = await client.send('Runtime.evaluate', {
            expression: `document.querySelectorAll(${JSON.stringify(selector)})[${index}]`,
        });
        const { node: root } = await client.send('DOM.describeNode', { objectId: result.objectId });
        const { nodes } = await client.send('Accessibility.getFullAXTree');
        const byId = new Map(nodes.map((node) => [node.nodeId, node]));
        const top = nodes.find((node) => node.backendDOMNodeId === root.backendNodeId);
        const role = (node) => node.role?.value ?? '';
        const rows = [];
        const walk = (node, row) => {
            if (role(node) === 'row') {
                row = { backend: node.backendDOMNodeId, ignored: node.ignored === true, headers: [], cells: 0 };
                rows.push(row);
            } else if (!node.ignored && role(node) === 'columnheader') {
                row?.headers.push((node.name?.value ?? '').trim());
                return;
            } else if (!node.ignored && role(node) === 'gridcell') {
                if (row) {
                    row.cells++;
                }
                return;
            }
            for (const child of node.childIds ?? []) {
                const next = byId.get(child);
                if (next) {
                    walk(next, row);
                }
            }
        };
        expect(top, 'the grid is in the accessibility tree').toBeDefined();
        expect(role(top)).toBe('grid');
        walk(top, null);
        for (const row of rows) {
            const { node } = await client.send('DOM.describeNode', { backendNodeId: row.backend });
            const attributes = node.attributes ?? [];
            row.index = attributes[attributes.indexOf('aria-rowindex') + 1] ?? null;
            delete row.backend;
        }
        return rows;
    } finally {
        await client.detach();
    }
}

/** The painted row of `grid` that holds the cell at an A1 address (its id ends -r{row}c{column}). */
const rowHolding = (page, grid, address) => {
    const { row, column } = at(address);
    return grid.locator('.ex-viewport .ex-row', { has: page.locator(`[id$='-r${row}c${column}']`) });
};

/** The indices from `from`, `count` of them, as aria-rowindex writes them. */
const numbers = (from, count) => Array.from({ length: count }, (_, i) => String(from + i));

// /features' rows, as Features.razor makes them: 400 trades in the first grid, the first 20 of them
// in the second. The counts below are read against these, not against the grid's own count.
const FEATURES_ROWS = 400;
const SECOND_ROWS = 20;

test.describe('ADR-0033: /features', () => {
    test.beforeEach(async ({ page }) => {
        await page.goto('/features');
        await expect(page.locator('.ex-grid').first().locator('.ex-viewport .ex-row').first()).toBeVisible();
        await expect(page.locator('.ex-grid').nth(1).locator('.ex-viewport .ex-row').first()).toBeVisible();
    });

    test('A11Y-3 / ADR-0033: a Header Group band is row 1, the header row 2, and the data rows count from 3, near the top and far down @behaviour', async ({ page }) => {
        const grid = page.locator('.ex-grid').first();
        expect(await headerRowsOf(grid)).toBe(2);
        // The page's 400 trades, and the band's row and the header row; the fixture the other specs
        // read a row total through takes the two off.
        await expect(grid).toHaveAttribute('aria-rowcount', String(FEATURES_ROWS + 2));
        expect(await dataRowCount(grid)).toBe(FEATURES_ROWS);

        const top = await accessibleRows(page, '.ex-grid', 0);
        expect(top.filter((row) => row.ignored), 'no row is ignored by the tree').toEqual([]);
        // The band's row owns the groups' labels, the header row the leaf headers, in that order.
        expect(top[0].index).toBe('1');
        expect(top[0].headers).toEqual(['Who', 'What']);
        expect(top[0].cells).toBe(0);
        expect(top[1].index).toBe('2');
        expect(top[1].headers.some((name) => name.startsWith('Book'))).toBe(true);
        expect(top[1].headers).not.toContain('Who');
        expect(top[1].cells).toBe(0);
        const data = top.slice(2);
        expect(data.length).toBeGreaterThan(5);
        expect(data.map((row) => row.index)).toEqual(numbers(3, data.length));
        expect(data.every((row) => row.cells > 0 && row.headers.length === 0)).toBe(true);
        // The first data row is the one whose cells are row 0's.
        await expect(grid.locator('.ex-viewport .ex-row', { has: page.locator("[id$='-r0c0']") })).toHaveAttribute('aria-rowindex', '3');

        await scrollRowToTop(grid, 300);

        await expect(grid.locator('.ex-viewport .ex-row', { has: page.locator("[id$='-r300c0']") })).toHaveAttribute('aria-rowindex', '303');
        const far = await accessibleRows(page, '.ex-grid', 0);
        expect(far.filter((row) => row.ignored), 'no row is ignored by the tree, far down').toEqual([]);
        // Each header row still owns its column headers, as at the top: the band's row the groups'
        // labels, the header row the leaf headers, neither a cell.
        expect(far.slice(0, 2), 'the header rows far down are the header rows at the top').toEqual(top.slice(0, 2));
        const farData = far.slice(2);
        expect(farData.every((row) => row.cells > 0 && row.headers.length === 0)).toBe(true);
        expect(farData.map((row) => row.index)).toEqual(numbers(Number(farData[0].index), farData.length));
        expect(farData.map((row) => row.index)).toContain('303');
    });

    test('A11Y-2 / A11Y-3 / ADR-0033: an untiered header is row 1, the first data row 2, and the count is the data rows and the header row @behaviour', async ({ page }) => {
        const grid = page.locator('.ex-grid').nth(1);
        expect(await headerRowsOf(grid)).toBe(1);
        // The first 20 trades, and the header row.
        await expect(grid).toHaveAttribute('aria-rowcount', String(SECOND_ROWS + 1));
        expect(await dataRowCount(grid)).toBe(SECOND_ROWS);

        const rows = await accessibleRows(page, '.ex-grid', 1);
        expect(rows.filter((row) => row.ignored)).toEqual([]);
        expect(rows[0].index).toBe('1');
        expect(rows[0].headers.length).toBeGreaterThan(1);
        expect(rows[0].cells).toBe(0);
        const data = rows.slice(1);
        expect(data.map((row) => row.index)).toEqual(numbers(2, data.length));
        await expect(grid.locator('.ex-viewport .ex-row', { has: page.locator("[id$='-r0c0']") })).toHaveAttribute('aria-rowindex', '2');
    });
});

/** The Sheet's grid, as sheet() finds it: the grid that paints a Formula Bar. */
const SHEET = '.ex-grid:has(> .ex-formula-bar)';

test.describe('ADR-0033: /sheet', () => {
    test('A11Y-2 / A11Y-3 / ADR-0033: the Column Headings are row 1, so a Sheet row is its number plus one, near the top and far down @behaviour', async ({ page }) => {
        await openSheet(page);
        const grid = sheet(page);
        // The Sheet's 1,048,576 rows, and the Column Headings' row.
        await expect(grid).toHaveAttribute('aria-rowcount', '1048577');
        expect(await dataRowCount(grid)).toBe(1048576);

        const rows = await accessibleRows(page, SHEET, 0);
        expect(rows.filter((row) => row.ignored)).toEqual([]);
        expect(rows[0].index).toBe('1');
        expect(rows[0].headers.slice(0, 3)).toEqual(['A', 'B', 'C']);
        expect(rows[0].cells).toBe(0);
        expect(rows.slice(1).map((row) => row.index)).toEqual(numbers(2, rows.length - 1));
        // D10, which NVDA read as "row 11" in the seventeenth run while aria-rowindex said 10.
        await expect(rowHolding(page, grid, 'D10')).toHaveAttribute('aria-rowindex', '11');
        await expect(rowHolding(page, grid, 'A1')).toHaveAttribute('aria-rowindex', '2');

        await scrollRowToTop(grid, 499_999);

        await expect(rowHolding(page, grid, 'A500000')).toHaveAttribute('aria-rowindex', '500001');
        const far = await accessibleRows(page, SHEET, 0);
        expect(far.filter((row) => row.ignored), 'no row is ignored by the tree, far down').toEqual([]);
        // The Column Headings' row still owns its column headers, as at the top, and no cell.
        expect(far[0], 'the header row far down is the header row at the top').toEqual(rows[0]);
        const farData = far.slice(1);
        expect(farData.every((row) => row.cells > 0 && row.headers.length === 0)).toBe(true);
        expect(farData.map((row) => row.index)).toEqual(numbers(Number(farData[0].index), farData.length));
        expect(farData.map((row) => row.index)).toContain('500001');
    });
});
