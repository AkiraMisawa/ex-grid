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

    // spec-lint: early-read: reads what the alterPage script recorded synchronously against a stub core; no grid answer is owed
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

        // spec-lint: early-read: reads what the alterPage script recorded synchronously against a stub core whose answer the test holds back
        expect(await page.evaluate(() => ({
            asked: window.__keyboardReturnCheck.asked,
            completed: window.__keyboardReturnCheck.completed,
            focusGranted: window.__keyboardReturnCheck.focusGranted,
        }))).toEqual({ asked: 7, completed: false, focusGranted: granted });

        await page.evaluate(() => window.__keyboardReturnCheck.answer([]));
        await expect.poll(() => page.evaluate(() => window.__keyboardReturnCheck.completed)).toBe(true);
    });
}

// A design-system picker can expose its container without exposing its own text field.
// The conditional hand-off still honours a control the user chose before it arrived.
test('ADR-0039/ADR-0050: a container hands held typing to its text field and preserves an earlier choice', async ({ page }) => {
    await page.goto('/features');
    await expect(page.locator('.ex-grid').first()).not.toHaveAttribute('aria-busy', /.*/);
    await alterPage(page, async () => {
        const module = await import('/_content/ExGrid/ex-grid.min.js');
        const box = document.createElement('div');
        const root = document.createElement('div');
        root.className = 'ex-grid';
        root.tabIndex = 0;
        const scroller = document.createElement('div');
        root.append(scroller);
        const popup = document.createElement('div');
        const slider = document.createElement('input');
        slider.type = 'range';
        const readOnly = document.createElement('input');
        readOnly.readOnly = true;
        const number = document.createElement('input');
        number.type = 'number';
        const hex = document.createElement('input');
        hex.value = '#123456';
        const other = document.createElement('button');
        popup.append(slider, readOnly, number, hex);
        box.append(root, popup, other);
        document.body.append(box);
        const handle = module.attach(root, scroller, { invokeMethodAsync: () => Promise.resolve() }, [], false, 150, false, [], false, true);
        root.focus();
        handle.handKeyboardTo(popup);
        const state = window.__containerHandOff = {
            reachedHex: document.activeElement === hex,
            selected: [hex.selectionStart, hex.selectionEnd],
        };
        hex.setSelectionRange(2, 4);
        handle.handKeyboardTo(popup);
        state.earlySelection = [hex.selectionStart, hex.selectionEnd];
        other.focus();
        handle.handKeyboardTo(popup);
        state.keptOther = document.activeElement === other;
        return () => {
            handle.dispose();
            box.remove();
            delete window.__containerHandOff;
        };
    });
    // spec-lint: early-read: reads what the alterPage script recorded synchronously against a stub core; no grid answer is owed
    expect(await page.evaluate(() => window.__containerHandOff)).toEqual({
        reachedHex: true, selected: [0, 7], earlySelection: [2, 4], keptOther: true,
    });
});

for (const typed of ['', '#FF0000']) {
    test(`ADR-0039: held Enter commits only text changed by the hold (${typed || 'unchanged'})`, async ({ page }) => {
        await page.goto('/features');
        await expect(page.locator('.ex-grid').first()).not.toHaveAttribute('aria-busy', /.*/);
        await alterPage(page, async (typed) => {
            const module = await import('/_content/ExGrid/ex-grid.min.js');
            const box = document.createElement('div');
            const root = document.createElement('div');
            root.className = 'ex-grid';
            root.tabIndex = 0;
            const scroller = document.createElement('div');
            root.append(scroller);
            const popup = document.createElement('div');
            const field = document.createElement('input');
            field.value = '#123456';
            popup.append(field);
            box.append(root, popup);
            document.body.append(box);
            const state = window.__heldFieldCommit = { enters: 0, changes: [] };
            field.addEventListener('keydown', event => { if (event.key === 'Enter') state.enters++; });
            field.addEventListener('change', () => state.changes.push(field.value));
            const handle = module.attach(root, scroller, { invokeMethodAsync: () => Promise.resolve() }, [], false, 150, false, [], false, true);
            root.focus();
            handle.handOff('frame');
            for (const key of [...typed, 'Enter']) {
                root.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
            }
            handle.handKeyboardTo(popup);
            return () => {
                handle.dispose();
                box.remove();
                delete window.__heldFieldCommit;
            };
        }, typed);
        await expect.poll(() => page.evaluate(() => window.__heldFieldCommit.enters)).toBe(1);
        expect(await page.evaluate(() => window.__heldFieldCommit.changes)).toEqual(typed ? [typed] : []);
    });
}

// The frame's Escape handler has not entered until the controlled question is answered.
// Keys behind it must then follow the hand-off that handler declared, not HEX's caret rules.
test('ADR-0039/ADR-0050: a held frame Escape is acknowledged before the next key chooses its destination', async ({ page }) => {
    await page.goto('/features');
    await expect(page.locator('.ex-grid').first()).not.toHaveAttribute('aria-busy', /.*/);
    await alterPage(page, async () => {
        const module = await import('/_content/ExGrid/ex-grid.min.js');
        const box = document.createElement('div');
        const root = document.createElement('div');
        root.className = 'ex-grid';
        root.tabIndex = 0;
        const scroller = document.createElement('div');
        root.append(scroller);
        const popup = document.createElement('input');
        const opener = document.createElement('button');
        box.append(root, popup, opener);
        document.body.append(box);
        const state = window.__frameEscape = { escape: false, down: 0 };
        const entered = new Promise(resolve => { state.entered = resolve; });
        const core = {
            invokeMethodAsync(name) {
                if (name === 'Ping') return entered;
                return Promise.resolve(name === 'KeysTypedOnTheWay' ? [] : undefined);
            },
        };
        const handle = module.attach(root, scroller, core, [], false, 150, false, [], false, true);
        popup.addEventListener('keydown', event => { if (event.key === 'Escape') state.escape = true; });
        opener.addEventListener('keydown', event => { if (event.key === 'ArrowDown') state.down++; });
        state.answer = async () => {
            const returned = handle.takeKeyboardBack(popup, 1);
            handle.handOff('frame');
            handle.handKeyboardTo(opener);
            state.entered();
            await returned;
        };
        root.focus();
        handle.handOff('frame');
        for (const key of ['Escape', 'ArrowDown']) {
            root.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
        }
        handle.handKeyboardTo(popup);
        return () => {
            state.entered();
            handle.dispose();
            box.remove();
            delete window.__frameEscape;
        };
    });
    await expect.poll(() => page.evaluate(() => window.__frameEscape.escape)).toBe(true);
    await page.evaluate(() => window.__frameEscape.answer());
    await expect.poll(() => page.evaluate(() => window.__frameEscape.down)).toBe(1);
});

for (const ending of ['blur', 'restored', 'native change', 'held Enter then blur', 'dispose', 'replace detached field']) {
    test(`ADR-0210: a held text edit commits once and releases its supplement (${ending})`, async ({ page }) => {
        await page.goto('/features');
        await expect(page.locator('.ex-grid').first()).not.toHaveAttribute('aria-busy', /.*/);
        await alterPage(page, async (ending) => {
            const module = await import('/_content/ExGrid/ex-grid.min.js');
            const box = document.createElement('div');
            const root = document.createElement('div');
            root.className = 'ex-grid';
            root.tabIndex = 0;
            const scroller = document.createElement('div');
            root.append(scroller);
            const popup = document.createElement('div');
            const field = document.createElement('input');
            field.id = 'held-text-commit-field';
            field.value = '#123456';
            const outside = document.createElement('button');
            popup.append(field);
            box.append(root, popup, outside);
            document.body.append(box);
            const state = window.__heldBlurCommit = { changes: [], value: () => field.value };
            field.addEventListener('change', () => state.changes.push(field.value));
            const handle = module.attach(root, scroller, { invokeMethodAsync: () => Promise.resolve() }, [], false, 150, false, [], false, true);
            let disposed = false;
            const send = (keys, destination = popup) => {
                root.focus();
                handle.handOff('frame');
                for (const key of keys) root.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
                handle.handKeyboardTo(destination);
            };
            state.leave = () => outside.focus();
            state.finish = (ending) => {
                if (ending === 'restored') field.value = '#123456';
                if (ending === 'dispose') { handle.dispose(); disposed = true; }
                if (ending === 'held Enter then blur') {
                    return;
                }
                if (ending === 'replace detached field') {
                    // Model removal on a browser that does not deliver blur for a detached
                    // field; Chromium can deliver it, so suppress that one notification.
                    field.addEventListener('blur', event => event.stopImmediatePropagation(), { capture: true, once: true });
                    field.remove();
                    const next = document.createElement('input');
                    next.value = 'old';
                    popup.append(next);
                    next.addEventListener('change', () => state.changes.push(next.value));
                    state.nextValue = () => next.value;
                    state.blurDetached = () => field.dispatchEvent(new FocusEvent('blur'));
                    send(['B']);
                    return;
                }
                outside.focus();
            };
            send([... '#FF0000', ...(ending === 'held Enter then blur' ? ['Enter'] : [])]);
            return () => {
                if (!disposed) handle.dispose();
                box.remove();
                delete window.__heldBlurCommit;
            };
        }, ending);
        await expect.poll(() => page.evaluate(() => window.__heldBlurCommit.value())).toBe('#FF0000');
        if (ending === 'native change') {
            await page.locator('#held-text-commit-field').press('End');
            await page.keyboard.type('1');
        }
        await page.evaluate(ending => window.__heldBlurCommit.finish(ending), ending);
        if (ending === 'held Enter then blur') {
            await expect.poll(() => page.evaluate(() => window.__heldBlurCommit.changes)).toEqual(['#FF0000']);
            await page.evaluate(() => window.__heldBlurCommit.leave());
        }
        if (ending === 'replace detached field') {
            await expect.poll(() => page.evaluate(() => window.__heldBlurCommit.nextValue())).toBe('B');
            await page.evaluate(() => window.__heldBlurCommit.blurDetached());
            expect(await page.evaluate(() => window.__heldBlurCommit.changes)).toEqual([]);
            await page.evaluate(() => window.__heldBlurCommit.leave());
        }
        const expected = ending === 'restored' || ending === 'dispose' ? []
            : ending === 'replace detached field' ? ['B']
            : ending === 'native change' ? ['#FF00001'] : ['#FF0000'];
        expect(await page.evaluate(() => window.__heldBlurCommit.changes)).toEqual(expected);
    });
}
