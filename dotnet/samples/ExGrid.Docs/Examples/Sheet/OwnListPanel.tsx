import { useLayoutEffect, useRef, useState, useSyncExternalStore, type KeyboardEvent, type ReactElement } from 'react';
import type { OwnList, OwnListChrome } from './OwnListChrome';

// The open list, drawn beside the Sheet. Opened from the toolbar's keys it takes the keyboard on the Focus cell's
// choice; ↑ and ↓ move, Enter or Space chooses, Escape closes it, and a press on a choice chooses it. Each close is
// handed back through choicesClosed: the keyboard goes back to the Sheet, or to the KeyTips after Escape in a list a
// KeyTip opened, and the toolbar makes the choice.
export function OwnListPanel({ chrome }: { readonly chrome: OwnListChrome }): ReactElement {
    const open = useSyncExternalStore(chrome.subscribe, chrome.current);
    if (open === undefined) return <p>Open a list from the toolbar: its choices are drawn here.</p>;
    // A list of its own for each opening, so the keyboard's place starts again.
    return <OpenList key={open.opening} open={open} chrome={chrome} />;
}

function OpenList({ open, chrome }: { readonly open: OwnList; readonly chrome: OwnListChrome }): ReactElement {
    const { context, fromKeys } = open;
    const view = useSyncExternalStore(context.subscribe, context.view);
    const buttons = useRef<(HTMLButtonElement | null)[]>([]);
    const [at, setAt] = useState(() => Math.max(0, view.choices.findIndex(choice => choice.selected && choice.disabledReason === undefined)));
    useLayoutEffect(() => {
        if (fromKeys) buttons.current[at]?.focus();
    }, [fromKeys, at]);

    const close = (choice: number | undefined, escaped: boolean): void => {
        context.choicesClosed(choice, escaped);
        chrome.closeList(context.id);
    };

    const onKey = (event: KeyboardEvent): void => {
        event.preventDefault();
        switch (event.key) {
            case 'Escape': close(undefined, true); break;
            case 'Enter':
            case ' ': if (view.choices[at]?.disabledReason === undefined) close(at, false); break;
            case 'ArrowDown': setAt(Math.min(at + 1, view.choices.length - 1)); break;
            case 'ArrowUp': setAt(Math.max(at - 1, 0)); break;
            default: break;
        }
    };

    return (
        <div role="listbox" aria-label={view.name} onKeyDown={onKey} onMouseDown={event => { event.preventDefault(); }}
            style={{ display: 'flex', flexDirection: 'column', gap: 2, padding: 4, border: '1px solid currentColor', maxHeight: 300, overflow: 'auto' }}>
            {view.choices.map((choice, i) => (
                <button key={choice.label} type="button" role="option" tabIndex={-1} aria-selected={choice.selected}
                    disabled={choice.disabledReason !== undefined} title={choice.disabledReason}
                    ref={button => { buttons.current[i] = button; }} onClick={() => { close(i, false); }}
                    style={{ textAlign: 'left', borderLeft: choice.swatch === undefined ? undefined : `1em solid ${choice.swatch}` }}>
                    {choice.label}
                </button>
            ))}
        </div>
    );
}
