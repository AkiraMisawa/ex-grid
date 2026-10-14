import { test, expect, alterPage } from './fixtures.mjs';

test('ADR-0070: passing keys out hands over the held keys before a later key or focus departure can overtake them', async ({ page }) => {
    await page.goto('/features');
    await expect(page.locator('.ex-grid').first()).not.toHaveAttribute('aria-busy', /.*/);
    await alterPage(page, async () => {
        const module = await import('/_content/ExGrid/ex-grid.min.js');
        const frame = document.createElement('div');
        const root = document.createElement('div');
        root.className = 'ex-grid';
        root.tabIndex = 0;
        const scroller = document.createElement('div');
        root.append(scroller);
        const elsewhere = document.createElement('button');
        frame.append(root, elsewhere);
        document.body.append(frame);
        const state = window.__keysPassedOutCheck = { heard: [], asked: [] };
        const answer = new Promise(resolve => { state.answer = resolve; });
        const core = {
            invokeMethodAsync(name) {
                state.asked.push(name);
                return name === 'OnKeyAsync' ? answer : Promise.resolve();
            },
        };
        const handle = module.attach(root, scroller, core, ['Escape', 'ArrowDown', 'ArrowRight'], false, 150, false, [], false, true);
        frame.addEventListener('keydown', event => { state.heard.push(event.key); });
        root.focus();
        const key = name => root.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));
        key('Escape');
        key('ArrowDown');
        state.before = [...state.heard];
        // The Consumer answers OnLeave before the Escape's own answer is back. The keyboard may
        // leave immediately, and a later key must follow the keys already held behind that Escape.
        handle.passKeysOut();
        key('ArrowRight');
        elsewhere.focus();
        return async () => {
            state.answer();
            await answer;
            handle.dispose();
            frame.remove();
            delete window.__keysPassedOutCheck;
        };
    });

    expect(await page.evaluate(() => ({
        before: window.__keysPassedOutCheck.before,
        heard: window.__keysPassedOutCheck.heard,
        asked: window.__keysPassedOutCheck.asked.filter(name => name === 'OnKeyAsync'),
    }))).toEqual({ before: [], heard: ['ArrowDown', 'ArrowRight'], asked: ['OnKeyAsync'] });
});

// The shipped module's hand-back contract, with the Consumer's answer under the test's control.
// No latency or scheduling probability is needed: its keys have not been answered until we answer.
for (const granted of [true, false]) {
    test(`ADR-0050/ADR-0070: a frame's hand-back completes only after its held keys are collected (${granted ? 'granted' : 'declined'})`, async ({ page }) => {
        await page.goto('/features');
        await expect(page.locator('.ex-grid').first()).not.toHaveAttribute('aria-busy', /.*/);
        await alterPage(page, async (granted) => {
            const module = await import('/_content/ExGrid/ex-grid.min.js');
            const box = document.createElement('div');
            const root = document.createElement('div');
            root.className = 'ex-grid';
            root.tabIndex = 0;
            const scroller = document.createElement('div');
            root.append(scroller);
            const frame = document.createElement('button');
            const elsewhere = document.createElement('button');
            box.append(root, frame, elsewhere);
            document.body.append(box);
            const state = window.__keyboardReturnCheck = { completed: false, asked: null };
            const keys = new Promise(resolve => { state.answer = resolve; });
            const core = {
                invokeMethodAsync(name, ticket) {
                    if (name === 'KeysTypedOnTheWay') {
                        state.asked = ticket;
                        return keys;
                    }
                    return Promise.resolve();
                },
            };
            const handle = module.attach(root, scroller, core, [], false, 150, false, [], false, false);
            (granted ? frame : elsewhere).focus();
            const completed = Promise.resolve(handle.takeKeyboardBack(frame, 7)).then(() => {
                state.completed = true;
            });
            state.focusGranted = document.activeElement === root;
            return async () => {
                state.answer([]);
                await completed;
                handle.dispose();
                box.remove();
                delete window.__keyboardReturnCheck;
            };
        }, granted);

        expect(await page.evaluate(() => ({
            asked: window.__keyboardReturnCheck.asked,
            completed: window.__keyboardReturnCheck.completed,
            focusGranted: window.__keyboardReturnCheck.focusGranted,
        }))).toEqual({ asked: 7, completed: false, focusGranted: granted });

        await page.evaluate(() => window.__keyboardReturnCheck.answer([]));
        await expect.poll(() => page.evaluate(() => window.__keyboardReturnCheck.completed)).toBe(true);
    });
}
