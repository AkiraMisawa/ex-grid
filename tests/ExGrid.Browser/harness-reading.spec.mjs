import { test, expect, alterPage, circuitQuiet, setRoundTrip } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';
import { blend, offDevicePixels, onDeviceGrid, paints, pixelsApart, stillPictures, wholePixelCentres } from './pixels.mjs';

// The harness's tools for reading what the page shows (ADR-0056; README.md, "Waiting on the
// Server host" and "Reading pixels"): circuitQuiet, which waits until the browser and the
// Server host have stopped talking; stillPictures, which takes pictures to compare only while
// nothing but the test changed the page; and the one allowance for rounding a painted colour.
// Each is pinned here both ways: it waits, retakes or refuses when it should, and its answer
// would have been wrong without it.

const sheetGrid = (page) => page.locator('.ex-grid').first();
const nameBox = (page) => sheetGrid(page).locator('input.ex-name-box');

async function openSheet(page) {
    await page.goto('/sheet');
    await expect(sheetGrid(page).locator("[id$='-r0c0']")).toHaveText('Item');
    // Cells are pointer-events: none — the Viewport is the delegated target (ADR-0004).
    await sheetGrid(page).locator("[id$='-r0c5']").click({ force: true });
    await expect(nameBox(page)).toHaveValue('F1');
}

test('circuitQuiet returns once the Server host has answered, so what it decided is there to read at once', async ({ page }) => {
    await openSheet(page);
    await setRoundTrip(300);

    await page.keyboard.press('ArrowDown');
    const early = await nameBox(page).inputValue();
    const started = Date.now();
    await circuitQuiet();
    const waited = Date.now() - started;

    if (SERVER) {
        // Read at once, the move had not come back: it is the host's, a round trip away.
        expect(early, 'the Name Box straight after the key').toBe('F1');
        expect(waited, 'circuitQuiet waited out the round trip').toBeGreaterThanOrEqual(300);
        // One reading, no retry: the answer is in the DOM.
        expect(await nameBox(page).inputValue(), 'the Name Box once the circuit is quiet').toBe('F2');
    } else {
        expect(waited, 'no wire to wait for on WebAssembly').toBeLessThan(50);
        await expect(nameBox(page)).toHaveValue('F2');
    }
});

test('circuitQuiet refuses a window the keep-alive pings could keep from ever being met', async () => {
    test.skip(!SERVER, 'there is no circuit on WebAssembly');
    await expect(circuitQuiet({ quietFor: 5_000 })).rejects.toThrow(/keep-alive pings/);
});

test('circuitQuiet says what kept the wire busy rather than waiting for ever', async ({ page }) => {
    test.skip(!SERVER, 'there is no circuit on WebAssembly');
    await openSheet(page);
    // Something on the page that never stops asking the host: here, a stylesheet every 50 ms.
    await alterPage(page, () => {
        const timer = setInterval(() => fetch('/_content/ExGrid/ex-grid.min.css', { cache: 'no-store' }).catch(() => { }), 50);
        return () => clearInterval(timer);
    });
    await expect(circuitQuiet({ timeout: 1_500 }))
        .rejects.toThrow(/did not go quiet: not quiet for \d+ ms within 1500 ms: [1-9]\d* chunks from the browser/);
});

// The index, which has no grid, with a box and a field of the test's own on it: nothing on the
// page changes but what a test changes. A mark draws either one of two ways, black or white,
// through a stylesheet laid over the page.
async function openStill(page) {
    await page.goto('/');
    await alterPage(page, () => {
        const style = document.createElement('style');
        style.textContent = `
            #harness-box { width: 120px; height: 40px; }
            [data-harness-way="dark"] { background: #000 !important; color: #000 !important; }
            [data-harness-way="light"] { background: #fff !important; color: #fff !important; }`;
        const box = document.createElement('div');
        box.id = 'harness-box';
        box.textContent = 'Harness';
        const field = document.createElement('input');
        field.id = 'harness-field';
        document.head.append(style);
        document.body.prepend(box, field);
        return () => {
            box.remove();
            field.remove();
            style.remove();
        };
    });
    return { box: page.locator('#harness-box'), field: page.locator('#harness-field') };
}

const retakes = () => test.info().annotations.filter((note) => note.type === 'stillPictures: taken again');

test('stillPictures takes every picture again when the page changed between them', async ({ page }) => {
    const { box } = await openStill(page);
    let shots = 0;

    const pictures = await stillPictures(box, {
        mark: 'data-harness-way',
        ways: ['dark', 'light'],
        shoot: async (subject) => {
            const picture = await subject.screenshot();
            shots++;
            // After the first picture, a change the test did not make — as a render arriving
            // late would be.
            if (shots === 1) {
                await subject.evaluate((element) => element.setAttribute('data-harness-late', ''));
            }
            return picture;
        },
    });

    expect(shots, 'both ways pictured twice').toBe(4);
    expect(retakes().map((note) => note.description)).toEqual(['attempt 1: data-harness-late on div#harness-box, none → "" now']);
    expect(pixelsApart(pictures.dark, pictures.light, 96).apart, 'the two ways are two pictures').toBeGreaterThan(0);
    await expect(box, 'the mark is taken off').not.toHaveAttribute('data-harness-way');
});

test('stillPictures fails, naming the change, when the page never holds still', async ({ page }) => {
    const { box } = await openStill(page);
    let shots = 0;

    await expect(stillPictures(box, {
        mark: 'data-harness-way',
        ways: ['dark', 'light'],
        attempts: 2,
        shoot: async (subject) => {
            const picture = await subject.screenshot();
            await subject.evaluate((element, n) => element.setAttribute('data-harness-late', String(n)), ++shots);
            return picture;
        },
    })).rejects.toThrow(/did not hold still while it was pictured dark and light, in 2 attempts:\nattempt 1: data-harness-late on div#harness-box.*\nattempt 2: data-harness-late on div#harness-box/);
});

test('stillPictures does not count Playwright\'s own preparation of the fields for a picture', async ({ page }) => {
    const { field } = await openStill(page);
    expect(await field.getAttribute('style'), 'the field has no inline style').toBeNull();

    await stillPictures(field, { mark: 'data-harness-way', ways: ['dark', 'light'] });

    // Playwright made the caret transparent on every field for each picture and put back what was
    // there, which leaves an empty style: it wrote, and the writes were not taken for changes.
    expect(await field.getAttribute('style'), 'Playwright\'s write, put back').toBe('');
    expect(retakes(), 'no picture taken again').toEqual([]);
});

test('a colour between two bytes is painted as either, and a whole one only as itself', () => {
    // Ticket 92: MudBlazor's dark row rule, white at 30/255 on #373740, is 78.53 in red and
    // green and 86.47 in blue; Chrome on Linux painted 79 on a pinned cell and 78 beside it.
    const rule = blend([255, 255, 255], 30 / 255, [0x37, 0x37, 0x40]);
    expect(rule.map((v) => +v.toFixed(2))).toEqual([78.53, 78.53, 86.47]);
    expect(paints([78, 78, 86], rule)).toBe(true);
    expect(paints([79, 79, 86], rule)).toBe(true);
    expect(paints([79, 78, 87], rule)).toBe(true);
    expect(paints([77, 78, 86], rule)).toBe(false);
    expect(paints([78, 80, 86], rule)).toBe(false);
    expect(paints([78, 78, 85], rule)).toBe(false);
    // Opaque, and opaque over anything: one byte each, and no other.
    expect(paints([255, 0, 0], [255, 0, 0])).toBe(true);
    expect(paints([254, 0, 0], [255, 0, 0])).toBe(false);
    expect(paints([0, 0, 1], blend([0, 0, 0], 1, [255, 255, 255]))).toBe(false);
});

test('where to read: the edges off the device pixels, the pixels a box covers whole, a region on the grid', () => {
    expect(offDevicePixels({ x: 10, y: 10.5, width: 20, height: 20 }, 2)).toEqual([]);
    expect(offDevicePixels({ x: 10, y: 10.5, width: 20, height: 20 }, 1)).toEqual([
        'top at 10.5 CSS px, 10.5 device px', 'bottom at 30.5 CSS px, 30.5 device px']);
    expect(offDevicePixels({ x: 10, y: 10, width: 20.25, height: 20 }, 1.5)).toEqual([
        'right at 30.25 CSS px, 45.375 device px']);
    // At 150%, a box from 10 to 11 CSS px reaches from device px 15 to 16.5: pixel 15 is whole.
    expect(wholePixelCentres({ x: 10, y: 10, width: 1, height: 1 }, 1.5)).toEqual({ xs: [15.5 / 1.5], ys: [15.5 / 1.5] });
    expect(wholePixelCentres({ x: 0.5, y: 0, width: 2, height: 1 }, 1)).toEqual({ xs: [1.5], ys: [0.5] });
    const inDevicePixels = (box, scale) => Object.fromEntries(Object.entries(box).map(([k, v]) => [k, +(v * scale).toFixed(6)]));
    expect(inDevicePixels(onDeviceGrid({ x: 10.2, y: 5, width: 3, height: 3 }, 2), 2)).toEqual({ x: 20, y: 10, width: 7, height: 6 });
    expect(inDevicePixels(onDeviceGrid({ x: 10, y: 5, width: 3, height: 3 }, 1.5, 1), 1.5)).toEqual({ x: 13, y: 6, width: 8, height: 8 });
});
