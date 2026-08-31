import { test, expect } from '@playwright/test';

// A classic (non-overlay) scrollbar takes a strip out of the box the element declares.
// macOS draws overlay scrollbars, which take nothing, so on the machine this component
// is developed on the bug this file is about is invisible: every measurement is 0.
//
// The forcing rule below is what stands in for Windows and Linux here. It is not a
// simulation of their scrollbars — it is the same mechanism, a bar that occupies layout
// — and it is what makes the Mac able to fail this suite at all.
//
// It has to be in the document before the first layout: applied afterwards, Chrome keeps
// the overlay bars it has already chosen and every measurement stays 0. That is how the
// first version of this file managed to pass while proving nothing, which is why the
// tests below check that the forcing actually took before they check anything else.
const CLASSIC_SCROLLBARS = `
    .ex-scroller::-webkit-scrollbar { width: 15px; height: 15px; }
    .ex-scroller::-webkit-scrollbar-track { background: #eee; }
    .ex-scroller::-webkit-scrollbar-thumb { background: #888; }
`;

// Fractional device pixel ratios put sub-pixel edges on both rectangles, and a Focus
// outline flush against the client edge can round the wrong way by a hair. A whole
// pixel of slack is far below the ~15px this suite exists to catch.
const SLACK_PX = 1;

// How close to a corner counts as "the grid has finished moving there". Wide enough that
// a wrong answer still arrives and gets asserted on, narrow enough that not having moved
// at all cannot pass for having arrived. See moveToCorner.
const SETTLED_PX = 64;

/**
 * Where the Focus outline is, and where the scroller's readable area is, measured in the
 * same coordinates at the same instant.
 *
 * Both are viewport-relative, which is the point: an earlier hand-rolled version of this
 * check measured the overlay in content coordinates and compared it against a client box
 * that moved whenever the PAGE scrolled or the painted slice translated, and reported
 * nonsense for hours. Two getBoundingClientRects taken together cannot drift apart.
 */
async function focusAgainstClientBox(page) {
    return page.evaluate(() => {
        const scroller = document.querySelector('.ex-scroller');
        if (!scroller) {
            return { error: 'no .ex-scroller on the page' };
        }
        const outer = scroller.getBoundingClientRect();
        // clientLeft/clientTop are the borders; clientWidth/clientHeight are the padding
        // box minus the scrollbar — the browser's own answer to "what is readable".
        const client = {
            left: outer.left + scroller.clientLeft,
            top: outer.top + scroller.clientTop,
            right: outer.left + scroller.clientLeft + scroller.clientWidth,
            bottom: outer.top + scroller.clientTop + scroller.clientHeight,
        };
        const focus = [...document.querySelectorAll('.ex-focus')]
            .map((element) => element.getBoundingClientRect())
            // A zero-sized layer element is not a Focus; the painted outline has area.
            .filter((rect) => rect.width > 0 && rect.height > 0)
            .map((rect) => ({ left: rect.left, top: rect.top, right: rect.right, bottom: rect.bottom }));
        return {
            client,
            focus,
            diagnostics: {
                platform: navigator.userAgentData?.platform ?? navigator.platform,
                dpr: window.devicePixelRatio,
                gutterWidth: scroller.offsetWidth - scroller.clientWidth,
                gutterHeight: scroller.offsetHeight - scroller.clientHeight,
            },
        };
    });
}

/**
 * The invariant, and the only thing this file asserts: wherever the Focus ends up, all of
 * it is inside the area the scrollbars left readable.
 *
 * Deliberately not "the gutter is 15px on Windows". What a native scrollbar is worth in
 * CSS pixels — at 100% zoom or any other — is not something this project has been able to
 * measure, and asserting a guess would be recording a guess. This holds on every platform
 * at every zoom, or the component is broken there.
 */
function expectFocusInsideClientBox(measured, testInfo) {
    expect(measured.error).toBeUndefined();
    testInfo.annotations.push({ type: 'measured', description: JSON.stringify(measured.diagnostics) });
    expect(measured.focus, 'the grid painted no Focus outline to check').toHaveLength(1);

    const [focus] = measured.focus;
    const { client } = measured;
    expect(focus.left, 'the Focus is left of the readable area').toBeGreaterThanOrEqual(client.left - SLACK_PX);
    expect(focus.top, 'the Focus is above the readable area').toBeGreaterThanOrEqual(client.top - SLACK_PX);
    expect(focus.right, 'the Focus is behind the vertical scrollbar').toBeLessThanOrEqual(client.right + SLACK_PX);
    expect(focus.bottom, 'the Focus is behind the horizontal scrollbar').toBeLessThanOrEqual(client.bottom + SLACK_PX);
}

/**
 * Guards against the only way these tests can lie: a scrollbar that takes no space makes
 * every assertion below trivially true, and they would go on passing with the fix
 * reverted. Both axes, because headless Chrome on macOS reserves the vertical strip and
 * leaves the horizontal one an overlay — which is exactly the axis the row band loses.
 */
async function expectScrollbarsOccupyLayout(page) {
    const measured = await focusAgainstClientBox(page);
    // Checked before destructuring: a grid that failed to render answers with `error` and
    // no diagnostics, and reaching into them would replace the message below with a
    // TypeError about reading a property of undefined.
    expect(measured.error).toBeUndefined();
    const { diagnostics } = measured;
    const message = 'the scrollbars are not occupying layout, so this test proves nothing '
        + `(measured ${diagnostics.gutterWidth}x${diagnostics.gutterHeight} on ${diagnostics.platform})`;
    expect(diagnostics.gutterWidth, message).toBeGreaterThan(0);
    expect(diagnostics.gutterHeight, message).toBeGreaterThan(0);
}

/** Opens /wide, gives the grid the keyboard, and puts a Focus on a cell. */
async function openGrid(page, { classicScrollbars }) {
    if (classicScrollbars) {
        await page.addInitScript((css) => {
            document.addEventListener('DOMContentLoaded', () => {
                const style = document.createElement('style');
                style.textContent = css;
                document.head.append(style);
            });
        }, CLASSIC_SCROLLBARS);
    }
    await page.goto('/wide');
    await expect(page.locator('.ex-row').first()).toBeVisible();
    // Clicking a cell is what selects one; the click also gives the root the DOM focus
    // the capture-phase listener depends on (ADR-0018).
    await page.locator('.ex-viewport').click({ position: { x: 40, y: 40 } });
    await expect(page.locator('.ex-focus')).toHaveCount(1);
}

/**
 * Ctrl+End is the far corner of the whole result, Ctrl+Home the near one (ADR-0012).
 * Both are pressed AFTER whatever change is being tested, which is the entire point: read
 * the position without pressing and the Focus may simply still be sitting where the
 * previous scroll offset happened to leave it, inside the box by luck.
 */
async function moveToCorner(page, key) {
    await page.keyboard.press(`Control+${key}`);
    // Waited for by naming the position the grid is supposed to land on, not by watching
    // for the scroll to stop changing. "Stopped changing" is true before the keystroke has
    // been round-tripped at all, so it would sample the PREVIOUS corner and, at Ctrl+Home
    // from the top, would never notice the grid had failed to move.
    //
    // Ctrl+End is the browser's own far extreme on both axes: the content is the whole
    // result, and revealing the last cell right- and bottom-aligns it against the READABLE
    // area, which is exactly scrollWidth - clientWidth. That the C# arithmetic agrees with
    // the browser there is not assumed — if it disagreed this wait would time out and say
    // so, which is most of why the condition is written this way.
    //
    // Ctrl+Home only names the vertical extreme, and "at the top" can already be true
    // before the keystroke has been round-tripped: **never call this with 'Home' from a
    // grid that is already at the top** — it would return immediately and the caller would
    // measure the state before the press. Every caller here reaches Home from End.
    //
    // The reason it names only the one axis: /wide pins its first two columns, and a
    // Pinned Column covers the Viewport's left edge without occupying anything ahead of the
    // content, so a Focus on column 0 is already whole on screen and nothing needs to
    // scroll sideways to show it (ADR-0004/0013). Asserting left === 0 here would be
    // asserting a behaviour the grid deliberately does not have.
    // The tolerance is wide on purpose. This is asking "has the grid finished moving",
    // not "did it land in the right place" — that is the assertion's job, and a wait
    // strict enough to answer it would turn a real failure into a timeout, which says
    // nothing about scrollbars. A whole scrollbar's width fits inside SETTLED_PX, so the
    // 15px error this suite exists to catch reaches the assertion and gets named.
    await page.waitForFunction(({ corner, settled }) => {
        const scroller = document.querySelector('.ex-scroller');
        if (!scroller) {
            return false;
        }
        const at = (actual, want) => Math.abs(actual - want) <= settled;
        if (corner === 'Home') {
            return at(scroller.scrollTop, 0);
        }
        return at(scroller.scrollTop, scroller.scrollHeight - scroller.clientHeight)
            && at(scroller.scrollLeft, scroller.scrollWidth - scroller.clientWidth);
    }, { corner: key, settled: SETTLED_PX }, { polling: 'raf' });
}

test.describe('the Focus is never behind a scrollbar (ADR-0012/0013)', () => {
    test('with the platform\'s own scrollbars', async ({ page }, testInfo) => {
        await openGrid(page, { classicScrollbars: false });

        await moveToCorner(page, 'End');
        expectFocusInsideClientBox(await focusAgainstClientBox(page), testInfo);

        await moveToCorner(page, 'Home');
        expectFocusInsideClientBox(await focusAgainstClientBox(page), testInfo);
    });

    test('with scrollbars that occupy layout', async ({ page }, testInfo) => {
        await openGrid(page, { classicScrollbars: true });

        await expectScrollbarsOccupyLayout(page);

        await moveToCorner(page, 'End');
        expectFocusInsideClientBox(await focusAgainstClientBox(page), testInfo);

        await moveToCorner(page, 'Home');
        expectFocusInsideClientBox(await focusAgainstClientBox(page), testInfo);
    });

    // What a native scrollbar is worth in CSS pixels under zoom could not be measured on
    // the machine this was written on — macOS forces overlay scrollbars and
    // --disable-features=OverlayScrollbar does not turn them off. Rather than guess a
    // number, the unknown is written as the behaviour that has to hold either way: the
    // grid is told what the gutter is (a ResizeObserver on the content box, ADR-0021), so
    // if the width changes the notification arrives, and if it does not, nothing needed
    // to happen. THIS TEST IS WHERE THE ANSWER LIVES — run it on Windows and it either
    // passes, or it names the zoom level at which the chain breaks.
    test('at every zoom level, after moving again', async ({ page }, testInfo) => {
        await openGrid(page, { classicScrollbars: true });
        await expectScrollbarsOccupyLayout(page);
        const client = await page.context().newCDPSession(page);
        const size = page.viewportSize() ?? { width: 1280, height: 720 };

        for (const deviceScaleFactor of [1, 1.25, 2, 1]) {
            await client.send('Emulation.setDeviceMetricsOverride', {
                width: size.width,
                height: size.height,
                deviceScaleFactor,
                mobile: false,
            });

            // Both corners, both pressed after the change. A grid that had the right
            // answer at 100% and kept using it would still be sitting at a legal offset
            // until something asked it to move.
            //
            // End first, and that order matters: moveToCorner waits for the grid to reach
            // the corner, and "at the top" is already true before Ctrl+Home has even been
            // round-tripped. Starting with Home would let the first iteration measure the
            // state BEFORE the keystroke — the same vacuous pass this test exists to avoid.
            await moveToCorner(page, 'End');
            const measured = await focusAgainstClientBox(page);
            testInfo.annotations.push({
                type: 'zoom',
                description: `dpr ${deviceScaleFactor}: gutter ` +
                    `${measured.diagnostics.gutterWidth}x${measured.diagnostics.gutterHeight}`,
            });
            expectFocusInsideClientBox(measured, testInfo);

            await moveToCorner(page, 'Home');
            expectFocusInsideClientBox(await focusAgainstClientBox(page), testInfo);
        }

        await client.send('Emulation.clearDeviceMetricsOverride');
    });
});
