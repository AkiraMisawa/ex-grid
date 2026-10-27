import { useEffect, useRef, useSyncExternalStore, type ReactElement } from 'react';
import type { SheetToolbarItemSlotContext } from '@ex-grid/sheet';
import type { OwnListChrome } from './OwnListChrome';

// One list item on the toolbar: a button that opens its list in the panel. A pointer press leaves the keyboard on the
// Sheet, as the built-in items do.
export function OwnListButton({ context, chrome }: { readonly context: SheetToolbarItemSlotContext; readonly chrome: OwnListChrome }): ReactElement {
    const view = useSyncExternalStore(context.subscribe, context.view);
    const open = useSyncExternalStore(chrome.subscribe, chrome.current);
    const answered = useRef({ open: view.openRequest, close: view.closeRequest });
    useEffect(() => {
        // The toolbar gave the keyboard back before the list had it: the list closes, telling nothing.
        if (view.closeRequest !== answered.current.close) {
            answered.current.close = view.closeRequest;
            chrome.closeList(context.id);
        }
        // The toolbar's keys asked for the list: it opens and takes the keyboard. One that cannot open says so.
        if (view.openRequest !== answered.current.open) {
            answered.current.open = view.openRequest;
            if (view.openRequest === view.closeRequest) return;
            if (view.disabledReason === undefined && view.choices.length > 0) chrome.openList(context, true);
            else context.choicesClosed(undefined, false);
        }
    }, [view, chrome, context]);
    const shown = open?.context.id === context.id;
    return (
        <button type="button" tabIndex={-1} disabled={view.disabledReason !== undefined} title={view.tooltip}
            aria-haspopup="listbox" aria-expanded={shown}
            onMouseDown={event => { event.preventDefault(); }}
            onClick={() => { if (shown) chrome.closeList(context.id); else chrome.openList(context, false); }}
            style={view.active ? { outline: '2px solid currentColor' } : undefined}>
            {view.text ?? view.name} ▾
        </button>
    );
}
