import { expectActiveDescendant } from './keyboard.mjs';

// A press on a cell that the grid did not hear, journalled so that its failure says what happened. Twice on CI's Edge
// (RT-1: the MudBlazor Sheet on WebAssembly, PR #497; the built-in Sheet on the Server host, PR #500) a press on the
// first cell left the active descendant empty for good, the Focus still on the cell pressed before and the keyboard on
// the page's body; twice on CI's Chrome on the Server host (KB-23, PR #500) the first press on /cells left the grid with
// no Focus at all. Over a hundred local runs, throttled and behind a round trip too, did not repeat either. So a
// failure says which round it was, what the grid held then, and what the root heard from the press on: each press,
// and where DOM focus went in and out (journalPress).
export async function journalPress(grid) {
    await grid.evaluate(root => {
        const name = element => (element instanceof Element
            ? `${element.tagName.toLowerCase()}.${[...element.classList].join('.')}` : String(element));
        const journal = [];
        root.exPressJournal = journal;
        const note = entry => { journal.push({ at: Math.round(performance.now()), ...entry }); };
        root.addEventListener('mousedown', event => {
            note({ heard: 'mousedown', target: name(event.target), x: event.clientX, y: event.clientY });
        }, true);
        // Heard last, on the way back up: whether the grid kept the press's own focusing.
        root.addEventListener('mousedown', event => {
            note({ heard: 'mousedown done', prevented: event.defaultPrevented, active: name(document.activeElement) });
        });
        root.addEventListener('focusin', event => { note({ heard: 'focusin', target: name(event.target) }); });
        root.addEventListener('focusout', event => {
            note({ heard: 'focusout', target: name(event.target), to: name(event.relatedTarget) });
        });
    });
}

export async function pressFirstCell(grid, cell, round) {
    const before = await grid.evaluate(root => {
        const name = element => (element === null ? null : `${element.tagName.toLowerCase()}.${[...element.classList].join('.')}`);
        root.exPressJournal.length = 0;
        const box = root.querySelector('[id$="-r0c0"]')?.getBoundingClientRect();
        return {
            active: name(document.activeElement),
            cell: box === undefined ? null : { left: box.left, top: box.top },
            at: box === undefined ? null : name(document.elementFromPoint(box.left + 8, box.top + 8)),
        };
    });
    await cell.click({ position: { x: 8, y: 8 }, force: true });
    try {
        await expectActiveDescendant(grid, /-r0c0$/);
    } catch (error) {
        const held = await grid.evaluate(root => {
            const cell = root.querySelector('[id$="-r0c0"]');
            const box = cell?.getBoundingClientRect();
            const at = box === undefined ? null : document.elementFromPoint(box.left + 8, box.top + 8);
            const name = element => (element === null ? null : `${element.tagName.toLowerCase()}.${[...element.classList].join('.')}`);
            return {
                focus: root.getAttribute('data-ex-follow-focus'),
                scrollLeft: root.querySelector('.ex-scroller').scrollLeft,
                settled: root.hasAttribute('data-ex-settled'),
                busy: root.getAttribute('aria-busy'),
                pinned: root.querySelector('.ex-row').querySelectorAll('.ex-pinned').length,
                cell: box === undefined ? null : { left: box.left, top: box.top },
                atPress: name(at),
                active: name(document.activeElement),
                heard: root.exPressJournal,
            };
        }).catch(reading => ({ unread: String(reading) }));
        throw new Error(`${error.message}\nIn round ${round}, before the press: ${JSON.stringify(before)}\n`
            + `The grid then held: ${JSON.stringify(held)}`, { cause: error });
    }
}
