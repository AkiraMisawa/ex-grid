import { test, expect, alterPage, roundTrip, setRoundTrip, watchNextKey } from './fixtures.mjs';
import { SERVER } from './hosting.mjs';

// The harness itself (ADR-0048): a spec file boots the app once, each test mounts its page
// afresh, and whatever a test changes outside its own page the harness puts back — or names.
// The tests come in pairs, and the order is the point: the first leaves something behind, the
// second says what it found. A file's tests run in order on one worker (playwright.config.mjs).
// A second test skips itself when its first has not run in this worker — alone, under --grep or
// --last-failed, it would pass without having tested anything.

function grid(page) {
    return page.locator('.ex-grid').first();
}

// The document a test runs in, told apart by its time origin, which a document keeps for as
// long as it lives: an in-app navigation keeps it, a load gives a new one.
const documentOf = (page) => page.evaluate(() => performance.timeOrigin);
const bootedAt = (page) => page.evaluate(() => new URL(performance.getEntriesByType('navigation')[0].name).pathname);

// What each first test left for its second: the document it ran in.
const left = {};
function leftBy(first) {
    test.skip(!(first in left), `runs after "${first}", whose leftovers it looks for`);
    return left[first];
}

async function openFeatures(page) {
    await page.goto('/features');
    await expect(grid(page).locator('.ex-row').first()).toBeVisible();
}

test('a spec file boots its app once, at the index (ADR-0048)', async ({ page }) => {
    await openFeatures(page);
    expect(await bootedAt(page), 'the document was loaded at the index, not at the page').toBe('/');
    // Inside the test's own page, so nothing the harness has to put back.
    await grid(page).evaluate((root) => root.setAttribute('data-harness-first', ''));
    left.boots = await documentOf(page);
});

test('the next test mounts its page afresh on the same document (ADR-0048)', async ({ page }) => {
    const document = leftBy('boots');
    await openFeatures(page);
    expect(await documentOf(page), 'no second boot').toBe(document);
    await expect(grid(page), 'a new grid, not the last test\'s').not.toHaveAttribute('data-harness-first', /.*/);
});

test('what a test changed through the harness is put back when it ends (ADR-0048)', async ({ page, context }) => {
    await page.setViewportSize({ width: 900, height: 600 });
    await openFeatures(page);
    await alterPage(page, () => {
        const original = navigator.clipboard.writeText;
        navigator.clipboard.writeText = async () => { };
        document.body.style.setProperty('--harness-probe', '1px');
        return () => {
            navigator.clipboard.writeText = original;
            document.body.style.removeProperty('--harness-probe');
        };
    });
    // A key the test never presses: its listener would otherwise hear the next test's keys.
    await watchNextKey(page, 'q');
    await context.grantPermissions(['clipboard-read']);
    await setRoundTrip(150);
    await page.mouse.move(300, 300);
    // Scrolled, text selected, and the keyboard's place at the end of the page: none of which
    // a fresh load has.
    const moved = await page.evaluate(() => {
        window.scrollTo(0, 400);
        getSelection().selectAllChildren(document.querySelector('h1'));
        const buttons = document.querySelectorAll('button');
        buttons[buttons.length - 1].focus();
        return { scrolled: scrollY, selected: !getSelection().isCollapsed };
    });
    expect(moved.scrolled, 'the page scrolls at this size, or the next test checks nothing').toBeGreaterThan(0);
    expect(moved.selected).toBe(true);
    left.changes = true;
});

test('the next test finds none of it (ADR-0048)', async ({ page }) => {
    leftBy('changes');
    await openFeatures(page);
    const found = await page.evaluate(async () => ({
        writeTextIsNative: Function.prototype.toString.call(navigator.clipboard.writeText).includes('[native code]'),
        probe: document.body.style.getPropertyValue('--harness-probe'),
        clipboardRead: (await navigator.permissions.query({ name: 'clipboard-read' })).state,
        width: innerWidth,
        scrolled: scrollY,
        selected: getSelection().rangeCount > 0 && !getSelection().isCollapsed,
    }));
    expect(found).toEqual({
        writeTextIsNative: true, probe: '', clipboardRead: 'prompt', width: 1280, scrolled: 0, selected: false,
    });
    // Back to no delay on the Server host; WebAssembly has no round trip to set.
    expect(await roundTrip()).toBe(SERVER ? 0 : null);
    await page.keyboard.press('q');
    expect(await page.evaluate(() => window.__keySeen), 'the last test\'s key watch is gone').toBeUndefined();
    // No row under a pointer the last test left over the grid: the pointer is in the corner.
    await expect(grid(page).locator('.ex-hover-row')).toHaveCount(0);
    // The next Tab starts from the top of the document, as it does after a load.
    const first = await page.evaluate(() => [...document.querySelectorAll('a[href], button, input, select, textarea, [tabindex]')]
        .find((el) => el.tabIndex >= 0 && !el.disabled && el.getClientRects().length > 0)?.outerHTML);
    await page.keyboard.press('Tab');
    expect(await page.evaluate(() => document.activeElement?.outerHTML)).toBe(first);
});

test.describe('a change made behind the harness\'s back is named, and the page is not handed on (ADR-0048)', () => {
    test.describe(() => {
        test.use({ expectedLeaks: [/navigator\.clipboard\.write\b/] });
        test('a native stubbed with page.evaluate', async ({ page }) => {
            await openFeatures(page);
            await page.evaluate(() => { navigator.clipboard.write = async () => { }; });
            left.stubbed = await documentOf(page);
        });
    });

    test('after it, the next test has a document of its own', async ({ page }) => {
        const document = leftBy('stubbed');
        await openFeatures(page);
        expect(await documentOf(page)).not.toBe(document);
    });

    test.describe(() => {
        test.use({ expectedLeaks: [/document\.hasFocus/] });
        test('a native stubbed on its instance rather than its prototype', async ({ page }) => {
            await openFeatures(page);
            await page.evaluate(() => { document.hasFocus = () => true; });
        });
    });

    test.describe(() => {
        // Named twice: in the markup, and as a stylesheet the document did not have. One pattern
        // for both, because test.use reads a list whose second item is an object — a RegExp is
        // one — as Playwright's own [value, options] pair.
        test.use({ expectedLeaks: [/harness-leak|stylesheets/] });
        test('an element left in the head', async ({ page }) => {
            await openFeatures(page);
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
            await openFeatures(page);
            await grid(page).evaluate((root) => root.parentElement.setAttribute('data-harness-leak', ''));
        });
    });

    test.describe(() => {
        test.use({ expectedLeaks: [/stylesheets/] });
        test('a rule inserted into a stylesheet, which the markup does not show', async ({ page }) => {
            await openFeatures(page);
            await page.evaluate(() => document.styleSheets[0].insertRule('.harness-leak { color: red; }'));
        });
    });

    test.describe(() => {
        test.use({ expectedLeaks: [/window\.harnessLeak/] });
        test('a global left on window', async ({ page }) => {
            await openFeatures(page);
            await page.evaluate(() => { window.harnessLeak = true; });
        });
    });
});

test('a test that leaves a key held hands no page on (ADR-0048)', async ({ page }) => {
    await openFeatures(page);
    left.keyHeld = await documentOf(page);
    await page.keyboard.down('Shift');
});

test('after it, the next test boots, with no key held (ADR-0048)', async ({ page }) => {
    const document = leftBy('keyHeld');
    await openFeatures(page);
    expect(await documentOf(page)).not.toBe(document);
    await watchNextKey(page, 'a');
    await page.keyboard.press('a');
    expect(await page.evaluate(() => window.__keySeen), 'a plain a, not a Shift+A').toBe(true);
});

test('a test that leaves a mouse button held hands no page on (ADR-0048)', async ({ page }) => {
    await openFeatures(page);
    left.buttonHeld = await documentOf(page);
    await page.mouse.move(5, 5);
    await page.mouse.down();
});

test('after it too, the next test boots (ADR-0048)', async ({ page }) => {
    const document = leftBy('buttonHeld');
    await openFeatures(page);
    expect(await documentOf(page)).not.toBe(document);
});

test('a test whose console reports an error hands no page on (ADR-0048)', async ({ page }) => {
    test.fail(true, 'reports an error on purpose, which CON-1 fails: the point is what the next test is given');
    await openFeatures(page);
    left.reported = await documentOf(page);
    await page.evaluate(() => console.error('harness.spec: an error on purpose'));
});

test('after that, the next test boots (ADR-0048)', async ({ page }) => {
    const document = leftBy('reported');
    await openFeatures(page);
    expect(await documentOf(page)).not.toBe(document);
});

test('a test that fails hands no page on (ADR-0048)', async ({ page }) => {
    test.fail(true, 'fails on purpose, to see what the next test is given');
    await openFeatures(page);
    left.failed = await documentOf(page);
    expect(false).toBe(true);
});

test('after a failure, the next test boots (ADR-0048)', async ({ page }) => {
    const document = leftBy('failed');
    await openFeatures(page);
    expect(await documentOf(page)).not.toBe(document);
});

test.describe(() => {
    test.use({ freshDocument: true });
    test('a test that asks for a document of its own loads its page for real (ADR-0048)', async ({ page }) => {
        await openFeatures(page);
        expect(await bootedAt(page)).toBe('/features');
    });
});
