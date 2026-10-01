import { test, expect } from './fixtures.mjs';
import { painted, contrast, resolvedColour } from './pixels.mjs';

// The Wrapper contract, measured with a real Wrapper (ADR-0030): ExGrid.MudBlazor on
// /mud. The Definition of Done wrote UX-3/6/9 against a "stub Wrapper stylesheet";
// this is the real one. Plus the hover band (UX-13, ADR-0021's fifth entry) and the
// Wrapper's editor inside the core's box (ADR-0010/0030). Console errors fail the run.

const paperA = (page) => page.locator('.demo-paper-a');
const paperB = (page) => page.locator('.demo-paper-b');
const gridA = (page) => paperA(page).locator('.ex-grid');
const gridB = (page) => paperB(page).locator('.ex-grid');

async function open(page) {
    await page.goto('/mud');
    await expect(gridA(page).locator('.ex-row').first()).toBeVisible();
    await expect(gridB(page).locator('.ex-row').first()).toBeVisible();
    // The Wrapper's stylesheet has landed when one of its tokens reaches the root —
    // one the page's own stylesheet does not restate (the header tokens it does).
    await expect.poll(async () => gridA(page).evaluate((g) => getComputedStyle(g).getPropertyValue('--ex-editor-outline').trim()))
        .not.toBe('');
}

test('the painted geometry equals the declared metrics under the real Wrapper stylesheet (UX-3)', async ({ page }) => {
    await open(page);
    // Dense → Compact: 28px rows, 28px header, 8px padding — nothing in the Wrapper's
    // stylesheet may move them (ADR-0027/0030).
    const measured = await gridA(page).evaluate((g) => ({
        row: getComputedStyle(g.querySelector('.ex-row')).height,
        header: getComputedStyle(g.querySelector('.ex-header')).height,
        cellPadding: getComputedStyle(g.querySelector('.ex-cell')).paddingLeft,
        font: getComputedStyle(g.querySelector('.ex-cell')).fontFamily,
    }));
    expect(measured.row).toBe('28px');
    expect(measured.header).toBe('28px');
    expect(measured.cellPadding).toBe('8px');
    expect(measured.font.toLowerCase()).toContain('roboto');

    // Not dense → Standard: 32px, from the same cascade, and the grid re-anchors.
    await page.locator('#toggle-dense').click();
    await expect.poll(async () => gridA(page).evaluate((g) => getComputedStyle(g.querySelector('.ex-row')).height))
        .toBe('32px');
});

test('nothing under the Viewport transitions or animates with the Wrapper loaded (UX-6)', async ({ page }) => {
    await open(page);
    const offenders = await gridA(page).evaluate((g) => {
        const out = [];
        for (const el of g.querySelectorAll('.ex-viewport, .ex-viewport *')) {
            const s = getComputedStyle(el);
            if (s.transitionProperty !== 'none' && s.transitionDuration !== '0s') {
                out.push(`${el.className}: transition ${s.transitionProperty}`);
            }
            if (s.animationName !== 'none') {
                out.push(`${el.className}: animation ${s.animationName}`);
            }
        }
        return out;
    });
    expect(offenders).toEqual([]);
});

// A colour's OKLCH hue, in degrees, from sRGB bytes — to say that the dark scheme's raise kept
// the primary's hue (ADR-0030, 2026-09-29).
function hue([r, g, b]) {
    const linear = (v) => {
        const c = v / 255;
        return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
    };
    const [lr, lg, lb] = [linear(r), linear(g), linear(b)];
    const l = Math.cbrt(0.4122214708 * lr + 0.5363325363 * lg + 0.0514459929 * lb);
    const m = Math.cbrt(0.2119034982 * lr + 0.6806995451 * lg + 0.1073969566 * lb);
    const s = Math.cbrt(0.0883024619 * lr + 0.2817188376 * lg + 0.6299787005 * lb);
    const a = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s;
    const bb = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s;
    return ((Math.atan2(bb, a) * 180) / Math.PI + 360) % 360;
}

test('the focus outline and the selection fill stay visible under the Wrapper theme, light and dark (UX-9)', async ({ page }) => {
    await open(page);
    // The colours the cascade resolved, in whatever syntax it kept them — the dark scheme's is
    // an oklch() — as sRGB bytes.
    const colours = async () => {
        const css = await gridA(page).evaluate((g) => {
            const focus = [...g.querySelectorAll('.ex-focus')].find((el) => el.getBoundingClientRect().width > 0);
            const range = g.querySelector('.ex-range');
            // Each outline is the border of its box of its own, the ::after (ADR-0008, 2026-10-01).
            return {
                focus: getComputedStyle(focus, '::after').borderTopColor,
                focusStyle: getComputedStyle(focus, '::after').borderTopStyle,
                range: range ? getComputedStyle(range, '::after').borderTopColor : null,
                fill: range ? getComputedStyle(range, '::before').backgroundColor : null,
                ground: getComputedStyle(g).backgroundColor,
                primary: getComputedStyle(g).getPropertyValue('--mud-palette-primary'),
            };
        });
        return {
            ...css,
            focus: await resolvedColour(page, css.focus),
            range: css.range === null ? null : await resolvedColour(page, css.range),
            ground: await resolvedColour(page, css.ground),
            primary: await resolvedColour(page, css.primary),
        };
    };
    const cellAt = (row, column) => gridA(page).locator('.ex-row').nth(row).locator('.ex-cell').nth(column);
    for (const dark of [false, true]) {
        if (dark) {
            await page.locator('#toggle-dark').click();
            await expect(page.locator('#dark-status')).toHaveText('Dark: True');
        }
        // One cell: its Focus outline alone, no tint (ADR-0008, 2026-09-29). Forced, because
        // cells are pointer-events: none by design and the Viewport is the target (README,
        // "Traps this suite has already hit").
        await cellAt(2, 2).click({ force: true });
        // Painted a round trip after the click on the Server host.
        await expect(gridA(page).locator('.ex-focus')).not.toHaveCount(0);
        await expect(gridA(page).locator('.ex-range')).toHaveCount(0);
        const oneCell = await colours();
        expect(oneCell.focusStyle).toBe('solid');
        expect(contrast(oneCell.focus, oneCell.ground), `focus outline against the cell ground, dark=${dark} (${JSON.stringify(oneCell)})`)
            .toBeGreaterThanOrEqual(3);
        if (dark) {
            // Raised from the primary, which misses 3:1 on this surface, and the hue kept.
            expect(contrast(oneCell.primary, oneCell.ground), 'the premise: the dark primary alone misses 3:1').toBeLessThan(3);
            expect(Math.abs(hue(oneCell.focus) - hue(oneCell.primary)), JSON.stringify(oneCell)).toBeLessThanOrEqual(2);
        } else {
            // The palette's primary itself (ADR-0030, 2026-09-29).
            expect(oneCell.focus, JSON.stringify(oneCell)).toEqual(oneCell.primary);
        }
        // And as painted, where this page puts it: on the Focus band's tint over the surface,
        // which is lighter than the surface in the dark scheme (ADR-0030 sets the floor to clear
        // that too). The pointer is parked, so no hover band adds to it.
        await page.mouse.move(0, 0);
        await expect(gridA(page).locator('.ex-hover-row')).toHaveCount(0);
        await expect(gridA(page).locator('.ex-focus-row')).not.toHaveCount(0);
        // The outline's top lies above the cell, as Excel's does (ADR-0008, 2026-10-01), so it is
        // read a pixel above the cell's top; the ground beside it is the band's tint in the cell.
        const box = await cellAt(2, 2).boundingBox();
        const cellPixels = await painted(page, { x: box.x, y: box.y - 4, width: box.width, height: box.height + 4 });
        const onBand = {
            outline: cellPixels.at(box.x + box.width / 2, box.y - 1),
            ground: cellPixels.at(box.x + 6, box.y + 6),
        };
        expect(contrast(onBand.outline, onBand.ground), `painted over the Focus band, dark=${dark} (${JSON.stringify(onBand)})`)
            .toBeGreaterThanOrEqual(3);

        // A range: its fill and its one outline, in the Focus outline's colour — the range's
        // own token, --ex-selection-outline, defaults to it and the Wrapper leaves it so.
        await cellAt(3, 3).click({ force: true, modifiers: ['Shift'] });
        await expect(gridA(page).locator('.ex-range')).not.toHaveCount(0);
        const withRange = await colours();
        expect(withRange.range, JSON.stringify(withRange)).toEqual(oneCell.focus);
        expect(withRange.fill).not.toBe('rgba(0, 0, 0, 0)');
    }
});

test('a loss is painted in the palette\'s error colour by the Consumer\'s tone rule, and only there (ADR-0006, FN-7a)', async ({ page }) => {
    await open(page);
    // Every fifth trade is negative in the fixture; the rule marks it, the Wrapper's
    // token colours it, and a gain the rule says nothing about stays the ink colour.
    const loss = gridA(page).locator('.ex-row').nth(4).locator('.ex-cell').nth(2);
    const gain = gridA(page).locator('.ex-row').nth(3).locator('.ex-cell').nth(2);
    await expect(loss).toHaveClass(/ex-tone-negative/);
    await expect(gain).not.toHaveClass(/ex-tone-/);
    const colours = await gridA(page).evaluate((g, [lossIdx, gainIdx]) => {
        const cell = (r) => g.querySelectorAll('.ex-row')[r].querySelectorAll('.ex-cell')[2];
        const probe = document.createElement('span');
        probe.style.color = 'var(--mud-palette-error)';
        g.appendChild(probe);
        const error = getComputedStyle(probe).color;
        probe.remove();
        return { loss: getComputedStyle(cell(lossIdx)).color, gain: getComputedStyle(cell(gainIdx)).color, ink: getComputedStyle(g).color, error };
    }, [4, 3]);
    expect(colours.loss).toBe(colours.error);
    expect(colours.gain).toBe(colours.ink);
    expect(colours.loss).not.toBe(colours.ink);
});

test('a theme switch re-renders nothing inside the grid: the rows are the same elements (RR-1)', async ({ page }) => {
    await open(page);
    const before = await gridA(page).evaluate((g) => {
        const rows = [...g.querySelectorAll('.ex-row')];
        rows.forEach((r, i) => { r.dataset.probe = String(i); });
        return { ground: getComputedStyle(g).backgroundColor, count: rows.length };
    });
    await page.locator('#toggle-dark').click();
    await expect(page.locator('#dark-status')).toHaveText('Dark: True');
    const after = await gridA(page).evaluate((g) => {
        const rows = [...g.querySelectorAll('.ex-row')];
        return { ground: getComputedStyle(g).backgroundColor, probes: rows.map((r) => r.dataset.probe) };
    });
    expect(after.ground, 'the ground recoloured').not.toBe(before.ground);
    // An element re-created by a render loses the probe; the same element keeps it.
    expect(after.probes).toEqual([...Array(before.count).keys()].map(String));
});

test('the row under the pointer is highlighted by an overlay band, in the hovered instance only (UX-13)', async ({ page }) => {
    await open(page);
    const rows = gridA(page).locator('.ex-row');
    const second = await rows.nth(1).boundingBox();
    const fourth = await rows.nth(3).boundingBox();

    await page.mouse.move(second.x + 60, second.y + second.height / 2);
    const band = gridA(page).locator('.ex-hover-row');
    await expect(band.first()).toBeVisible();
    // Visible is not yet there: a band a real OS cursor put on another row is visible too,
    // until the move's report arrives (UX-16's race, the fifth Windows run).
    await expect.poll(async () => Math.round((await band.first().boundingBox())?.y ?? -1)).toBe(Math.round(second.y));
    const bandBox = await band.first().boundingBox();
    expect(Math.round(bandBox.y)).toBe(Math.round(second.y));
    expect(Math.round(bandBox.height)).toBe(Math.round(second.height));
    // No row carries a class for it (ADR-0029): the band is the overlay's.
    expect(await rows.nth(1).evaluate((r) => r.className)).toBe('ex-row');
    // The other instance did not react (ADR-0018).
    await expect(gridB(page).locator('.ex-hover-row')).toHaveCount(0);

    await page.mouse.move(fourth.x + 60, fourth.y + fourth.height / 2);
    await expect.poll(async () => Math.round((await band.first().boundingBox()).y)).toBe(Math.round(fourth.y));

    // Leave the rows: the band goes.
    await page.mouse.move(5, 5);
    await expect(band).toHaveCount(0);
});

test("the Wrapper's editor opens inside the core's box with the typed character, and commits (ED-4, ADR-0030)", async ({ page }) => {
    await open(page);
    // Trader is column 1, editable.
    const cell = gridA(page).locator('.ex-row').nth(1).locator('.ex-cell').nth(1);
    await cell.click({ force: true });
    await page.keyboard.type('Q');
    const editor = gridA(page).locator('.ex-editor');
    await expect(editor).toBeVisible();
    const input = editor.locator('input.mud-ex-editor');
    await expect(input).toHaveValue('Q');
    await expect(input).toBeFocused();
    // The control lives inside the box the core handed it: the cell's own (ADR-0028).
    const box = await editor.boundingBox();
    const cellBox = await cell.boundingBox();
    expect(Math.round(box.height)).toBe(Math.round(cellBox.height));
    expect(Math.round(box.width)).toBe(Math.round(cellBox.width));
    // The text sits where the cell's did: the box pads once, the control not again.
    const padding = await editor.evaluate((e) => ({
        box: getComputedStyle(e).paddingLeft,
        input: getComputedStyle(e.querySelector('input')).paddingLeft,
    }));
    expect(padding.box).toBe('8px');
    expect(padding.input).toBe('0px');
    expect(await editor.evaluate((e) => e.querySelector('.mud-input, .mud-textfield') === null)).toBe(true);

    await page.keyboard.type('uinn');
    await page.keyboard.press('Enter');
    await expect(page.locator('#edit-status')).toContainText('Trader=Quinn');
    await expect(gridA(page).locator('.ex-row').nth(1).locator('.ex-cell').nth(1)).toHaveText('Quinn');
});

test('the loading bar is a MudProgressLinear where the core places its seam, only while loading', async ({ page }) => {
    await open(page);
    await expect(gridB(page).locator('.mud-ex-grid-loading')).toHaveCount(0);
    await page.locator('#toggle-loading').click();
    const bar = gridB(page).locator('.mud-ex-grid-loading');
    await expect(bar).toBeVisible();
    expect(await bar.evaluate((b) => b.classList.contains('mud-progress-indeterminate'))).toBe(true);
    await expect(gridB(page)).toHaveClass(/ex-loading/);
    await page.locator('#toggle-loading').click();
    await expect(bar).toHaveCount(0);
});

test("the paper's corners: rounded with an inset, square flush; the grid's own box is untouched", async ({ page }) => {
    await open(page);
    const before = await paperA(page).evaluate((p) => ({
        radius: getComputedStyle(p).borderTopLeftRadius,
        padding: getComputedStyle(p).paddingLeft,
        overflow: getComputedStyle(p).overflow,
    }));
    expect(before.radius).not.toBe('0px');
    expect(before.padding).toBe(before.radius);
    // Never clipped: a popover reaching past the paper must survive (UX-11).
    expect(before.overflow).toBe('visible');

    await page.locator('#toggle-square').click();
    await expect.poll(async () => paperA(page).evaluate((p) => getComputedStyle(p).borderTopLeftRadius)).toBe('0px');
    expect(await paperA(page).evaluate((p) => getComputedStyle(p).paddingLeft)).toBe('0px');
});
