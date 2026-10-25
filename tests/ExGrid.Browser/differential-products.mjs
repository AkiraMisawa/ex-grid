import { expect, alterPage, circuitQuiet, settled } from './fixtures.mjs';
import { expectTabStopTaken } from './keyboard.mjs';
import { openSheet, sheet, bar, nameBox } from './sheet-helpers.mjs';
import { pick } from './differential.mjs';

// The products the differential test drives (ADR-0240 §8, differential.spec.mjs): for each, how its
// page is opened, the places its presses aim at, how a step is sent, when it has settled, what of it is
// compared, and the steps a seed draws. ExPivot is not among them yet: its first runs found races of its
// own, and it joins once they are fixed (ticket 07, ASY-8).

// ---- Grids: ExGrid's editable grid on /features, and the Sheet on /sheet -----------------------------

/**
 * The steps for a grid of `rows` × `columns` in view: a press on `first`, so the run starts from a known
 * Focus, then a mix of presses, typed text and keys.
 */
function gridSteps(next, { rows: ROWS, columns: COLUMNS, first, texts, count = 28 }) {
    const steps = [{ kind: 'press', target: first }];
    // Where the Focus and the extent may be, as bounds: a key that may move them widens the bounds
    // it may move them across, and a press without Shift makes them exact again.
    const start = /^r(\d+)c(\d+)$/.exec(first);
    let rows = [Number(start[1]), Number(start[1])];
    let columns = [Number(start[2]), Number(start[2])];
    let previous = null;
    while (steps.length < count) {
        const roll = next();
        if (roll < 0.2) {
            const row = Math.floor(next() * ROWS);
            const column = Math.floor(next() * COLUMNS);
            const shift = next() < 0.25;
            steps.push({ kind: 'press', target: `r${row}c${column}`, shift });
            if (!shift) {
                rows = [row, row];
                columns = [column, column];
            }
            previous = null;
            continue;
        }
        if (roll < 0.4) {
            steps.push({ kind: 'type', text: pick(next, texts) });
            previous = null;
            continue;
        }
        const keys = ['Escape', 'F2', 'Delete', 'Backspace', 'Home', 'ControlOrMeta+z'];
        if (rows[1] < ROWS - 1) keys.push('ArrowDown', 'Shift+ArrowDown', 'Enter');
        if (rows[0] > 0) keys.push('ArrowUp', 'Shift+ArrowUp', 'Shift+Enter');
        if (columns[1] < COLUMNS - 1 && previous !== 'Escape') keys.push('ArrowRight', 'Shift+ArrowRight', 'Tab');
        else if (columns[1] < COLUMNS - 1) keys.push('ArrowRight', 'Shift+ArrowRight');
        if (columns[0] > 0 && previous !== 'Escape') keys.push('ArrowLeft', 'Shift+ArrowLeft', 'Shift+Tab');
        else if (columns[0] > 0) keys.push('ArrowLeft', 'Shift+ArrowLeft');
        const key = pick(next, keys);
        if (['ArrowDown', 'Shift+ArrowDown', 'Enter'].includes(key)) rows = [rows[0], rows[1] + 1];
        if (['ArrowUp', 'Shift+ArrowUp', 'Shift+Enter'].includes(key)) rows = [rows[0] - 1, rows[1]];
        if (['ArrowRight', 'Shift+ArrowRight', 'Tab'].includes(key)) columns = [columns[0], columns[1] + 1];
        // Enter after Tabs goes back to the column the Tabs started from, which is inside the bounds.
        if (['ArrowLeft', 'Shift+ArrowLeft', 'Shift+Tab'].includes(key)) columns = [columns[0] - 1, columns[1]];
        if (key === 'Home') columns = [0, columns[1]];
        steps.push({ kind: 'key', key });
        previous = key;
    }
    return steps;
}

/**
 * Holds `grid` where it stands for the run: the page is scrolled to put its top near the window's, and
 * every element laid out before it keeps the height it has now. A status the page writes above the grid
 * — a refusal said in a line that wraps — would otherwise move the grid under a press already aimed, and
 * where it moves first in one run than the other the press lands on another cell.
 */
async function holdGridInPlace(page, grid) {
    await alterPage(page, (root) => {
        const held = [];
        for (let node = root; node && node !== document.body; node = node.parentElement) {
            for (let before = node.previousElementSibling; before; before = before.previousElementSibling) {
                held.push([before, before.style.height, before.style.overflow]);
                before.style.height = `${before.getBoundingClientRect().height}px`;
                before.style.overflow = 'hidden';
            }
        }
        const scrolled = window.scrollY;
        window.scrollTo(0, window.scrollY + root.getBoundingClientRect().top - 8);
        return () => {
            for (const [element, height, overflow] of held) {
                element.style.height = height;
                element.style.overflow = overflow;
            }
            window.scrollTo(0, scrolled);
        };
    }, await grid.elementHandle());
}

/** The centres of the cells `rows` × `columns` of `grid`, each checked to lie inside its Viewport and the window. */
async function cellPlaces(grid, rows, columns) {
    const viewport = await grid.locator('.ex-viewport').first().boundingBox();
    const window = await grid.page().evaluate(() => ({ width: innerWidth, height: innerHeight }));
    const places = {};
    for (let row = 0; row < rows; row++) {
        for (let column = 0; column < columns; column++) {
            const box = await grid.locator(`[id$='-r${row}c${column}']`).boundingBox();
            expect(box, `r${row}c${column} is on screen`).not.toBeNull();
            const place = { x: box.x + box.width / 2, y: box.y + box.height / 2 };
            expect(place.x > viewport.x && place.x < viewport.x + viewport.width
                && place.y > viewport.y && place.y < viewport.y + viewport.height
                && place.x < window.width && place.y < window.height, `r${row}c${column} is in view`).toBe(true);
            places[`r${row}c${column}`] = place;
        }
    }
    return places;
}

/** What a grid shows: the Focus, the Selection's ranges as painted, the cells' text and the editor's. */
const readGrid = (grid, rows, columns) => grid.evaluate((root, size) => {
    const id = (value) => value?.match(/r\d+c\d+$/)?.[0] ?? value ?? null;
    const carrier = root.matches('[aria-activedescendant]') ? root : root.querySelector('[aria-activedescendant]');
    const cells = [];
    for (let row = 0; row < size.rows; row++) {
        cells.push([...Array(size.columns).keys()]
            .map((column) => root.querySelector(`[id$='-r${row}c${column}']`)?.textContent ?? null));
    }
    const editor = root.querySelector('.ex-viewport input.ex-editor, .ex-viewport textarea.ex-editor, .ex-viewport .ex-editor input');
    return {
        focus: id(carrier?.getAttribute('aria-activedescendant')),
        selection: [...root.querySelectorAll('.ex-selection .ex-range')].map((range) => range.getAttribute('style')),
        cells,
        editor: editor ? editor.value : null,
        popover: [...root.querySelectorAll('.ex-popover')].map((p) => p.getAttribute('aria-label')),
    };
}, { rows, columns });

// A run sent at machine speed is answered one input at a time behind the first one unanswered: on the
// Server host each held key takes a round trip, and a sequence that types a few words holds a hundred
// inputs, which take longer to answer than settled's usual bound.
const RUN_ANSWERED_MS = 60_000;

async function settleGrid(grid) {
    await settled(grid, { timeout: RUN_ANSWERED_MS });
    await circuitQuiet();
    await settled(grid, { timeout: RUN_ANSWERED_MS });
}

const FEATURE_ROWS = 7;      // rows 0–6, in view in the 320 px Viewport with room to spare
const FEATURE_COLUMNS = 4;   // Book (read-only), Trader, Notional, Narrow
const featuresGrid = (page) => page.locator('.ex-grid').first();

const gridScenario = {
    async open(page) {
        await page.goto('/features');
        await expect(featuresGrid(page).locator('.ex-row').first()).toBeVisible();
        await expectTabStopTaken(featuresGrid(page));
        await holdGridInPlace(page, featuresGrid(page));
    },
    places: (page) => cellPlaces(featuresGrid(page), FEATURE_ROWS, FEATURE_COLUMNS),
    act: actOnPage,
    settle: (page) => settleGrid(featuresGrid(page)),
    async read(page) {
        return {
            grid: await readGrid(featuresGrid(page), FEATURE_ROWS, FEATURE_COLUMNS),
            keyboard: await keyboardAt(page),
            statuses: await statuses(page, ['#edit-status', '#history-status', '#clear-status', '#edit-discarded-status', '#commit-refused-status']),
        };
    },
};

const SHEET_ROWS = 8;      // rows 1–8 of /sheet's Sheet, above its Linked Table's totals
const SHEET_COLUMNS = 5;   // A–E

const sheetScenario = {
    async open(page) {
        await openSheet(page);
        await holdGridInPlace(page, sheet(page));
    },
    places: (page) => cellPlaces(sheet(page), SHEET_ROWS, SHEET_COLUMNS),
    act: actOnPage,
    settle: (page) => settleGrid(sheet(page)),
    async read(page) {
        const grid = sheet(page);
        return {
            grid: await readGrid(grid, SHEET_ROWS, SHEET_COLUMNS),
            bar: await bar(grid).inputValue(),
            name: await nameBox(grid).inputValue(),
            keyboard: await keyboardAt(page),
        };
    },
};

// ---- What every scenario shares --------------------------------------------------------------------

async function actOnPage(page, step, places) {
    if (step.kind === 'key') {
        await page.keyboard.press(step.key);
    } else if (step.kind === 'type') {
        await page.keyboard.type(step.text);
    } else {
        const place = places[step.target];
        if (step.shift) await page.keyboard.down('Shift');
        await page.mouse.click(place.x, place.y);
        if (step.shift) await page.keyboard.up('Shift');
    }
}

/** Where the keyboard is, said as a reader would: the element, its role or ex- classes, and its name. */
const keyboardAt = (page) => page.evaluate(() => {
    const active = document.activeElement;
    if (!active || active === document.body) return 'body';
    const classes = [...active.classList].filter((c) => c.startsWith('ex-') || c.startsWith('mud-ex-')).sort().join('.');
    const name = active.getAttribute('aria-label') ?? active.labels?.[0]?.textContent?.trim()
        ?? active.getAttribute('name') ?? active.textContent?.trim().slice(0, 40) ?? '';
    return `${active.tagName.toLowerCase()}${classes ? `.${classes}` : ''}[${active.getAttribute('role') ?? ''}] ${name}`;
});

async function statuses(page, selectors) {
    const read = {};
    for (const selector of selectors) {
        const status = page.locator(selector);
        read[selector] = (await status.count()) > 0 ? (await status.textContent()).trim() : null;
    }
    return read;
}

/** Each product's scenario and the steps a seed draws for it, by the product's name. */
export const scenarios = {
    ExGrid: {
        scenario: gridScenario,
        steps: (next) => gridSteps(next, {
            rows: FEATURE_ROWS, columns: FEATURE_COLUMNS, first: 'r0c1', texts: ['Alpha', 'Bravo 2', '42', '7.5', 'x'],
        }),
    },
    ExSheet: {
        scenario: sheetScenario,
        steps: (next) => gridSteps(next, {
            rows: SHEET_ROWS, columns: SHEET_COLUMNS, first: 'r3c3', texts: ['=1+', '=A2*2', '12', 'note', '7.5', '=SUM('],
        }),
    },
};
