import { unreachable } from '../must';
import type { CycleOrder, GridDirection } from '../selection/cell';

/** What the core does with a key it has taken (ADR-0012, ADR-0011, ADR-0020). */
export type GridKeyKind =
    /** Not the core's: it passes through to the browser and to whatever is inside the cell. */
    | 'none'
    /** Arrow: collapse and move. */
    | 'move'
    /** Shift+arrow: the Focus stays and the Extent moves (ADR-0052). */
    | 'extend'
    /** Ctrl+arrow, Home, End: jump to the edge. */
    | 'moveToEdge'
    /** Ctrl+Shift+arrow, Shift+Home, Shift+End: extend to the edge. */
    | 'extendToEdge'
    /** Enter and Tab and their Shift forms: move the Focus inside the selection. */
    | 'cycle'
    /** Ctrl+A. */
    | 'selectAll'
    /** Ctrl+Space. */
    | 'selectWholeColumns'
    /** Shift+Space. */
    | 'selectWholeRows'
    /** Ctrl+Home and Ctrl+End: the first or last cell of the whole result. */
    | 'moveToCorner'
    /** PageUp and PageDown: collapse and move the Focus by the rows fully visible, the Viewport moving by
     * the same number (ADR-0012). */
    | 'moveByViewport'
    /** Shift+PageUp and Shift+PageDown: extend by the same rows. */
    | 'extendByViewport'
    /** Space: engage with the cell's content (ADR-0020). */
    | 'engage'
    /** Escape with nothing left to dismiss: the way out of Tab's cycle. The grid keeps DOM focus and
     * releases Tab, so the next Tab is the browser's (ADR-0012, rewritten 2026-10-01). */
    | 'leave'
    /** The Context Menu key and Shift+F10: the context menu on the Focus cell (ADR-0036). */
    | 'openContextMenu'
    /** Alt+↓: the column menu of the Focus's column, Excel's key for a header's drop-down (ADR-0039). */
    | 'openColumnMenu'
    /** Ctrl+Z: forwarded to the Consumer, who owns the history (ADR-0007); claimed only while someone
     * listens. */
    | 'undo'
    /** Ctrl+Y or Ctrl+Shift+Z: forwarded like undo. */
    | 'redo'
    /** Delete: one Clear Intent over the selection (ADR-0054); claimed only on a grid that edits. */
    | 'clear'
    /** Backspace: Overwrite on the Focus with nothing in it (ADR-0035); claimed only on a grid that edits. */
    | 'clearAndEdit'
    /** Ctrl+D: fill the range's top row down it (ADR-0035); claimed only on a grid that edits. */
    | 'fillDown'
    /** Ctrl+R: fill the range's left column across it (ADR-0035); claimed only on a grid that edits. */
    | 'fillRight'
    /** Ctrl+F: the find panel, or a refusal where nothing can search (ADR-0055); claimed on every grid. */
    | 'find'
    /** Ctrl+H: the find panel with Replace, or a refusal where no Replace can land (ADR-0332 §2). */
    | 'replace'
    /** Ctrl+. (period): the Focus to the next corner of its range, clockwise (ADR-0052). */
    | 'moveFocusToNextCorner'
    /** Ctrl+Backspace: scroll the Focus into view and change nothing else (ADR-0052). */
    | 'revealFocus'
    /** Shift+Backspace: collapse the Selection to the Focus (ADR-0052). */
    | 'collapseToFocus';

/** One taken key, resolved to what it means: a value, so the table is a constant. */
export interface GridKeyAction {
    /** What to do. */
    readonly kind: GridKeyKind;
    /** Which way, for the movement kinds; `down` where the kind has none. */
    readonly direction: GridDirection;
    /** Enter runs down columns, Tab across rows (ADR-0012); `columnMajor` where the kind has none. */
    readonly order: CycleOrder;
    /** Shift+Enter and Shift+Tab, and the corner Ctrl+Home takes. */
    readonly backward: boolean;
}

/** What one grid can do, as far as its key claims go (ADR-0007, ADR-0010, ADR-0054). */
export interface GridKeyClaims {
    /** Whether any column is Editable: Delete, Backspace, Ctrl+D and Ctrl+R are claimed only then. */
    readonly canEdit: boolean;
    /** Whether someone listens for undo: Ctrl+Z is claimed only then. */
    readonly canUndo: boolean;
    /** Whether someone listens for redo: Ctrl+Y and Ctrl+Shift+Z are claimed only then. */
    readonly canRedo: boolean;
    /** Whether anything can answer a Find (ADR-0055): Ctrl+F opens the find panel, or is refused. Ctrl+F is
     * claimed either way, so {@link keysTakenFor} does not read it; the grid does, when the key arrives. */
    readonly canFind?: boolean;
    /** Whether a Replace can land (ADR-0332 §2): Ctrl+H opens Replace, or is refused. Ctrl+H is claimed
     * either way, so {@link keysTakenFor} does not read it; the grid does, when the key arrives. */
    readonly canReplace?: boolean;
}

/** One key press, as the browser reports it. */
export interface KeyPress {
    /** The event's `key`, as the browser reported it. */
    readonly key: string;
    /** Whether the Control key was held. */
    readonly ctrl: boolean;
    /** Whether the Shift key was held. */
    readonly shift: boolean;
    /** Whether the Alt key was held. */
    readonly alt: boolean;
    /** Whether the Meta key was held: Command on an Apple keyboard, the OS's own key elsewhere. */
    readonly meta: boolean;
    /** Whether Meta is this platform's primary modifier: true on an Apple platform, where it is Command.
     * Only the browser can answer it. */
    readonly metaIsPrimary: boolean;
}

function action(kind: GridKeyKind, options: { readonly direction?: GridDirection; readonly order?: CycleOrder; readonly backward?: boolean } = {}): GridKeyAction {
    const { direction = 'down', order = 'columnMajor', backward = false } = options;
    return Object.freeze({ kind, direction, order, backward });
}

/** The key means nothing to the core: what every key outside the table resolves to. */
const none = action('none');

// The table, in canonical form (ADR-0010: the core arbitrates the keys, and Chrome is not trusted with it).
const table: ReadonlyMap<string, GridKeyAction> = (() => {
    const keys = new Map<string, GridKeyAction>();
    // Arrows: the four forms of ADR-0012's first table.
    const arrows: readonly (readonly [string, GridDirection])[] = [
        ['ArrowUp', 'up'], ['ArrowDown', 'down'], ['ArrowLeft', 'left'], ['ArrowRight', 'right'],
    ];
    for (const [key, direction] of arrows) {
        keys.set(key, action('move', { direction }));
        keys.set(`Shift+${key}`, action('extend', { direction }));
        keys.set(`Control+${key}`, action('moveToEdge', { direction }));
        keys.set(`Control+Shift+${key}`, action('extendToEdge', { direction }));
    }
    // Home and End, read as Excel's: the row's first and last cell, and with Control the result's corners.
    keys.set('Home', action('moveToEdge', { direction: 'left' }));
    keys.set('End', action('moveToEdge', { direction: 'right' }));
    keys.set('Shift+Home', action('extendToEdge', { direction: 'left' }));
    keys.set('Shift+End', action('extendToEdge', { direction: 'right' }));
    keys.set('Control+Home', action('moveToCorner', { backward: true }));
    keys.set('Control+End', action('moveToCorner'));
    // PageUp and PageDown move the Focus and the Viewport together (ADR-0012). Their Control forms are
    // the browser's, which switches tabs with them.
    keys.set('PageUp', action('moveByViewport', { direction: 'up' }));
    keys.set('PageDown', action('moveByViewport', { direction: 'down' }));
    keys.set('Shift+PageUp', action('extendByViewport', { direction: 'up' }));
    keys.set('Shift+PageDown', action('extendByViewport', { direction: 'down' }));
    // Enter runs down columns, Tab runs across rows; Shift runs both backwards.
    keys.set('Enter', action('cycle', { order: 'columnMajor' }));
    keys.set('Shift+Enter', action('cycle', { order: 'columnMajor', backward: true }));
    keys.set('Tab', action('cycle', { order: 'rowMajor' }));
    keys.set('Shift+Tab', action('cycle', { order: 'rowMajor', backward: true }));
    keys.set('Control+a', action('selectAll'));
    // With CapsLock on the browser reports an uppercase key and no Shift.
    keys.set('Control+A', action('selectAll'));
    keys.set('Control+ ', action('selectWholeColumns'));
    keys.set('Shift+ ', action('selectWholeRows'));
    keys.set(' ', action('engage'));
    // Excel's three keys for the active cell (ADR-0052).
    keys.set('Control+.', action('moveFocusToNextCorner'));
    keys.set('Control+Backspace', action('revealFocus'));
    keys.set('Shift+Backspace', action('collapseToFocus'));
    // The way out of Tab's cycle: Enter and Tab never leave the selection, which without an exit would
    // trap the keyboard against ADR-0020's "the grid is one tab stop".
    keys.set('Escape', action('leave'));
    // The context menu from the keyboard (ADR-0036), in both spellings the platforms have.
    keys.set('ContextMenu', action('openContextMenu'));
    keys.set('Shift+F10', action('openContextMenu'));
    // The column menu from the keyboard (ADR-0039).
    keys.set('Alt+ArrowDown', action('openColumnMenu'));
    // Excel's editing keys (ADR-0007, ADR-0035, ADR-0054), each claimed only on the grids named in
    // `conditions`. Both cases of every letter, for CapsLock; Ctrl+Shift+Z with CapsLock reports `z`.
    for (const z of ['z', 'Z']) {
        keys.set(`Control+${z}`, action('undo'));
        keys.set(`Control+Shift+${z}`, action('redo'));
    }
    keys.set('Control+y', action('redo'));
    keys.set('Control+Y', action('redo'));
    keys.set('Delete', action('clear'));
    keys.set('Backspace', action('clearAndEdit'));
    keys.set('Control+d', action('fillDown'));
    keys.set('Control+D', action('fillDown'));
    keys.set('Control+r', action('fillRight'));
    keys.set('Control+R', action('fillRight'));
    // Find on every grid (ADR-0055): the browser's own find sees only painted rows.
    keys.set('Control+f', action('find'));
    keys.set('Control+F', action('find'));
    // Replace on every grid (ADR-0332 §2): the browser's Ctrl+H is its history.
    keys.set('Control+h', action('replace'));
    keys.set('Control+H', action('replace'));
    return keys;
})();

// The kinds claimed only on some grids, and what decides it; every other kind is claimed on every grid.
function claimedOn(kind: GridKeyKind, claims: GridKeyClaims): boolean {
    switch (kind) {
        case 'undo': return claims.canUndo;
        case 'redo': return claims.canRedo;
        case 'clear':
        case 'clearAndEdit':
        case 'fillDown':
        case 'fillRight': return claims.canEdit;
        case 'none':
        case 'move':
        case 'extend':
        case 'moveToEdge':
        case 'extendToEdge':
        case 'cycle':
        case 'selectAll':
        case 'selectWholeColumns':
        case 'selectWholeRows':
        case 'moveToCorner':
        case 'moveByViewport':
        case 'extendByViewport':
        case 'engage':
        case 'leave':
        case 'openContextMenu':
        case 'openColumnMenu':
        case 'find':
        case 'replace':
        case 'moveFocusToNextCorner':
        case 'revealFocus':
        case 'collapseToFocus': return true;
        default: return unreachable(kind);
    }
}

const nobody: GridKeyClaims = Object.freeze({ canEdit: false, canUndo: false, canRedo: false });

/**
 * The keys a grid takes for what they mean to it (ADR-0010, ADR-0007, ADR-0035, ADR-0054): the table's,
 * less the writing keys on a grid with no editable column and undo and redo where nobody listens. A key
 * taken for nobody is a key stolen from the page. Not here, deliberately: Ctrl+C and Ctrl+V, whose
 * browser events are the clipboard's route (ADR-0005), and F2 and typing, which open the Cell Editor.
 */
export function keysTakenFor(claims: GridKeyClaims): readonly string[] {
    return Object.freeze([...table].filter(([, taken]) => claimedOn(taken.kind, claims)).map(([key]) => key));
}

/** The keys taken on every grid, whatever it can do. */
export const takenOnEveryGrid: readonly string[] = keysTakenFor(nobody);

/**
 * The canonical form of one key press, `[Control+][Meta+][Shift+][Alt+]{key}` (ADR-0012). Control is the
 * primary modifier: always the Control key, and the Meta key too only where it is Command. Elsewhere Meta
 * is the OS's and stays in the form, so a chord aimed at the window manager matches nothing of the grid's.
 */
export function canonicalKey(press: KeyPress): string {
    const control = press.ctrl || (press.meta && press.metaIsPrimary);
    const foreign = press.meta && !press.metaIsPrimary;
    return (control ? 'Control+' : '') + (foreign ? 'Meta+' : '') + (press.shift ? 'Shift+' : '') + (press.alt ? 'Alt+' : '')
        + press.key;
}

/** What a key press means. A key outside the table means nothing, including the `Unidentified`, dead and
 * IME keys a browser is free to send. */
export function resolveKey(press: KeyPress): GridKeyAction {
    const known = table.get(canonicalKey(press));
    return known === undefined ? none : known;
}

/**
 * The error step's keys (ADR-0381 section 3, decided with the user 2026-10-26): F8 to the next error cell and
 * Shift+F8 to the previous, the keys VS Code and Visual Studio step through problems with. Outside the table: they
 * are the core's only on a grid given `findError`, and a grid without it leaves them to the page or to a Consumer
 * that declares them. `undefined` for any other key.
 */
export function errorStepOf(press: KeyPress): 'next' | 'previous' | undefined {
    const canonical = canonicalKey(press);
    return canonical === 'F8' ? 'next' : canonical === 'Shift+F8' ? 'previous' : undefined;
}

/**
 * The Header Group keys (ADR-0400 section 4, decided with the user 2026-10-27): Alt+Shift+↓ expands and Alt+Shift+↑
 * collapses, Google Sheets' keys, since Excel's own route is ribbon key tips a browser takes for its menu. Outside the
 * table: they are the core's only on a grid given `onHeaderGroupToggled`, and a grid without it leaves them to the page
 * or to a Consumer that declares them.
 */
export const headerGroupKeys: readonly string[] = Object.freeze(['Shift+Alt+ArrowDown', 'Shift+Alt+ArrowUp']);

/** What a key asks of a Header Group (ADR-0400 section 4): `'expand'`, `'collapse'`, or `undefined` for any other key. */
export function headerGroupKeyOf(press: KeyPress): 'expand' | 'collapse' | undefined {
    const canonical = canonicalKey(press);
    return canonical === 'Shift+Alt+ArrowDown' ? 'expand' : canonical === 'Shift+Alt+ArrowUp' ? 'collapse' : undefined;
}

/**
 * Copy with headers' key (ADR-0005, decided with the user 2026-10-28): Ctrl+Shift+C, Command+Shift+C on a Mac, the key
 * SQL Server Management Studio's results grid copies with headers by. Both cases, for CapsLock. Outside the table: it is
 * the core's only on a grid that offers Copy with headers, and a grid that hides it leaves the key to the page, whose
 * browser opens its element inspector by it, or to a Consumer that declares it. Taken, it fires no copy event, so it
 * writes as the Context Menu's item does.
 */
export const copyWithHeadersKeys: readonly string[] = Object.freeze(['Control+Shift+C', 'Control+Shift+c']);

/** Whether a key press is Copy with headers' key (ADR-0005). */
export function copiesWithHeaders(press: KeyPress): boolean {
    return copyWithHeadersKeys.includes(canonicalKey(press));
}

/** The clipboard's keys (ADR-0005, ADR-0014): answered through the browser's copy and paste events, with
 * cut and paste as plain text. Never taken, or the events would not fire. */
export const clipboardKeys: readonly string[] = Object.freeze([
    'Control+c', 'Control+C', 'Control+Insert',
    'Control+x', 'Control+X', 'Shift+Delete',
    'Control+v', 'Control+V', 'Shift+Insert', 'Control+Shift+v', 'Control+Shift+V',
]);

// The keys the core answers outside the table: two the editor claims while an edit is open, and the
// clipboard's, whose events a taken key would suppress (ADR-0005).
const answeredOutsideTable: ReadonlyMap<string, string> = new Map([
    ['Control+Enter', 'while an edit is open it fills the selection (ADR-0007)'],
    ['F4', 'while an edit is open it cycles the Reference at the caret, where CycleReference is declared (ADR-0051)'],
    ['Control+c', 'the browser\'s copy event, which the core answers (ADR-0005)'],
    ['Control+C', 'the browser\'s copy event, which the core answers (ADR-0005)'],
    ['Control+Insert', 'the browser\'s copy event, which the core answers (ADR-0005)'],
    ['Control+v', 'the browser\'s paste event, which the core answers (ADR-0014)'],
    ['Control+V', 'the browser\'s paste event, which the core answers (ADR-0014)'],
    ['Shift+Insert', 'the browser\'s paste event, which the core answers (ADR-0014)'],
]);

interface Parsed {
    readonly control: boolean;
    readonly meta: boolean;
    readonly alt: boolean;
    readonly key: string;
}

/** A declared key taken apart, or `undefined` when it is not in the canonical form: the modifiers in
 * order, each at most once, then a key that is not a modifier and holds no `+` unless it is `+`. */
function parseCanonical(declared: string): Parsed | undefined {
    let rest = declared;
    const take = (prefix: string): boolean => {
        if (!rest.startsWith(prefix) || rest.length === prefix.length) {
            return false;
        }
        rest = rest.slice(prefix.length);
        return true;
    };
    const control = take('Control+');
    const meta = take('Meta+');
    const shift = take('Shift+');
    const alt = take('Alt+');
    // The Alt key's own keydown, nothing else held, is the one modifier a Consumer may declare, as a
    // press of it canonicalises (ADR-0100; ADR-0050 item 14, note of 2026-10-02).
    if (rest === 'Alt' && alt && !control && !meta && !shift) {
        return { control, meta, alt, key: rest };
    }
    const named = rest.length > 0 && (rest === '+' || !rest.includes('+'))
        && rest !== 'Control' && rest !== 'Meta' && rest !== 'Shift' && rest !== 'Alt';
    return named ? { control, meta, alt, key: rest } : undefined;
}

/** What the core does with a declared key, in words, or `undefined` when it leaves the key alone. */
function coreAnswer(canonical: string, parsed: Parsed): string | undefined {
    const taken = table.get(canonical);
    if (taken !== undefined) {
        // In words, selectAll as "select all", as the C# core writes its SelectAll.
        return `the core's ${taken.kind.replaceAll(/[A-Z]/g, letter => ` ${letter.toLowerCase()}`)}`;
    }
    const outside = answeredOutsideTable.get(canonical);
    if (outside !== undefined) {
        return outside;
    }
    // The typing rules (ADR-0010): a character or F2 (one UTF-16 unit, as the C# core counts it) with nothing but Shift opens the Cell Editor on a grid
    // that edits, and Control with Alt together is AltGr typing one.
    if (!parsed.control && !parsed.meta && !parsed.alt && (parsed.key.length === 1 || parsed.key === 'F2')) {
        return 'typing, which opens the Cell Editor (ADR-0010)';
    }
    if (parsed.control && parsed.alt && parsed.key.length === 1) {
        return 'typing with AltGr, which opens the Cell Editor (ADR-0010)';
    }
    return undefined;
}

const noneDeclared: ReadonlySet<string> = new Set();

/**
 * A Consumer's declared keys, checked (ADR-0050, item 14): each in {@link canonicalKey}'s form, and none
 * a key the core answers itself, or the key would mean two things (ADR-0010). A key is matched exactly as
 * declared: a letter claims the case it names, so a key wanted under CapsLock too is declared in both.
 * Throws a TypeError naming the first key refused.
 */
export function declareKeys(keys?: Iterable<string>): ReadonlySet<string> {
    if (keys === undefined) {
        return noneDeclared;
    }
    const declared = new Set<string>();
    for (const key of keys) {
        // Typed as text, and from an untyped caller anything.
        const given: unknown = key;
        const parsed = typeof given === 'string' ? parseCanonical(given) : undefined;
        if (parsed === undefined) {
            throw new TypeError(`The declared key '${String(given)}' is not in the canonical form [Control+][Meta+][Shift+][Alt+]{key}, `
                + 'with the key as KeyboardEvent.key reports it, so no key press would ever match it (ADR-0050, item 14).');
        }
        const answer = coreAnswer(key, parsed);
        if (answer !== undefined) {
            throw new TypeError(`The declared key '${key}' is one the core answers itself: ${answer}. A declared key never takes a `
                + 'key the core answers, or the key would mean two things (ADR-0050, item 14; ADR-0010).');
        }
        declared.add(key);
    }
    return declared.size === 0 ? noneDeclared : declared;
}
