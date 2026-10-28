import { test, expect, circuitQuiet, settled, setRoundTrip } from './fixtures.mjs';

// Blazor's ExGrid over the Script Core (ADR-0350 section 7), on /script-grid, on both Blazor hosts: the one
// path no other layer sees whole. The grid decides in the browser and the rows stay in .NET, so an edit has
// to cross to the Consumer's handler and come back as the row its source then holds, and a sort has to reach
// the C# source and come back as its order. The bridge's own logic is layer 2's (src/npm/blazor/test), and a
// value's text on the wire is the shared cases' (tests/cases/blazor).

test.beforeEach(async ({ page }) => {
    await page.goto('/script-grid');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
});

function grid(page) {
    return page.locator('.ex-grid').first();
}

const cell = (page, row, column) => grid(page).locator(`[id$='r${row}c${column}']`);

test('ADR-0350 section 7: an edit reaches the Consumer in .NET, and the cell paints the row its source then holds', async ({ page }) => {
    await expect(cell(page, 0, 0)).toHaveText('Credit-001');
    await cell(page, 0, 0).click({ force: true });
    await page.keyboard.type('Zeta');
    await page.keyboard.press('Enter');

    await expect(page.locator('#script-edit')).toHaveText('Edit: Book of Credit-001 set to Zeta');
    await settled(grid(page));
    await circuitQuiet();
    await expect(cell(page, 0, 0)).toHaveText('Zeta');
    await expect(page.locator('#script-selection')).toHaveText('Selection: 1 range(s), Focus at row 1, column 0');
});

test('ADR-0350 section 7, ADR-0001: a header click sorts through the source in .NET, and the rows come back in its order', async ({ page }) => {
    const notional = grid(page).locator('.ex-header-cell').nth(2);
    await notional.click({ position: { x: 30, y: 14 }, force: true });

    await expect(notional).toHaveAttribute('aria-sort', 'ascending');
    await settled(grid(page));
    await circuitQuiet();
    const shown = await Promise.all([0, 1, 2].map(row => cell(page, row, 2).textContent()));
    expect(shown).toEqual(['-496956', '-494971', '-494956']);
});

test('DC-61, ADR-0070: the grid hands the keyboard on to a control of the page\'s, and takes it back from there, across the bridge', async ({ page }) => {
    await cell(page, 0, 1).dblclick({ force: true });
    await expect(page.locator('#script-note')).toBeFocused();

    await page.keyboard.type('checked');
    await page.keyboard.press('Enter');
    await expect.poll(() => grid(page).evaluate(root => root.contains(document.activeElement))).toBe(true);
    await page.keyboard.press('ArrowDown');
    await expect(page.locator('#script-selection')).toHaveText('Selection: 1 range(s), Focus at row 1, column 1');
});

// ADR-0380 section 5, ED-33, ED-35: Notional is judged by the trade's domain in .NET, so each commit's verdict and
// its Edit Intent cross the circuit, 150 ms each way round on the Server host, while the editor stands and the input
// behind it waits. Under 1,000 is refused, above 1,000,000 is booked flagged.
test.describe('ADR-0380: a commit .NET answers, with 150 ms injected on the Server host', () => {
    const editor = (page) => grid(page).locator('input.ex-editor');
    const answered = async (page) => {
        await expect(grid(page)).not.toHaveAttribute('data-ex-pending-commit');
        await settled(grid(page));
        await circuitQuiet();
    };
    const focusRow = async (page) => Number((await page.locator('#script-selection').textContent()).match(/Focus at row (\d+)/)[1]);

    const notionals = (page, row) => Promise.all([cell(page, row, 2).textContent(), cell(page, row + 1, 2).textContent()]);

    // What a run of keys from `row` left: the two Notionals it reached, each as written or 'unchanged' from `before`,
    // the editor, and the Focus, from `row`.
    async function outcome(page, row, before) {
        const open = await editor(page).count() > 0;
        const after = await notionals(page, row);
        return {
            cells: after.map((text, i) => (text === before[i] ? 'unchanged' : text)),
            editor: open ? await editor(page).inputValue() : null,
            focus: await focusRow(page) - row,
        };
    }

    test.beforeEach(async () => {
        await setRoundTrip(150);
    });

    for (const [first, second] of [['150', '200'], ['1500', '2000']]) {
        test(`ED-33: ${first} Enter ${second} Enter typed at once ends as the same keys typed after each answer`, async ({ page }) => {
            const before = [await notionals(page, 0), await notionals(page, 10)];
            await cell(page, 0, 2).click({ force: true });
            await page.keyboard.type(first);
            await page.keyboard.press('Enter');
            await page.keyboard.type(second);
            await page.keyboard.press('Enter');
            await answered(page);
            const atOnce = await outcome(page, 0, before[0]);

            await cell(page, 10, 2).click({ force: true });
            await page.keyboard.type(first);
            await page.keyboard.press('Enter');
            await answered(page);
            await page.keyboard.type(second);
            await page.keyboard.press('Enter');
            await answered(page);

            expect(await outcome(page, 10, before[1])).toEqual(atOnce);
            // Refused, the editor held the first text and the second was typed after it; taken, each went to its row.
            expect(atOnce).toEqual(first === '150'
                ? { cells: ['150200', 'unchanged'], editor: null, focus: 1 }
                : { cells: ['1500', '2000'], editor: null, focus: 2 });
        });
    }

    test('ED-35 / ED-15: a Reject .NET answers holds the editor under Enter, Tab and a press, and Escape alone leaves', async ({ page }) => {
        const before = await cell(page, 0, 2).textContent();
        await cell(page, 0, 2).click({ force: true });
        await page.keyboard.type('150');
        await page.keyboard.press('Enter');
        await page.keyboard.press('Tab');
        await cell(page, 3, 0).click({ force: true });
        await answered(page);

        await expect(editor(page)).toHaveValue('150');
        await expect(editor(page)).toHaveAttribute('aria-invalid', 'true');
        await expect(editor(page)).toBeFocused();
        await expect(page.locator('#script-edit')).toHaveText('Edit: none');
        expect(await focusRow(page)).toBe(0);

        await page.keyboard.press('Escape');
        await answered(page);
        await expect(editor(page)).toHaveCount(0);
        await expect(cell(page, 0, 2)).toHaveText(before);
    });

    test('ED-35: a Flag .NET answers is booked painted as an error, and its reason opens when the Focus stands on it', async ({ page }) => {
        await cell(page, 0, 2).click({ force: true });
        await page.keyboard.type('2000000');
        await page.keyboard.press('Enter');
        await answered(page);
        await expect(cell(page, 0, 2)).toHaveText('2000000');
        await expect(cell(page, 0, 2)).toHaveClass(/ex-state-error/);

        await page.keyboard.press('ArrowUp');
        await expect(grid(page).locator('.ex-message')).toHaveText('A notional of 2000000 is above the desk\'s limit of 1,000,000: a second signature is needed.');
    });

    test('ED-38 / ADR-0381: F8 asked of .NET brings the Focus to a flagged trade outside the Window, and Shift+F8 goes back', async ({ page }) => {
        await cell(page, 0, 2).click({ force: true });
        await page.keyboard.type('2000000');
        await page.keyboard.press('Enter');
        await page.keyboard.type('3000000');
        await page.keyboard.press('Enter');
        await answered(page);
        await page.keyboard.press('Control+End');
        await settled(grid(page));
        await circuitQuiet();
        expect(await focusRow(page)).toBe(999);

        await page.keyboard.press('F8');
        await settled(grid(page));
        await circuitQuiet();
        await expect(page.locator('#script-selection')).toHaveText('Selection: 1 range(s), Focus at row 0, column 2');
        await expect(cell(page, 0, 2)).toBeInViewport();
        await page.keyboard.press('F8');
        await settled(grid(page));
        await circuitQuiet();
        expect(await focusRow(page)).toBe(1);
        await page.keyboard.press('Shift+F8');
        await settled(grid(page));
        await circuitQuiet();
        expect(await focusRow(page)).toBe(0);
    });

    test('ED-35 / ED-20: a Ctrl+Enter fill is judged once, and a Reject holds the editor and pastes nothing', async ({ page }) => {
        await cell(page, 0, 2).click({ force: true });
        await cell(page, 2, 2).click({ force: true, modifiers: ['Shift'] });
        await page.keyboard.type('150');
        await page.keyboard.press('Control+Enter');
        await answered(page);
        await expect(editor(page)).toHaveValue('150');
        await expect(page.locator('#script-paste')).toHaveText('Paste: none');

        await page.keyboard.press('Control+A');
        await page.keyboard.type('5000');
        await page.keyboard.press('Control+Enter');
        await answered(page);
        await expect(page.locator('#script-paste')).toHaveText('Paste: 3 notionals booked');
        await expect(cell(page, 2, 2)).toHaveText('5000');
    });

    // The verdict's text and the fill's three values are each larger than the 32 KB a Server hub takes in one message,
    // which closed the circuit with nothing on the page: each is held and taken as a stream, in its turn (ADR-0005).
    test('CP-21, ADR-0005: a commit and a fill past a Server hub\'s message limit reach the Consumer in .NET', async ({ page }) => {
        await cell(page, 0, 2).click({ force: true });
        await cell(page, 2, 2).click({ force: true, modifiers: ['Shift'] });
        await page.keyboard.type('5');
        await editor(page).fill(`5000${' '.repeat(40_000)}`);
        await page.keyboard.press('Control+Enter');
        await answered(page);
        await expect(page.locator('#script-paste')).toHaveText('Paste: 3 notionals booked');
        await expect(cell(page, 2, 2)).toHaveText('5000');
    });
});
