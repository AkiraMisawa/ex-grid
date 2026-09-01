import { test, expect } from '@playwright/test';

// The presentation contract, measured (ADR-0027/0028/0029/0031): tokens win where the
// contract says they win, the painted geometry equals the declared geometry, nothing
// under the Viewport animates, forced colors keep every state tellable, and the grid
// stays an LTR island inside an RTL page. Console errors fail the run, as everywhere.

let consoleErrors;
let pageErrors;

test.beforeEach(({ page }) => {
    consoleErrors = [];
    pageErrors = [];
    page.on('console', (m) => {
        if (m.type() === 'error') {
            consoleErrors.push(m.text());
        }
    });
    page.on('pageerror', (e) => pageErrors.push(String(e)));
});

test.afterEach(() => {
    expect(consoleErrors, 'zero console errors (CON-1)').toEqual([]);
    expect(pageErrors, 'zero page errors (CON-2)').toEqual([]);
});

function grid(page) {
    return page.locator('.ex-grid').first();
}

async function open(page) {
    await page.goto('/features');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
}

test('geometry tokens are inline and read-only: an ancestor or stylesheet cannot move them (UX-2)', async ({ page }) => {
    await open(page);
    const before = await grid(page).locator('.ex-row').first()
        .evaluate((el) => getComputedStyle(el).height);
    expect(before).toBe('24px');

    await page.evaluate(() => {
        document.body.style.setProperty('--ex-row-height', '60px');
        // A stylesheet rule on the element the token lives on, as UX-2's
        // verification says. (A rule re-declaring the token on a DESCENDANT — or an
        // !important — can still displace it by CSS's own cascade; recorded as the
        // known limitation in ADR-0029's corrections, not silently passed over.)
        const sheet = document.createElement('style');
        sheet.textContent = '.ex-grid { --ex-row-height: 60px; }';
        document.head.append(sheet);
    });

    const after = await grid(page).locator('.ex-row').first()
        .evaluate((el) => getComputedStyle(el).height);
    expect(after, 'the inline declaration wins; painted row height is unchanged').toBe('24px');
});

test('the painted heights equal the declared metrics exactly (UX-3, ST-3)', async ({ page }) => {
    await open(page);

    // RowHeight=24, HeaderHeight=28, two tiers of Header Groups: the band is 56.
    const measured = await page.evaluate(() => {
        const g = document.querySelector('.ex-grid');
        return {
            row: getComputedStyle(g.querySelector('.ex-row')).height,
            header: getComputedStyle(g.querySelector('.ex-header')).height,
            cellPadding: getComputedStyle(g.querySelector('.ex-cell')).paddingLeft,
        };
    });
    expect(measured.row).toBe('24px');
    expect(measured.header).toBe('56px');
    expect(measured.cellPadding).toBe('8px');
});

test('a Visual Token set on an ancestor recolours the paint (UX-5, the paint half)', async ({ page }) => {
    await open(page);
    const before = await grid(page).locator('.ex-header')
        .evaluate((el) => getComputedStyle(el).backgroundColor);

    await page.evaluate(() => {
        document.body.style.setProperty('--ex-header-background', 'rgb(200, 12, 12)');
    });

    const after = await grid(page).locator('.ex-header')
        .evaluate((el) => getComputedStyle(el).backgroundColor);
    expect(after).toBe('rgb(200, 12, 12)');
    expect(after).not.toBe(before);
});

test('nothing inside the Viewport transitions or animates (UX-6)', async ({ page }) => {
    await open(page);

    const offenders = await page.evaluate(() =>
        [...document.querySelectorAll('.ex-viewport, .ex-viewport *')]
            .map((el) => ({
                cls: el.className,
                transition: getComputedStyle(el).transitionProperty,
                duration: getComputedStyle(el).transitionDuration,
                animation: getComputedStyle(el).animationName,
            }))
            .filter((s) => s.animation !== 'none'
                || (s.transition !== 'none' && !/^0s(, 0s)*$/.test(s.duration))));
    expect(offenders).toEqual([]);
});

test('forced colors keep every Cell State and Row Kind tellable (UX-7, on /cells)', async ({ page }) => {
    await page.emulateMedia({ forcedColors: 'active' });
    await page.goto('/cells');
    await expect(page.locator('.ex-grid').first().locator('.ex-row').first()).toBeVisible();

    const states = await page.evaluate(() => {
        const normal = document.querySelector('.ex-cell:not([class*=ex-state])');
        const describe = (el) => {
            if (!el) return null;
            const s = getComputedStyle(el);
            return [s.color, s.fontStyle, s.outlineStyle, s.textDecorationLine].join('|');
        };
        return {
            normal: describe(normal),
            stale: describe(document.querySelector('.ex-state-stale')),
            missing: describe(document.querySelector('.ex-state-missing')),
            error: describe(document.querySelector('.ex-state-error')),
            modified: describe(document.querySelector('.ex-state-modified')),
        };
    });
    // Every present state is distinguishable from Normal, and from each other.
    const present = Object.entries(states).filter(([, v]) => v !== null);
    const values = present.map(([, v]) => v);
    expect(new Set(values).size, JSON.stringify(states)).toBe(values.length);
});

test('under a dark scheme the untouched grid stays readable (UX-8)', async ({ page }) => {
    await page.emulateMedia({ colorScheme: 'dark' });
    await open(page);

    const contrast = await page.evaluate(() => {
        const cell = document.querySelector('.ex-cell');
        const s = getComputedStyle(cell);
        const parse = (c) => c.match(/\d+(\.\d+)?/g).slice(0, 3).map(Number);
        const luminance = ([r, g, b]) => {
            const f = (v) => {
                v /= 255;
                return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4;
            };
            return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
        };
        const text = luminance(parse(s.color));
        const ground = luminance(parse(getComputedStyle(document.querySelector('.ex-grid')).backgroundColor));
        const ratio = (Math.max(text, ground) + 0.05) / (Math.min(text, ground) + 0.05);
        return { ratio, color: s.color };
    });
    expect(contrast.ratio, JSON.stringify(contrast)).toBeGreaterThanOrEqual(4.5);
});

test('inside an RTL ancestor the grid stays an LTR island (DIR-2/DIR-3)', async ({ page }) => {
    await open(page);
    await page.evaluate(() => {
        document.querySelector('.ex-grid').parentElement.setAttribute('dir', 'rtl');
    });

    // The overlay still lands on its cell to within a pixel.
    await grid(page).locator("[id$='r1c1']").click({ force: true });
    const alignment = await page.evaluate(() => {
        const g = document.querySelector('.ex-grid');
        const cell = g.querySelector("[id$='r1c1']");
        const focus = [...g.querySelectorAll('.ex-focus')]
            .map((el) => el.getBoundingClientRect())
            .find((r) => r.width > 0);
        const c = cell.getBoundingClientRect();
        return { dx: Math.abs(focus.left - c.left), dy: Math.abs(focus.top - c.top) };
    });
    expect(alignment.dx).toBeLessThanOrEqual(1);
    expect(alignment.dy).toBeLessThanOrEqual(1);

    // Columns still flow left to right: the first header starts left of the second.
    const headers = await page.evaluate(() =>
        [...document.querySelectorAll('.ex-grid .ex-header-cell')].slice(0, 2)
            .map((el) => el.getBoundingClientRect().left));
    expect(headers[0]).toBeLessThan(headers[1]);

    // And the Arabic value renders inside its cell (DIR-3): present, not displaced.
    const arabic = grid(page).locator('.ex-cell', { hasText: 'عمر' }).first();
    await expect(arabic).toBeVisible();
});

test('a Header Group label is centred, and nothing aligns it vertically (HG-13, ADR-0032)', async ({ page }) => {
    await open(page);
    const group = grid(page).locator('.ex-header-group').first();
    await expect(group).toBeVisible();

    const painted = await group.evaluate((el) => {
        const style = getComputedStyle(el);
        return { textAlign: style.textAlign, verticalAlign: style.verticalAlign };
    });

    expect(painted.textAlign).toBe('center');
    // The other half of HG-13 is the absence of a knob, which the layer-1 surface test
    // holds; what a browser can say is that nothing is being aligned vertically here —
    // the row height is fixed (ADR-0013), so the option would have nothing to do.
    expect(painted.verticalAlign).toBe('baseline');
});

test("the editor's box is exactly the cell's (ED-9)", async ({ page }) => {
    await open(page);
    await grid(page).locator("[id$='r0c1']").click({ force: true });
    await page.keyboard.press('F2');

    const boxes = await page.evaluate(() => {
        const g = document.querySelector('.ex-grid');
        const cell = g.querySelector("[id$='r0c1']").getBoundingClientRect();
        const editor = g.querySelector('.ex-editor').getBoundingClientRect();
        return { cell, editor };
    });
    expect(Math.abs(boxes.editor.left - boxes.cell.left)).toBeLessThanOrEqual(1);
    expect(Math.abs(boxes.editor.top - boxes.cell.top)).toBeLessThanOrEqual(1);
    expect(Math.abs(boxes.editor.width - boxes.cell.width)).toBeLessThanOrEqual(1);
    expect(Math.abs(boxes.editor.height - boxes.cell.height)).toBeLessThanOrEqual(1);
    await page.keyboard.press('Escape');
});

test('the pointer leaving the grid stops the auto-scroll (SL-14/SL-15)', async ({ page }) => {
    await open(page);
    const g = grid(page);
    const box = await g.locator('.ex-scroller').boundingBox();

    // Drag from a top cell into the bottom band and hold.
    await page.mouse.move(box.x + 60, box.y + 60);
    await page.mouse.down();
    await page.mouse.move(box.x + 60, box.y + box.height - 6, { steps: 4 });
    await page.waitForTimeout(400);
    const whileHeld = await g.locator('.ex-scroller').evaluate((el) => el.scrollTop);
    expect(whileHeld).toBeGreaterThan(0);

    // Leave the grid with the button still down: the scroll stops where it was.
    await page.mouse.move(box.x + 60, box.y + box.height + 200, { steps: 4 });
    const atLeave = await g.locator('.ex-scroller').evaluate((el) => el.scrollTop);
    await page.waitForTimeout(800);
    const afterWait = await g.locator('.ex-scroller').evaluate((el) => el.scrollTop);
    expect(afterWait, 'no runaway scroll after the pointer left (SL-14)').toBe(atLeave);

    // Return with the button released: the drag is over and stays over.
    await page.mouse.up();
    await page.mouse.move(box.x + 60, box.y + box.height - 6);
    await page.waitForTimeout(400);
    expect(await g.locator('.ex-scroller').evaluate((el) => el.scrollTop)).toBe(afterWait);
});

test('the Blazor error UI never appears (CON-5)', async ({ page }) => {
    await open(page);
    const display = await page.locator('#blazor-error-ui')
        .evaluate((el) => getComputedStyle(el).display);
    expect(display).toBe('none');
});
