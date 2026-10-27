import type { ReactNode } from 'react';
import type { SheetToolbarItemSlotContext } from '@ex-grid/sheet';
import { OwnListButton } from './OwnListButton';

/** The list open in the panel: the item it is for, and whether the toolbar's keys opened it, so it takes the keyboard. */
export interface OwnList {
    readonly context: SheetToolbarItemSlotContext;
    readonly fromKeys: boolean;
    /** Counts the openings, so each draws a list of its own. */
    readonly opening: number;
}

/**
 * A Chrome of your own that draws the toolbar's lists — Number Format, Borders, the colours — in a panel beside the
 * Sheet, outside it, and leaves every other item to the built-in Chrome. The panel hands each choice back through the
 * item's `choicesClosed`, which gives the keyboard back to the Sheet and makes the choice there.
 */
export class OwnListChrome {
    readonly #listeners = new Set<() => void>();
    #open: OwnList | undefined;
    #openings = 0;

    /** Each item of the toolbar: a menu or a split control is ours, every other the built-in Chrome's. One function for
     * the Chrome's lifetime, so the Sheet does not draw the items again on every render. */
    readonly toolbarItem = (context: SheetToolbarItemSlotContext): ReactNode =>
        context.kind === 'menu' || context.kind === 'split' ? <OwnListButton context={context} chrome={this} /> : null;

    /** The list open, or none. */
    readonly current = (): OwnList | undefined => this.#open;

    /** Hears the list opening and closing. */
    readonly subscribe = (listener: () => void): (() => void) => {
        this.#listeners.add(listener);
        return () => { this.#listeners.delete(listener); };
    };

    /** Opens `context`'s list in the panel. */
    openList(context: SheetToolbarItemSlotContext, fromKeys: boolean): void {
        this.#openings += 1;
        this.#open = { context, fromKeys, opening: this.#openings };
        this.#changed();
    }

    /** Closes the list, if `id`'s is the one open. */
    closeList(id: string): void {
        if (this.#open?.context.id !== id) return;
        this.#open = undefined;
        this.#changed();
    }

    #changed(): void {
        for (const listener of this.#listeners) listener();
    }
}
