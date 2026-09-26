import { test, expect } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';

// Changing a row from its inspector while the store changes underneath it, on
// /inspector-edits (docs/specs/row-inspectors, page B). The store is one per process, so
// every test starts by resetting it. 20 trades E-001 … E-020, version 1; columns Id 0,
// Inspect 1, Book 2, Notional 3, Status 4, Version 5. The page's "elsewhere" buttons act
// on E-001.
//
// What is pinned: an inspector never changes its values under the reader's eyes, says when
// the row changed or went, and an action decided on a version that is no longer current is
// refused by the store — including in the window the banner cannot close (ADR-0043's
// "fail loudly rather than land elsewhere").

function grid(page) {
    return page.locator('#edit-grid .ex-grid');
}

function cell(page, row, column) {
    return grid(page).locator(`[id$='-r${row}c${column}']`);
}

function inspector(page, id = 'E-001') {
    return page.locator(`.demo-floating-inspector[data-inspector-key="${id}"]`);
}

async function open(page) {
    await page.goto('/inspector-edits');
    await expect(page.locator('#demo-interactive')).toBeAttached();
    await page.locator('#reset-store').click();
    await expect(page.locator('#held-count')).toHaveText('0');
    // Loaded again, so this page starts from the reset store rather than racing the
    // reset's own announcement.
    await page.goto('/inspector-edits');
    await expect(page.locator('#demo-interactive')).toBeAttached();
    await expect(cell(page, 0, 0)).toHaveText('E-001');
    await expect(cell(page, 0, 5)).toHaveText('1');
}

async function inspect(page, row = 0) {
    await cell(page, row, 1).locator('.ex-action').click();
    const id = `E-${String(row + 1).padStart(3, '0')}`;
    await expect(inspector(page, id)).toBeVisible();
    return inspector(page, id);
}

test('RI-16: an approval from the inspector reaches the grid as a new instance, and the inspector shows it without a banner (ADR-0003)', async ({ page }) => {
    await open(page);
    const panel = await inspect(page);
    await panel.locator('.demo-inspector-approve').click();

    await expect(cell(page, 0, 4)).toHaveText('Approved');
    await expect(panel.locator('.demo-inspector-status')).toHaveText('Approved');
    await expect(panel.locator('.demo-inspector-version')).toHaveText('2');
    await expect(panel.locator('.demo-inspector-banner')).toHaveCount(0);
    await expect(panel.locator('.demo-inspector-refusal')).toHaveCount(0);
});

test('RI-17: a change elsewhere raises the banner with the new value, keeps the old one shown, and disables the actions until reloaded', async ({ page }) => {
    await open(page);
    const panel = await inspect(page);
    const before = await panel.locator('.demo-inspector-notional').textContent();

    await page.locator('#change-now').click();
    const banner = panel.locator('.demo-inspector-banner');
    await expect(banner).toContainText('Changed elsewhere');
    await expect(banner).toContainText(await cell(page, 0, 3).textContent());
    await expect(panel.locator('.demo-inspector-notional')).toHaveText(before);
    await expect(panel.locator('.demo-inspector-approve')).toBeDisabled();
    await expect(panel.locator('.demo-inspector-add-note')).toBeDisabled();

    await banner.locator('.demo-inspector-reload').click();
    await expect(banner).toHaveCount(0);
    await expect(panel.locator('.demo-inspector-notional')).not.toHaveText(before);
    await expect(panel.locator('.demo-inspector-version')).toHaveText('2');
    await expect(panel.locator('.demo-inspector-approve')).toBeEnabled();
});

test('RI-18: a row deleted elsewhere says so, and its inspector acts on nothing', async ({ page }) => {
    await open(page);
    const panel = await inspect(page);
    await page.locator('#delete-now').click();

    await expect(panel.locator('.demo-inspector-banner')).toContainText('Deleted elsewhere');
    await expect(panel.locator('.demo-inspector-approve')).toBeDisabled();
    await expect(cell(page, 0, 0)).toHaveText('E-002');
});

test('RI-19: an approval pressed before the news of a change arrives is refused by the store, and says why', async ({ page }) => {
    await open(page);
    const panel = await inspect(page);
    const before = await cell(page, 0, 3).textContent();

    // The store has changed; nobody has been told. The banner cannot know.
    await page.locator('#change-held').click();
    await expect(page.locator('#held-count')).toHaveText('1');
    await expect(panel.locator('.demo-inspector-banner')).toHaveCount(0);
    await expect(cell(page, 0, 3)).toHaveText(before);

    await panel.locator('.demo-inspector-approve').click();
    await expect(panel.locator('.demo-inspector-refusal')).toHaveText(
        'Refused: trade E-001 is at version 2, and this inspector shows version 1. Reload before trying again.');
    await expect(cell(page, 0, 4)).not.toHaveText('Approved');
    // The refusal brought the news the announcement had not: the banner stands now.
    await expect(panel.locator('.demo-inspector-banner')).toContainText('Changed elsewhere');

    await page.locator('#release-held').click();
    await expect(cell(page, 0, 3)).not.toHaveText(before);
    await expect(cell(page, 0, 5)).toHaveText('2');
});

test('RI-24: a deletion stays said, whatever news about the trade arrives after it', async ({ page }) => {
    await open(page);
    const panel = await inspect(page);
    await page.locator('#change-held').click();
    await page.locator('#delete-now').click();
    const banner = panel.locator('.demo-inspector-banner');
    await expect(banner).toContainText('Deleted elsewhere');

    // The held change to the deleted trade is announced after the deletion.
    await page.locator('#release-held').click();
    await expect(page.locator('#held-count')).toHaveText('0');
    await expect(banner).toContainText('Deleted elsewhere');
    await expect(banner.locator('.demo-inspector-reload')).toHaveCount(0);
    await expect(panel.locator('.demo-inspector-approve')).toBeDisabled();
});

test('RI-25: an inspector opened while news is held back shows what the grid showed, and learns of the change when it is announced', async ({ page }) => {
    await open(page);
    const before = await cell(page, 0, 3).textContent();
    await page.locator('#change-held').click();
    const panel = await inspect(page);
    await expect(panel.locator('.demo-inspector-notional')).toHaveText(before);
    await expect(panel.locator('.demo-inspector-banner')).toHaveCount(0);

    await page.locator('#release-held').click();
    await expect(panel.locator('.demo-inspector-banner')).toContainText('Changed elsewhere');
});

test('RI-20: a note records the version it was written against, and is marked once the trade has moved on', async ({ page }) => {
    await open(page);
    const panel = await inspect(page);
    await panel.locator('.demo-inspector-note-text').fill('Checked against the confirmation');
    await panel.locator('.demo-inspector-add-note').click();

    const note = panel.locator('.demo-inspector-note');
    await expect(note).toHaveCount(1);
    await expect(note).toContainText('Checked against the confirmation');
    await expect(note.locator('.demo-inspector-note-stale')).toHaveCount(0);
    await expect(panel.locator('.demo-inspector-banner')).toHaveCount(0);

    await page.locator('#change-now').click();
    await panel.locator('.demo-inspector-reload').click();
    await expect(note.locator('.demo-inspector-note-stale')).toHaveText('written against version 1');
});

test('RI-21: the inspector follows its trade by key when an approval moves it under the sort', async ({ page }) => {
    await open(page);
    await page.locator('#sort-status').click();
    // Z to A: the Pending trades first. E-001 is Booked, so the sort has landed once it
    // has left the top.
    await expect(cell(page, 0, 4)).toHaveText('Pending');
    const id = (await cell(page, 0, 0).textContent()).trim();
    await cell(page, 0, 1).locator('.ex-action').click();
    const panel = inspector(page, id);
    await expect(panel).toBeVisible();

    await panel.locator('.demo-inspector-approve').click();
    await expect(panel.locator('.demo-inspector-status')).toHaveText('Approved');
    await expect(cell(page, 0, 0)).not.toHaveText(id);
    await expect(panel).toHaveAttribute('data-inspector-key', id);
});

test('RI-22: on WebAssembly the page says there is nobody to share the store with', async ({ page }) => {
    test.skip(SERVER, 'the Server host shares the store; the next test covers it');
    await open(page);
    await expect(page.locator('#store-scope')).toContainText('only this tab');
});

test('RI-23: on the Server host, an approval in another tab raises the banner in this one (ADR-0018)', async ({ page, browser }) => {
    test.skip(!SERVER, 'a WebAssembly host has one user per process');
    await open(page);
    const mine = await inspect(page, 1);

    const other = await browser.newContext();
    try {
        const theirs = await other.newPage();
        await theirs.goto('/inspector-edits');
        await expect(theirs.locator('#demo-interactive')).toBeAttached();
        await theirs.locator("#edit-grid .ex-grid [id$='-r1c1'] .ex-action").click();
        await theirs.locator('.demo-floating-inspector[data-inspector-key="E-002"] .demo-inspector-approve').click();
        await expect(theirs.locator("#edit-grid .ex-grid [id$='-r1c4']")).toHaveText('Approved');
    } finally {
        await other.close();
    }

    await expect(mine.locator('.demo-inspector-banner')).toContainText('Changed elsewhere');
    await expect(cell(page, 1, 4)).toHaveText('Approved');
});
