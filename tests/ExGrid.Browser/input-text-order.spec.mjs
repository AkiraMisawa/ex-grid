import { test, expect, setRoundTrip, settled, alterPage } from './fixtures.mjs';
import { expectTabStopTaken } from './keyboard.mjs';
import { openSheet, sheet, pressCell, editor, bar, nameBox, cell } from './sheet-helpers.mjs';

test('ADR-0240: a row press commits all text typed immediately before it', async ({ page }) => {
    await page.goto('/features');
    const grid = page.locator('.ex-grid').first();
    await expectTabStopTaken(grid);
    await setRoundTrip(150);
    await grid.locator("[id$='r0c1']").click({ force: true });
    await page.keyboard.press('F2');
    await settled(grid);
    const target = await grid.locator("[id$='r3c1']").boundingBox();
    expect(target).not.toBeNull();

    await page.keyboard.press('ControlOrMeta+A');
    await page.keyboard.type('all the text 123456789');
    await page.mouse.click(target.x + target.width / 2, target.y + target.height / 2);

    await settled(grid);
    expect(await page.locator('#edit-status').textContent()).toContain('Trader=all the text 123456789');
    expect(await grid.locator("[id$='r0c1']").textContent()).toBe('all the text 123456789');
});


test('ADR-0240: typing alone is settled only when the editor render has heard its text', async ({ page }) => {
    await page.goto('/features');
    const grid = page.locator('.ex-grid').first();
    await expectTabStopTaken(grid);
    await setRoundTrip(150);
    await grid.locator("[id$='r0c1']").click({ force: true });
    await page.keyboard.press('F2');
    await settled(grid);

    await page.keyboard.press('ControlOrMeta+A');
    await page.keyboard.type('typed without a command 123456789');
    await settled(grid);
    const reading = await grid.locator('.ex-editor:not(.ex-formula-bar-text)').evaluate((surface) => ({
        typed: surface.value ?? surface.querySelector('input, textarea').value,
        heard: surface.getAttribute('data-ex-mode-text'),
    }));
    expect(reading.typed).toBe('typed without a command 123456789');
    expect(reading.heard).toBe(reading.typed);
});

for (const chrome of ['builtin', 'mud']) {
    test(`ADR-0240: a point survives its press answer and release, and fresh typing replaces it (${chrome})`, async ({ page }) => {
        await openSheet(page, chrome);
        const grid = sheet(page);
        await setRoundTrip(150);
        await pressCell(grid, 'F3');
        await page.keyboard.press('F2');
        await settled(grid);
        const targets = [];
        for (const address of ['B2', 'C2']) {
            const box = await cell(grid, address).boundingBox();
            expect(box).not.toBeNull();
            targets.push({ address, x: box.x + box.width / 2, y: box.y + box.height / 2 });
        }

        for (const target of targets) {
            await page.keyboard.press('ControlOrMeta+A');
            await page.keyboard.type('=1+');
            await page.mouse.click(target.x, target.y);
            await settled(grid);
            expect(await editor(grid).inputValue()).toBe(`=1+${target.address}`);
        }
    });

    for (const surface of ['cell', 'bar', 'name']) {
        test(`ADR-0240: ${surface} typing is covered by the Settled Mark (${chrome})`, async ({ page }) => {
            await openSheet(page, chrome);
            const grid = sheet(page);
            await setRoundTrip(150);
            await pressCell(grid, 'D10');
            if (surface === 'cell') await page.keyboard.press('F2');
            else await (surface === 'bar' ? bar(grid) : nameBox(grid)).click();
            await settled(grid);
            const field = surface === 'cell' ? editor(grid) : surface === 'bar' ? bar(grid) : nameBox(grid);
            const typed = surface === 'name' ? 'D12345' : 'whole field text 123456789';

            await page.keyboard.press('ControlOrMeta+A');
            await page.keyboard.type(typed);
            await settled(grid);
            const reading = await field.evaluate((input) => ({
                typed: input.value,
                heard: input.closest('[data-ex-mode-text]').getAttribute('data-ex-mode-text'),
            }));
            expect(reading).toEqual({ typed, heard: typed });
        });
    }
}

for (const chrome of ['builtin', 'mud']) {
for (const target of ['heading', 'menu', 'macOS Control-click menu']) {
    test(`ADR-0240: typing followed immediately by a ${target} command keeps the full text (${chrome})`, async ({ page, context }) => {
        const menu = target !== 'heading';
        const controlClick = target === 'macOS Control-click menu';
        test.skip(controlClick && process.platform !== 'darwin', 'ADR-0021: Control-primary opens a context menu on macOS only');
        if (menu) await context.grantPermissions(['clipboard-read', 'clipboard-write']);
        await page.goto(`/features?chrome=${chrome}`);
        const grid = page.locator('.ex-grid').first();
        await expectTabStopTaken(grid);
        await setRoundTrip(150);
        await grid.locator("[id$='r0c1']").click({ force: true });
        await page.keyboard.press('F2');
        await settled(grid);
        if (menu) {
            // A Consumer's status can grow when the press commits. Force that movement between
            // mousedown and contextmenu: the secondary click must keep the cell it started on.
            await alterPage(page, () => {
                const root = document.querySelector('.ex-grid');
                const status = document.querySelector('.demo-status');
                const before = status.getAttribute('style');
                const move = (event) => {
                    if (event.button === 2 || (event.button === 0 && event.ctrlKey)) status.style.paddingBottom = '48px';
                };
                root.addEventListener('mousedown', move);
                return () => {
                    root.removeEventListener('mousedown', move);
                    if (before === null) status.removeAttribute('style');
                    else status.setAttribute('style', before);
                };
            });
        }
        const button = target === 'heading' ? grid.locator('.ex-header-cell').nth(1) : grid.locator("[id$='r3c1']");
        const copiedText = menu ? await button.textContent() : null;
        const box = await button.boundingBox();
        expect(box).not.toBeNull();

        await page.keyboard.press('ControlOrMeta+A');
        await page.keyboard.type('command sees all 123456789');
        if (controlClick) await page.keyboard.down('Control');
        await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2,
            { button: menu && !controlClick ? 'right' : 'left' });
        if (controlClick) await page.keyboard.up('Control');
        if (menu) await page.keyboard.press('Enter');
        await settled(grid);
        expect(await page.locator('#edit-status').textContent()).toContain('Trader=command sees all 123456789');
        expect(await grid.locator('[role=menu]').count()).toBe(0);
        if (menu) {
            // Control-primary adds the pressed cell to the existing Selection (ADR-0012).
            // The menu opens inside that Selection and keeps both ranges (ADR-0036).
            const copied = controlClick ? `command sees all 123456789\r\n${copiedText}\r\n` : `${copiedText}\r\n`;
            expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(copied);
        }
    });
}

}

for (const chrome of ['builtin', 'mud']) {
    test(`ADR-0240: Name Box typing, Enter and the following key keep their order (${chrome})`, async ({ page }) => {
        await openSheet(page, chrome);
        const grid = sheet(page);
        await setRoundTrip(150);
        await nameBox(grid).click();
        await settled(grid);

        await page.keyboard.press('ControlOrMeta+A');
        await page.keyboard.type('D123');
        await page.keyboard.press('Enter');
        await page.keyboard.press('ArrowDown');
        await settled(grid);
        expect(await nameBox(grid).inputValue()).toBe('D124');
    });
}

for (const chrome of ['builtin', 'mud']) {
    test(`ADR-0240: a Formula Bar commit answers both its typing and its command (${chrome})`, async ({ page }) => {
        await openSheet(page, chrome);
        const grid = sheet(page);
        await setRoundTrip(150);
        await pressCell(grid, 'D10');
        await bar(grid).click();
        await settled(grid);

        await page.keyboard.press('ControlOrMeta+A');
        await page.keyboard.type('whole bar text 123456789');
        await page.keyboard.press('Enter');
        await settled(grid);
        expect(await cell(grid, 'D10').textContent()).toBe('whole bar text 123456789');
        expect(await nameBox(grid).inputValue()).toBe('D11');
    });
}

for (const chrome of ['builtin', 'mud']) {
test(`ADR-0240: a right-click without an edit holds the following menu command (${chrome})`, async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
    await page.goto(`/features?chrome=${chrome}`);
    const grid = page.locator('.ex-grid').first();
    await expectTabStopTaken(grid);
    await setRoundTrip(150);
    const target = grid.locator("[id$='r3c1']");
    const copiedText = await target.textContent();
    const box = await target.boundingBox();
    await settled(grid);

    await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2, { button: 'right' });
    await page.keyboard.press('Enter');
    await settled(grid);
    expect(await grid.locator('[role=menu]').count()).toBe(0);
    expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(`${copiedText}\r\n`);
});
}
