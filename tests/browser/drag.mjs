// The browser's own drag and drop (HTML5: `draggable`, `dragstart`, `dragover`, `drop`), driven so that its outcome never
// depends on timing (ADR-0220, ADR-0451).
//
// Playwright's Chromium mouse emulates such a drag itself, and decides whether a move began one by reading, one task after
// the first mousemove its page script hears, whether a `dragstart` came. A move the browser hands the page at the next
// frame is not heard by then, so Playwright decides no drag began while the browser has begun one and handed it to the
// DevTools Protocol to carry. The page then hears no mouse event at all until a drop or a cancel the test never sends, and
// a test whose drop should change nothing passes without its drop having been made (pivot.spec's report removals, the
// release of v0.2.0-beta.2). Here every step waits for what the browser says it did instead: a real press and move over
// the Protocol, the page's own `dragstart`, the Protocol's `Input.dragIntercepted`, then each drag event, and at the end
// the page hearing the pointer again. A drag that did not begin is said, naming what the page heard, rather than read as a refusal.

import { expect, noteDrivenDrag } from './fixtures.mjs';

/** A point on `locator`: `position` from its top-left corner, or its middle. */
async function pointOn(locator, position) {
    const box = await locator.boundingBox();
    if (box === null) {
        throw new Error(`drag: ${locator} is not laid out, so there is no point on it to drag from or to`);
    }
    return position === undefined
        ? { x: box.x + box.width / 2, y: box.y + box.height / 2 }
        : { x: box.x + position.x, y: box.y + position.y };
}

/** Whether the element drawn at `point` is `locator`'s element or inside it, and what is drawn there. */
async function hits(locator, point) {
    return locator.evaluate((element, { x, y }) => {
        const top = document.elementFromPoint(x, y);
        const name = top === null ? 'nothing' : `${top.tagName.toLowerCase()}${[...top.classList].map((c) => `.${c}`).join('')}`;
        return { inside: top !== null && (top === element || element.contains(top)), name };
    }, point);
}

/** Steps from `from` to `to`, the last one `to` itself, so the page crosses what lies between as a hand would. */
function* path(from, to, steps) {
    for (let i = 1; i <= steps; i++) {
        yield { x: from.x + ((to.x - from.x) * i) / steps, y: from.y + ((to.y - from.y) * i) / steps };
    }
}

/**
 * Presses `source` and moves until the browser begins its drag, and returns the drag, which the test then moves `over`
 * targets and ends with `drop()` or `cancel()`. Throws, naming what the page heard, if the browser began none: a drag a
 * test means to make is made, never assumed.
 */
export async function startDrag(page, source, { position } = {}) {
    await expect(source).toBeVisible();
    // Brought into view before the press, as a hand would reach it: a scroll once the drag has begun would end it.
    await source.scrollIntoViewIfNeeded();
    const press = await pointOn(source, position);
    const under = await hits(source, press);
    if (!under.inside) {
        throw new Error(`drag: ${source} is not what is drawn at its own point (${press.x}, ${press.y}); ${under.name} is`);
    }
    const session = await page.context().newCDPSession(page);
    // What the page hears of the drag: the moves, the dragstart (whose default a listener may prevent, so it is read when
    // the drag is judged, not when it fires), and, once the drag has ended, a move with no button held, which a page in
    // the middle of a drag never hears. Not the source's dragend: a drop that changes the layout takes the source away,
    // and the dragend goes to an element no longer in the document. Held in the page by the handle alone, so it leaves no
    // global behind; `stop` takes the listeners off.
    const heard = await page.evaluateHandle(() => {
        const state = { moves: [], start: null, armed: false, free: false };
        const moved = (event) => {
            if ((event.buttons & 1) === 1) state.moves.push({ x: event.clientX, y: event.clientY });
            else if (state.armed) state.free = true;
        };
        const started = (event) => { state.start = event; };
        window.addEventListener('mousemove', moved, true);
        window.addEventListener('dragstart', started, true);
        state.stop = () => {
            window.removeEventListener('mousemove', moved, true);
            window.removeEventListener('dragstart', started, true);
        };
        state.read = () => ({
            moves: state.moves,
            heardStart: state.start !== null,
            started: state.start !== null && !state.start.defaultPrevented,
            prevented: state.start !== null && state.start.defaultPrevented,
            free: state.free,
        });
        return state;
    });
    const read = () => heard.evaluate((state) => state.read());
    let intercepted = null;
    const interception = new Promise((resolve) => { session.once('Input.dragIntercepted', (event) => { intercepted = event.data; resolve(event.data); }); });
    const finish = async () => {
        await heard.evaluate((state) => state.stop()).catch(() => { });
        await heard.dispose().catch(() => { });
        await session.send('Input.setInterceptDrags', { enabled: false }).catch(() => { });
        await session.detach().catch(() => { });
    };
    const mouse = (type, point, extra = {}) => session.send('Input.dispatchMouseEvent', { type, x: point.x, y: point.y, ...extra });

    await session.send('Input.setInterceptDrags', { enabled: true });
    await mouse('mouseMoved', press, { button: 'none', buttons: 0 });
    await mouse('mousePressed', press, { button: 'left', buttons: 1, clickCount: 1 });
    // Past Chromium's threshold for a mouse drag (four pixels either way), in one move.
    const away = { x: press.x + 8, y: press.y + 8 };
    await mouse('mouseMoved', away, { button: 'left', buttons: 1 });
    // The page is handed the move at a frame, and judges it in that task: it begins a drag (a dragstart), or hears the
    // move as a move. Once either has happened, what it did with the move is settled.
    const reached = (state) => state.heardStart || state.moves.some((m) => Math.abs(m.x - away.x) <= 1 && Math.abs(m.y - away.y) <= 1);
    await expect.poll(async () => reached(await read()), { message: 'drag: the page has judged the move past the threshold' }).toBe(true);
    const judged = await read();
    if (!judged.started) {
        await mouse('mouseReleased', away, { button: 'left', buttons: 0, clickCount: 1 });
        await finish();
        throw new Error(`drag: the browser began no drag from ${source} (${under.name} at (${press.x}, ${press.y})): `
            + (judged.prevented ? 'its dragstart\'s default was prevented' : 'the page heard no dragstart'));
    }
    noteDrivenDrag(page);
    const data = await interception;
    await session.send('Input.dispatchDragEvent', { type: 'dragEnter', x: away.x, y: away.y, data });
    let at = away;
    let ended = false;
    const end = async (type) => {
        if (ended) throw new Error('drag: this drag has already ended');
        ended = true;
        await session.send('Input.dispatchDragEvent', { type, x: at.x, y: at.y, data: intercepted });
        // The drag is over once the page hears a move with no button held again. Each try moves the pointer a pixel,
        // to another place than the last, since a move to where the pointer already is is not sent on.
        await heard.evaluate((state) => { state.armed = true; });
        let nudge = 0;
        await expect.poll(async () => {
            nudge = 1 - nudge;
            await mouse('mouseMoved', { x: at.x + nudge, y: at.y }, { button: 'none', buttons: 0 });
            return (await read()).free;
        }, { message: `drag: the page hears the pointer again after the ${type}` }).toBe(true);
        await finish();
    };
    return {
        /** Moves the drag over `target`, at `position` from its corner or at its middle, in a few steps. */
        async over(target, { position, steps = 5 } = {}) {
            if (ended) throw new Error('drag: this drag has already ended');
            const to = await pointOn(target, position);
            const view = await page.evaluate(() => ({ width: innerWidth, height: innerHeight }));
            if (to.x < 0 || to.y < 0 || to.x >= view.width || to.y >= view.height) {
                throw new Error(`drag: ${target}'s point (${to.x}, ${to.y}) is outside the window, and a scroll would end the drag`);
            }
            for (const point of path(at, to, steps)) {
                await session.send('Input.dispatchDragEvent', { type: 'dragOver', x: point.x, y: point.y, data: intercepted });
            }
            at = to;
        },
        /** Drops where the drag stands, and waits until the page hears the pointer again. */
        drop: () => end('drop'),
        /** Cancels the drag, as Escape does, and waits until the page hears the pointer again. */
        cancel: () => end('dragCancel'),
    };
}

/** Drags `source` onto `target` and drops it there: the drag must begin, and both ends are waited for. */
export async function dragAndDrop(page, source, target, { sourcePosition, targetPosition } = {}) {
    const drag = await startDrag(page, source, { position: sourcePosition });
    await drag.over(target, { position: targetPosition });
    await drag.drop();
}
