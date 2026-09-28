import { test, expect, patchPage, watchNextKey } from './fixtures.mjs';

// The harness itself (ADR-0048): a spec file boots the app once, each test mounts its page
// afresh, and whatever a test changes outside its own page the harness puts back — or names.
// The tests come in pairs, and the order is the point: the first leaves something behind, the
// second says what it found. A file's tests run in order on one worker (playwright.config.mjs).

function grid(page) {
    return page.locator('.ex-grid').first();
}

// The document a test runs in, told apart by its time origin, which a document keeps for as
// long as it lives: an in-app navigation keeps it, a load gives a new one.
const documentOf = (page) => page.evaluate(() => performance.timeOrigin);
const bootedAt = (page) => page.evaluate(() => new URL(performance.getEntriesByType('navigation')[0].name).pathname);

let firstDocument;

test('a spec file boots its app once, at the index (ADR-0048)', async ({ page }) => {
    await page.goto('/features');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
    firstDocument = await documentOf(page);
    expect(await bootedAt(page), 'the document was loaded at the index, not at the page').toBe('/');
    // Inside the test's own page, so nothing the harness has to put back.
    await grid(page).evaluate((root) => root.setAttribute('data-harness-first', ''));
});

test('the next test mounts its page afresh on the same document (ADR-0048)', async ({ page }) => {
    await page.goto('/features');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
    expect(await documentOf(page), 'no second boot').toBe(firstDocument);
    await expect(grid(page), 'a new grid, not the last test\'s').not.toHaveAttribute('data-harness-first', /.*/);
});

test('what a test changed through the harness is put back when it ends (ADR-0048)', async ({ page, context }) => {
    await page.goto('/features');
    await patchPage(page, () => {
        const original = navigator.clipboard.writeText;
        navigator.clipboard.writeText = async () => { };
        document.body.style.setProperty('--ex-harness-probe', '1px');
        return () => {
            navigator.clipboard.writeText = original;
            document.body.style.removeProperty('--ex-harness-probe');
        };
    });
    // A key the test never presses: its listener would otherwise hear the next test's keys.
    await watchNextKey(page, 'q');
    await context.grantPermissions(['clipboard-read']);
    await page.setViewportSize({ width: 900, height: 600 });
    await page.mouse.move(300, 300);
});

test('the next test finds none of it (ADR-0048)', async ({ page }) => {
    await page.goto('/features');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
    const found = await page.evaluate(async () => ({
        writeTextIsNative: Function.prototype.toString.call(navigator.clipboard.writeText).includes('[native code]'),
        probe: document.body.style.getPropertyValue('--ex-harness-probe'),
        clipboardRead: (await navigator.permissions.query({ name: 'clipboard-read' })).state,
        width: innerWidth,
        scrolled: scrollY,
    }));
    expect(found).toEqual({ writeTextIsNative: true, probe: '', clipboardRead: 'prompt', width: 1280, scrolled: 0 });
    await page.keyboard.press('q');
    expect(await page.evaluate(() => window.__keySeen), 'the last test\'s key watch is gone').toBeUndefined();
    // No row under a pointer the last test left over the grid: the pointer is in the corner.
    await expect(grid(page).locator('.ex-hover-row')).toHaveCount(0);
});

test.describe('a change made behind the harness\'s back is named, and the page is not handed on (ADR-0048)', () => {
    test.describe(() => {
        test.use({ expectedLeaks: [/navigator\.clipboard\.write\b/] });
        test('a native stubbed with page.evaluate', async ({ page }) => {
            await page.goto('/features');
            await page.evaluate(() => {
                window.__harnessMarker = 'stubbed';
                navigator.clipboard.write = async () => { };
            });
        });
    });

    test('after it, the next test has a document of its own', async ({ page }) => {
        await page.goto('/features');
        expect(await page.evaluate(() => window.__harnessMarker)).toBeUndefined();
    });

    test.describe(() => {
        test.use({ expectedLeaks: [/harness-leak/] });
        test('an element left in the head', async ({ page }) => {
            await page.goto('/features');
            await page.evaluate(() => {
                const style = document.createElement('style');
                style.id = 'harness-leak';
                document.head.append(style);
            });
        });
    });

    test.describe(() => {
        // DIR-2's case: the grid's parent is #app on WebAssembly and body on the Server host,
        // and neither is part of the page Blazor removes.
        test.use({ expectedLeaks: [/data-harness-leak/] });
        test('an attribute left on the grid\'s parent', async ({ page }) => {
            await page.goto('/features');
            await grid(page).evaluate((root) => root.parentElement.setAttribute('data-harness-leak', ''));
        });
    });
});

test('a test that leaves a key held hands no page on (ADR-0048)', async ({ page }) => {
    await page.goto('/features');
    await page.evaluate(() => { window.__harnessMarker = 'held'; });
    await page.keyboard.down('Shift');
});

test('after it, the next test boots, with no key held (ADR-0048)', async ({ page }) => {
    await page.goto('/features');
    expect(await page.evaluate(() => window.__harnessMarker)).toBeUndefined();
    await watchNextKey(page, 'a');
    await page.keyboard.press('a');
    expect(await page.evaluate(() => window.__keySeen), 'a plain a, not a Shift+A').toBe(true);
});

test('a test that fails hands no page on (ADR-0048)', async ({ page }) => {
    test.fail(true, 'fails on purpose, to see what the next test is given');
    await page.goto('/features');
    await page.evaluate(() => { window.__harnessMarker = 'failed'; });
    expect(false).toBe(true);
});

test('after it, the next test boots (ADR-0048)', async ({ page }) => {
    await page.goto('/features');
    expect(await page.evaluate(() => window.__harnessMarker)).toBeUndefined();
});

test.describe(() => {
    test.use({ freshDocument: true });
    test('a test that asks for a document of its own loads its page for real (ADR-0048)', async ({ page }) => {
        await page.goto('/features');
        expect(await bootedAt(page)).toBe('/features');
    });
});
