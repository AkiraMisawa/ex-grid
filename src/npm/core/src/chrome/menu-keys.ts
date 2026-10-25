import { elementAt } from '../must';
import { gridCommandIds } from './command-ids';

// The keys inside a menu, the column menu or the Context Menu, and what each one means (ADR-0039,
// ADR-0044). Once a menu holds DOM focus the grid's key listener takes Escape alone and leaves every other
// key to the item that has it (ADR-0012), so the keys are the contents' to implement. What they mean is
// decided here, once, so that the built-in Chrome and a substituted one answer the same key the same way
// and swapping Chrome changes nothing about behaviour (ADR-0010, FN-17).
//
//   ↑ / ↓            the previous / next enabled item, wrapping at the ends
//   Home / End       the first / last enabled item
//   Enter / Space    runs the item; the menu closes
//   Tab / Shift+Tab  closes the menu, as a Cancel; over a column's filter, moves to the filter's first /
//                    last control (ADR-0044)
//   S / O / C        in the column menu, runs sort ascending, sort descending or clear filter, when enabled:
//                    Excel's drop-down letters (ADR-0044)
//   E                in the column menu over a filter, moves to the field a search is typed into
//
// Escape is not here: the grid takes it, from anywhere under the root, and closes the menu as a Cancel
// (ADR-0012's layering). A key chorded with Control, Alt or Meta means nothing: a menu has no shortcuts of
// its own, and a browser's or the OS's chord must not be read as a move. The letters are fixed whatever
// language the labels are in, and the Context Menu has none: it is not Excel's drop-down.

/**
 * What a key pressed on a menu's item does (ADR-0039).
 *
 * - `none`: the key has no meaning in a menu.
 * - `move`: DOM focus moves to the item {@link MenuKey.item}.
 * - `run`: the item the key was pressed on runs, and the menu closes.
 * - `close`: the menu closes as a Cancel, and the keyboard goes back to the root.
 * - `filterFirst`, `filterLast`: the keyboard moves to the first (Tab) or last (Shift+Tab, wrapping
 *   backwards) control of the filter below the commands (ADR-0044). The core moves it; the menu's contents
 *   do nothing.
 * - `filterSearch`: the keyboard moves to the field a search is typed into (E, as Excel's search box): the
 *   value list's search, or the condition's value where no list stands (ADR-0044).
 */
export type MenuKeyKind = 'none' | 'move' | 'run' | 'close' | 'filterFirst' | 'filterLast' | 'filterSearch';

/** A key's meaning in a menu: what to do, and for a move or a run, which item. */
export interface MenuKey {
    /** What to do. */
    readonly kind: MenuKeyKind;
    /** The item moved to or run, as an index of the menu's commands; -1 for any other kind. */
    readonly item: number;
}

/** The part of a command the menu's keys read. */
export interface MenuItem {
    /** The command's id: one of {@link gridCommandIds}, or a Consumer's own. */
    readonly id: string;
    /** Whether it may run. A disabled item is never moved to and never runs. */
    readonly enabled: boolean;
}

/** The key means nothing in a menu. */
export const menuKeyNothing: MenuKey = Object.freeze({ kind: 'none', item: -1 });

function key(kind: MenuKeyKind, item = -1): MenuKey {
    return Object.freeze({ kind, item });
}

/**
 * What `pressed` (a `KeyboardEvent.key` value) means, pressed on the item at `current` of `commands`, in a
 * menu of commands alone: the Context Menu (ADR-0036).
 */
export function resolveMenuKey(
    pressed: string, shift: boolean, controlAltOrMeta: boolean, commands: readonly MenuItem[], current: number,
): MenuKey {
    if (controlAltOrMeta) return menuKeyNothing;
    if (pressed === 'Tab') return key('close');
    return common(pressed, shift, commands, current);
}

/**
 * What `pressed` means pressed on the item at `current` of a column menu's `commands` (ADR-0044): the keys
 * of any menu, and Excel's letters. `filterBelow` says whether the column's filter stands under the
 * commands in the same popover: Tab then moves into it rather than closing, and E reaches its search box.
 */
export function resolveColumnMenuKey(
    pressed: string, shift: boolean, controlAltOrMeta: boolean, commands: readonly MenuItem[], current: number,
    filterBelow: boolean,
): MenuKey {
    if (controlAltOrMeta) return menuKeyNothing;
    if (pressed === 'Tab') {
        if (!filterBelow) return key('close');
        return key(shift ? 'filterLast' : 'filterFirst');
    }
    const letter = menuLetter(pressed, controlAltOrMeta, commands, filterBelow);
    return letter.kind === 'none' ? common(pressed, shift, commands, current) : letter;
}

/**
 * Excel's letters in the column's drop-down (ADR-0044): S, O and C run sort ascending, sort descending and
 * clear filter when they are enabled; E moves to the field a search is typed into, when a filter stands
 * below. Either case, Shift or not: Caps Lock is not a different key. Answered for a key on a command and
 * for a key on the value list alike; in a text field a letter is text, and nothing asks this.
 */
export function menuLetter(pressed: string, controlAltOrMeta: boolean, commands: readonly MenuItem[], filterBelow: boolean): MenuKey {
    if (controlAltOrMeta || pressed.length !== 1) return menuKeyNothing;
    const letter = pressed.toUpperCase();
    if (letter === 'E') return filterBelow ? key('filterSearch') : menuKeyNothing;
    const at = commands.findIndex(command => letterOf(command.id) === letter);
    return at >= 0 && elementAt(commands, at).enabled ? key('run', at) : menuKeyNothing;
}

/** The letter a command of the column menu answers to (S, O or C), or `undefined` for a command Excel's
 * drop-down does not have (ADR-0044): hide, pin, unpin and Size to fit get none, since a borrowed letter
 * would mean something an Excel user does not expect. */
export function letterOf(commandId: string): 'S' | 'O' | 'C' | undefined {
    switch (commandId) {
        case gridCommandIds.sortAscending: return 'S';
        case gridCommandIds.sortDescending: return 'O';
        case gridCommandIds.clearFilter: return 'C';
        default: return undefined;
    }
}

/**
 * A label split around the letter it answers to, the way Excel shows it (ADR-0044): at the letter's first
 * place in the label, either case, where the label has it, and appended as "(S)" where it does not, so a
 * label in another script keeps its letter. The Chrome underlines the middle part.
 */
export function splitAtLetter(label: string, letter: string): readonly [before: string, letter: string, after: string] {
    if (letter.length !== 1) {
        throw new TypeError(`A menu letter is one character. (letter: '${letter}')`);
    }
    const wanted = letter.toUpperCase();
    // Character by character, not label.toUpperCase().indexOf: an upper case longer than its letter (ß)
    // would move the index.
    for (let at = 0; at < label.length; at++) {
        if (label.charAt(at).toUpperCase() === wanted) {
            return Object.freeze([label.slice(0, at), label.charAt(at), label.slice(at + 1)] as const);
        }
    }
    return Object.freeze([`${label}(`, letter, ')'] as const);
}

/** The first enabled item, where DOM focus goes when the menu opens, or -1 when none is. */
export function firstEnabled(commands: readonly MenuItem[]): number {
    return commands.findIndex(command => command.enabled);
}

/** The last enabled item, or -1 when none is. */
export function lastEnabled(commands: readonly MenuItem[]): number {
    return commands.findLastIndex(command => command.enabled);
}

function common(pressed: string, shift: boolean, commands: readonly MenuItem[], current: number): MenuKey {
    if (shift) return menuKeyNothing;
    switch (pressed) {
        case 'ArrowDown': return moveTo(next(commands, current, 1));
        case 'ArrowUp': return moveTo(next(commands, current, -1));
        case 'Home': return moveTo(firstEnabled(commands));
        case 'End': return moveTo(lastEnabled(commands));
        case 'Enter':
        case ' ':
            return current >= 0 && current < commands.length && elementAt(commands, current).enabled ? key('run', current) : menuKeyNothing;
        default: return menuKeyNothing;
    }
}

function moveTo(item: number): MenuKey {
    return item < 0 ? menuKeyNothing : key('move', item);
}

// The next enabled item in the direction, wrapping: from the one that has focus, or, should that somehow be
// out of range, from the end the direction starts at.
function next(commands: readonly MenuItem[], current: number, direction: 1 | -1): number {
    const count = commands.length;
    if (count === 0) return -1;
    let start = current;
    if (current < 0 || current >= count) start = direction > 0 ? -1 : count;
    for (let step = 1; step <= count; step++) {
        const candidate = (((start + direction * step) % count) + count) % count;
        if (elementAt(commands, candidate).enabled) return candidate;
    }
    return -1;
}
