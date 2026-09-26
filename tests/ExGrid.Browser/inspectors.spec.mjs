import { test, expect } from './fixtures.mjs';

// Row inspectors on /inspectors (docs/specs/row-inspectors, page A). An Action Column
// whose action opens that row's inspector — the grid reports the press and does nothing
// else (ADR-0020) — and a Mark Column whose marks the page opens as inspectors from its
// toolbar (ADR-0043). 60 trades; columns Mark 0, Id 1, Inspect 2, Book 3, Trader 4,
// Notional 5, Status 6. Trade ids run T-2100001, T-2100002, …
//
// What these pin above all is where the keyboard is once an inspector has opened: inside
// it, not back in the grid. The grid takes the keyboard back to its root after every
// action handler returns (ADR-0037), and an inspector that looks usable while the keys go
// to the grid is the quietly wrong outcome.

const ROWS = 60;

function id(row) {
    return `T-21${String(row + 1).padStart(5, '0')}`;
}

function grid(page) {
    return page.locator('#inspector-grid .ex-grid');
}

function cell(page, row, column) {
    return grid(page).locator(`[id$='-r${row}c${column}']`);
}

function inspectButton(page, row) {
    return cell(page, row, 2).locator('.ex-action');
}

function floating(page) {
    return page.locator('.demo-floating-inspector');
}

// The inspector painted on top: the page stacks them by z-index, never by DOM order.
async function front(page) {
    return floating(page).evaluateAll((all) => all
        .map((e) => ({ key: e.getAttribute('data-inspector-key'), z: Number(getComputedStyle(e).zIndex) }))
        .sort((a, b) => b.z - a.z)[0]?.key);
}

async function open(page, query = '') {
    await page.goto(`/inspectors${query}`);
    await expect(page.locator('#demo-interactive')).toBeAttached();
    await expect(cell(page, 0, 1)).toHaveText(id(0));
}

async function useFloating(page) {
    await page.locator('#mode-floating').click();
    await expect(page.locator('#inspector-mode')).toHaveText('Floating');
}

async function focusedInside(page, selector) {
    return page.evaluate((s) => document.activeElement?.closest(s) != null, selector);
}

// Where the keyboard is, for the failure message: the grid's root, or something else.
async function keyboardAt(page) {
    return page.evaluate(() => {
        const e = document.activeElement;
        if (!e || e === document.body) return 'the page body';
        if (e.classList.contains('ex-grid')) return 'the grid root';
        return `${e.tagName.toLowerCase()}.${[...e.classList].join('.')}`;
    });
}

async function expectKeyboardIn(page, selector, message) {
    await expect.poll(async () => (await focusedInside(page, selector)) || await keyboardAt(page),
        { message, timeout: 2_000 }).toBe(true);
    // And it stays there: the grid's reclaim, if it comes, comes after the handler.
    await page.waitForTimeout(300);
    const still = (await focusedInside(page, selector)) || await keyboardAt(page);
    expect(still, `${message} (still, 300 ms later)`).toBe(true);
}

async function spaceOnInspect(page, row) {
    await cell(page, row, 1).click({ force: true });
    await page.keyboard.press('ArrowRight');
    await expect(grid(page)).toHaveAttribute('aria-activedescendant', new RegExp(`r${row}c2$`));
    await page.keyboard.press(' ');
}

for (const handler of ['shown', 'await']) {
    const query = handler === 'await' ? '?handler=await' : '';
    const style = handler === 'await' ? 'awaiting the dialog\'s result' : 'returning once the dialog is shown';

    test(`RI-4: modal, by click, handler ${style}: the keyboard is in the inspector, and back in the grid when it closes (ADR-0020/0037)`, async ({ page }) => {
        await open(page, query);
        await inspectButton(page, 3).click();
        const dialog = page.locator('.mud-dialog');
        await expect(dialog.locator(`[data-inspector-key="${id(3)}"]`)).toBeVisible();
        await expectKeyboardIn(page, '.mud-dialog', 'the keyboard is inside the modal inspector');

        await dialog.locator('.demo-inspector-close').click();
        await expect(dialog).toHaveCount(0);
        await expect(grid(page)).toBeFocused();
    });

    test(`RI-5: modal, by Space, handler ${style}: the keyboard is in the inspector (ADR-0020/0037)`, async ({ page }) => {
        await open(page, query);
        await spaceOnInspect(page, 5);
        await expect(page.locator(`.mud-dialog [data-inspector-key="${id(5)}"]`)).toBeVisible();
        await expectKeyboardIn(page, '.mud-dialog', 'the keyboard is inside the modal inspector');
    });
}

test('RI-6: floating, by click: the keyboard is in the inspector just opened (ADR-0020/0037)', async ({ page }) => {
    await open(page);
    await useFloating(page);
    await inspectButton(page, 1).click();
    await expect(floating(page)).toHaveCount(1);
    await expectKeyboardIn(page, `.demo-floating-inspector[data-inspector-key="${id(1)}"]`, 'the keyboard is inside the first inspector');

    await inspectButton(page, 4).click();
    await expect(floating(page)).toHaveCount(2);
    await expectKeyboardIn(page, `.demo-floating-inspector[data-inspector-key="${id(4)}"]`, 'the keyboard is inside the second inspector');
});

test('RI-7: floating, by Space: the keyboard is in the inspector (ADR-0020/0037)', async ({ page }) => {
    await open(page);
    await useFloating(page);
    await spaceOnInspect(page, 2);
    await expect(floating(page)).toHaveCount(1);
    await expectKeyboardIn(page, '.demo-floating-inspector', 'the keyboard is inside the inspector');
});

test('RI-8: floating: a row whose inspector is open brings it to the front instead of opening another', async ({ page }) => {
    await open(page);
    await useFloating(page);
    await inspectButton(page, 1).click();
    await inspectButton(page, 2).click();
    await expect(floating(page)).toHaveCount(2);
    await expect.poll(() => front(page)).toBe(id(2));

    await inspectButton(page, 1).click();
    await expect(floating(page)).toHaveCount(2);
    await expect.poll(() => front(page)).toBe(id(1));
});

test('RI-9: floating: an inspector is dragged by its title bar, and closes on its own', async ({ page }) => {
    await open(page);
    await useFloating(page);
    await inspectButton(page, 0).click();
    await inspectButton(page, 1).click();
    const first = floating(page).and(page.locator(`[data-inspector-key="${id(0)}"]`));
    const before = await first.boundingBox();
    const bar = await first.locator('.demo-inspector-title').boundingBox();

    await page.mouse.move(bar.x + 20, bar.y + bar.height / 2);
    await page.mouse.down();
    await page.mouse.move(bar.x + 140, bar.y + bar.height / 2 + 90, { steps: 8 });
    await page.mouse.up();

    // Pressing it brought it to the front, and it followed the pointer.
    await expect.poll(() => front(page)).toBe(id(0));
    await expect.poll(async () => {
        const after = await first.boundingBox();
        return [Math.round(after.x - before.x), Math.round(after.y - before.y)];
    }).toEqual([120, 90]);

    await first.locator('.demo-inspector-close').click();
    await expect(floating(page)).toHaveCount(1);
    await expect(floating(page)).toHaveAttribute('data-inspector-key', id(1));
});

test('RI-10: the marked rows open as inspectors, one each, and the count names the ones outside the filter (ADR-0043)', async ({ page }) => {
    await open(page);
    await useFloating(page);
    for (const row of [0, 2, 4]) {
        await cell(page, row, 0).locator('.ex-mark').click();
    }
    const button = page.locator('#open-marked');
    await expect(button).toHaveText('Open inspectors (3)');

    await page.locator('#hide-first').click();
    await expect(cell(page, 0, 1)).toHaveText(id(1));
    await expect(button).toHaveText('Open inspectors (3, 1 outside the filter)');

    await button.click();
    await expect(floating(page)).toHaveCount(3);
    const keys = await floating(page).evaluateAll((all) => all.map((e) => e.getAttribute('data-inspector-key')));
    expect(keys.sort()).toEqual([id(0), id(2), id(4)]);
});

test('RI-11: the marks survive a sort, so the rows opened are the rows ticked (ADR-0043)', async ({ page }) => {
    await open(page);
    await useFloating(page);
    for (const row of [1, 3]) {
        await cell(page, row, 0).locator('.ex-mark').click();
    }
    await page.locator('#sort-notional').click();
    await expect(cell(page, 0, 1)).not.toHaveText(id(0));

    await page.locator('#open-marked').click();
    await expect(floating(page)).toHaveCount(2);
    const keys = await floating(page).evaluateAll((all) => all.map((e) => e.getAttribute('data-inspector-key')));
    expect(keys.sort()).toEqual([id(1), id(3)]);
});

test('RI-12: above the cap the marked rows are refused by count, and nothing opens', async ({ page }) => {
    await open(page);
    await useFloating(page);
    await grid(page).locator('.ex-header .ex-mark').click();
    const button = page.locator('#open-marked');
    await expect(button).toHaveText(`Open inspectors (${ROWS})`);

    await button.click();
    await expect(page.locator('#inspector-status')).toHaveText(
        `${ROWS} rows are marked; inspectors open for at most 10 at a time. Nothing was opened.`);
    await expect(floating(page)).toHaveCount(0);
});

test('RI-13: modal: the marked rows open one after another, and Escape ends the queue', async ({ page }) => {
    await open(page);
    for (const row of [0, 1]) {
        await cell(page, row, 0).locator('.ex-mark').click();
    }
    await page.locator('#open-marked').click();
    const dialog = page.locator('.mud-dialog');
    await expect(dialog.locator(`[data-inspector-key="${id(0)}"]`)).toBeVisible();
    await expect(dialog).toHaveCount(1);
    await dialog.locator('.demo-inspector-close').click();
    await expect(dialog.locator(`[data-inspector-key="${id(1)}"]`)).toBeVisible();
    await dialog.locator('.demo-inspector-close').click();
    await expect(dialog).toHaveCount(0);

    // Three marked, Escape on the first: the queue ends there.
    await cell(page, 2, 0).locator('.ex-mark').click();
    await page.locator('#open-marked').click();
    await expect(dialog.locator(`[data-inspector-key="${id(0)}"]`)).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(dialog).toHaveCount(0);
    await page.waitForTimeout(300);
    await expect(dialog).toHaveCount(0);
});

test('RI-14: Enter on the Inspect cell moves down and opens nothing (ADR-0020)', async ({ page }) => {
    await open(page);
    await cell(page, 0, 1).click({ force: true });
    await page.keyboard.press('ArrowRight');
    await expect(grid(page)).toHaveAttribute('aria-activedescendant', /r0c2$/);
    await page.keyboard.press('Enter');
    await expect(grid(page)).toHaveAttribute('aria-activedescendant', /r1c2$/);
    await page.waitForTimeout(300);
    await expect(page.locator('.mud-dialog')).toHaveCount(0);
});

test('RI-15: while a modal inspector stands, the grid behind it cannot be pressed', async ({ page }) => {
    await open(page);
    await inspectButton(page, 0).click();
    const dialog = page.locator('.mud-dialog');
    await expect(dialog.locator(`[data-inspector-key="${id(0)}"]`)).toBeVisible();

    // The press lands on the dialog's backdrop, which MudBlazor answers by closing the
    // dialog — never on the row's button underneath.
    const box = await inspectButton(page, 6).boundingBox();
    await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
    await page.waitForTimeout(500);
    await expect(page.locator(`[data-inspector-key="${id(6)}"]`)).toHaveCount(0);
});
