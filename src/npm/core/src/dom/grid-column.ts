// A column as the Consumer declares it to a grid (ADR-0350 section 16): a plain object, read once per
// `update`, and resolved here into what the paint reads. The query semantics read the same object, so
// a column has one shape for sorting, filtering and painting.

import type { EditVerdict } from '../cells/edit-intent';
import type { CellAlign, CellTone } from '../paint/cell-classes';
import { ColumnWidth, ColumnWidthSpec } from '../columns/column-width';
import { hashesWhenOverflowing } from '../columns/overflow-rules';
import type { QueryColumn } from '../query/columns';
import type { FilterUi } from './filter-panel';

/** An Auto width with its bounds (ADR-0016): computed from what is painted, between `minPx` and `maxPx`. */
export interface AutoColumnWidth {
    /** Says the width is computed from content. */
    readonly auto: true;
    /** The narrowest the column stands, and the narrowest a drag makes it; 40 unless given. */
    readonly minPx?: number;
    /** The widest the grid computes it, past which a number shows `####`; 400 unless given. */
    readonly maxPx?: number;
}

/** A Fixed width with its bounds (ADR-0016): `px` wide, and dragged no narrower than `minPx`. A MaxWidth
 * bounds only what the grid computes, Size to fit among it, so a width dragged past it stands as it came. */
export interface FixedColumnWidth {
    /** The width in CSS pixels; at least `minPx`. */
    readonly px: number;
    /** The narrowest a drag makes the column; 40 unless given. */
    readonly minPx?: number;
    /** The widest Size to fit makes it; 400 unless given. */
    readonly maxPx?: number;
}

/** A column's declared width: a Fixed number of pixels, a Fixed width with its bounds, or Auto (ADR-0016). */
export type GridColumnWidth = number | FixedColumnWidth | AutoColumnWidth;

/**
 * One action an Action Column's cells carry (ADR-0020). The Consumer gives what it looks like, a label or a
 * class naming an icon, and the core holds what pressing it means: the grid reports the press and does
 * nothing else. How many actions a cell carries is declared, never counted from what is painted, because
 * Space means something else at one action and at several.
 */
export interface GridAction {
    /** How the Consumer tells this action from the others when a press is reported back. */
    readonly name: string;
    /** The button's text, and its accessible name where `className` paints an icon instead. Required: an
     * icon-only control without a name is unreachable by anything but a mouse. */
    readonly label: string;
    /** An extra class on the button, for a theme's icon. Appearance only: with it the label is the button's
     * `aria-label` and title, and the button shows no text. */
    readonly className?: string;
}

/** An action pressed (ADR-0020): by pointer on its button, or by Space on its cell. The grid reports it and
 * does nothing else, not even move the keyboard (ADR-0037, amended 2026-09-26): what it opens is the
 * Consumer's. */
export interface GridActionEvent<Row> {
    /** The row it was pressed on, as the Window holds that row now (ADR-0142). */
    readonly row: Row;
    /** The Action Column's `name`. */
    readonly column: string;
    /** The action's `name`. */
    readonly action: string;
}

/**
 * What a Template cell's content is handed when it is mounted (ADR-0020, ADR-0037): the row it paints, its
 * column, and the core's request for the keyboard.
 */
export interface GridTemplateCell<Row> {
    /** The row the cell is painted for, as the Window held it when the cell was mounted. A control that writes
     * checks it against the row now before writing: the grid does not judge what such a control writes
     * (ADR-0142). */
    readonly row: Row;
    /** The column's `name`. */
    readonly column: string;
    /**
     * Non-zero on exactly one mount of exactly one cell: Space on a Template cell asks its content to take the
     * keyboard, and the content's control answers through `takeKeyboard`, acting once per number (ADR-0037).
     * Every other mount, and every later one of that cell, is handed zero, so a row recycled by
     * virtualisation never steals the keyboard.
     */
    readonly focusRequest: number;
    /**
     * Hands the keyboard to `control`, granted only while it is still the grid's: DOM focus inside its root
     * or on nothing (ADR-0204). A control that is not itself focusable names its first enabled text field,
     * with that field's text selected, or else its first enabled control. Moves neither the Focus nor the
     * Selection. A control that wants the keyboard carries `tabindex="-1"`, or the grid stops being one tab
     * stop, and `ex-interactive`, which turns its pointer events on (ADR-0020).
     */
    readonly takeKeyboard: (control: HTMLElement) => void;
}

/**
 * A Template Column's content (ADR-0020, ADR-0350 section 16): mounts what the Consumer paints into `slot`, the
 * cell's own element, empty, and answers how to take it away. The core mounts a cell's content once it is in
 * the DOM, keeps the slot while the cell stays painted, its row rebuilt or not, and mounts it again, into the
 * same slot, when a focus request is handed over or a new version of its row arrives: a Framework Adapter keyed
 * by the slot keeps its component's state across that. A new version is the same row only under a Row Key;
 * without one a new instance is another row (ADR-0140), whose cell is a new slot. The core writes the slot's own attributes, and nothing
 * inside it. Hold it on the column, never made per render: a new function is a new column, and every row
 * paints again (ADR-0003).
 */
export type GridTemplate<Row> = (slot: HTMLElement, cell: GridTemplateCell<Row>) => () => void;

/** A column of a grid: what the query semantics read, and what the grid paints. */
export interface GridColumn<Row> extends QueryColumn<Row> {
    /** The header's text; the column's name unless given. */
    readonly header?: string;
    /** The declared width; Auto between the default bounds unless given. */
    readonly width?: GridColumnWidth;
    /** How the column's cells align; by their type unless given (ADR-0006). */
    readonly align?: CellAlign;
    /** How the column's header aligns; as its cells unless given. A Sheet centres its column letters over cells
     * that align each by its own kind (ADR-0050, item 7). */
    readonly headerAlign?: CellAlign;
    /** Whether typing and a paste may write this column's cells (ADR-0007, ADR-0035). Off unless given: this
     * is a display-first grid, and a cell written where nobody hears the intent would be written into
     * nothing, so a grid with no `onEdit` opens no Cell Editor, and one with no `onPaste` takes a paste on no
     * column. The grid never holds the value either way: a write leaves as an intent. */
    readonly editable?: boolean;
    /** The column's tone rule (ADR-0006): what a value means, a gain or a loss, for the theme to paint. The
     * Consumer declares when; the theme's Visual Tokens say what colour, and the bare grid paints none. A null or
     * missing value has no tone and the rule is not asked. It is asked once per painted cell, so it stays a
     * comparison. */
    readonly tone?: (value: unknown) => CellTone;
    /** The Consumer's verdict on a commit to this column, asked as it commits (ADR-0034): accept, flag or
     * reject. None means every commit is accepted. It runs on the commit's path, so it is cheap. */
    readonly validate?: (row: Row, text: string) => EditVerdict;
    /** What the column's filter panel lays out (ADR-0009): `condition` unless given, the safe default, which
     * never asks the source for the column's values; `valueList`, a list of its distinct values to tick,
     * degraded to the condition form where the source answers TooMany; or `both`. */
    readonly filterUi?: FilterUi;
    /** The actions its cells carry, in order, painted as the grid's own buttons (ADR-0020). Made by
     * {@link actionColumn}, which declares the column's other half: no value, so no sort, filter or copy. */
    readonly actions?: readonly GridAction[];
    /** What its cells paint in place of their value's text (ADR-0020). The value is still read for sorting,
     * filtering, Find and copy, never the template; and `####` is not the template's business. */
    readonly template?: GridTemplate<Row>;
    /** Whether this is the grid's Mark Column (ADR-0043), declared through {@link markColumn}: a checkbox on
     * every Detail row, and one in the header. At most one per grid. */
    readonly mark?: boolean;
}

/** A column with what its declaration resolves to, checked once, where the Consumer handed it over. */
export interface ResolvedColumn<Row> {
    /** The column as declared. */
    readonly column: GridColumn<Row>;
    /** The header's text. */
    readonly header: string;
    /** The width and its bounds. */
    readonly spec: ColumnWidthSpec;
    /** Whether its cells are a numeric presentation, `####` when too narrow (ADR-0016). */
    readonly numeric: boolean;
    /** Whether a paste may write its cells (ADR-0035). */
    readonly editable: boolean;
    /** What its filter panel lays out (ADR-0009). */
    readonly filterUi: FilterUi;
    /** The actions its cells carry; none for every column but an Action Column (ADR-0020). */
    readonly actions: readonly GridAction[];
    /** What its cells paint, for a Template Column (ADR-0020). */
    readonly template: GridTemplate<Row> | undefined;
    /** Whether it is the grid's Mark Column (ADR-0043). */
    readonly mark: boolean;
    /** Whether its cells paint their value as text: false for an Action, Template or Mark Column, which takes
     * them out of the overflow decision, the Auto width's observation and Size to fit (ADR-0016). */
    readonly paintsValue: boolean;
}

const aligns: ReadonlySet<string> = new Set<CellAlign>(['auto', 'left', 'center', 'right']);
const filterUis: ReadonlySet<string> = new Set<FilterUi>(['condition', 'valueList', 'both']);

/** Resolves the declared columns, refusing by name what no grid could paint honestly. */
export function resolveColumns<Row>(columns: readonly GridColumn<Row>[]): readonly ResolvedColumn<Row>[] {
    const given: unknown = columns;
    if (!Array.isArray(given)) throw new TypeError('The columns are an array of columns, in display order.');
    const names = new Set<string>();
    let marking: string | undefined;
    return Object.freeze(columns.map((column, index) => {
        // Checked as an untyped caller may hand it, where it arrives (CONVENTIONS.md).
        const { name, value, align, headerAlign, header, type, editable, validate, filterUi, actions, template, queryable, mark, tone }: {
            readonly name: unknown; readonly value: unknown; readonly align?: unknown; readonly headerAlign?: unknown; readonly header?: unknown; readonly type: unknown;
            readonly editable?: unknown; readonly validate?: unknown; readonly filterUi?: unknown; readonly actions?: unknown;
            readonly template?: unknown; readonly queryable?: unknown; readonly mark?: unknown; readonly tone?: unknown;
        } = column;
        if (typeof name !== 'string' || name === '') {
            throw new TypeError(`The column at ${index} has no name: a column is named by a non-empty string.`);
        }
        if (names.has(name)) {
            throw new TypeError(`Two columns are named '${name}': a Sort, a Filter and the grid's state name a column by it.`);
        }
        names.add(name);
        if (typeof value !== 'function') {
            throw new TypeError(`Column '${name}' has no value: a column reads its value with a function of the row.`);
        }
        if (type !== 'text' && type !== 'number' && type !== 'date' && type !== 'boolean') {
            throw new TypeError(`Column '${name}': a type is text, number, date or boolean, not ${String(type)} (ADR-0002).`);
        }
        if (align !== undefined && (typeof align !== 'string' || !aligns.has(align))) {
            throw new RangeError(`Column '${name}': align is one of ${[...aligns].join(', ')}, not ${JSON.stringify(align)}.`);
        }
        if (headerAlign !== undefined && (typeof headerAlign !== 'string' || !aligns.has(headerAlign))) {
            throw new RangeError(`Column '${name}': headerAlign is one of ${[...aligns].join(', ')}, not ${JSON.stringify(headerAlign)}.`);
        }
        if (header !== undefined && typeof header !== 'string') {
            throw new TypeError(`Column '${name}': a header is a string.`);
        }
        if (editable !== undefined && typeof editable !== 'boolean') {
            throw new TypeError(`Column '${name}': editable is true or false.`);
        }
        if (validate !== undefined && typeof validate !== 'function') {
            throw new TypeError(`Column '${name}': validate is a function of the row and the text (ADR-0034).`);
        }
        if (tone !== undefined && typeof tone !== 'function') {
            throw new TypeError(`Column '${name}': tone is a function of the value, answering what it means (ADR-0006).`);
        }
        if (filterUi !== undefined && (typeof filterUi !== 'string' || !filterUis.has(filterUi))) {
            throw new RangeError(`Column '${name}': filterUi is one of ${[...filterUis].join(', ')}, not ${JSON.stringify(filterUi)} (ADR-0009).`);
        }
        if (template !== undefined && typeof template !== 'function') {
            throw new TypeError(`Column '${name}': a template is a function of the slot and the cell, answering how to take its content away (ADR-0020).`);
        }
        const declaredActions = actions === undefined ? [] : checkedActions(name, actions);
        if (actions !== undefined && (queryable !== false || template !== undefined)) {
            throw new TypeError(`Column '${name}' carries actions and ${template === undefined ? 'a value to sort and filter by' : 'a template'}: `
                + 'an Action Column carries no value and paints only its buttons, so it is made by actionColumn (ADR-0020).');
        }
        if (mark !== undefined && typeof mark !== 'boolean') {
            throw new TypeError(`Column '${name}': mark is true or false (ADR-0043).`);
        }
        if (mark === true && (queryable !== false || actions !== undefined || template !== undefined || editable === true)) {
            throw new TypeError(`Column '${name}' is a Mark Column and also ${actions !== undefined ? 'carries actions' : template !== undefined
                ? 'paints a template' : editable === true ? 'edits' : 'carries a value to sort and filter by'}: a Mark Column paints only its `
                + 'checkboxes, and a Row Mark is not the row\'s data, so it is made by markColumn (ADR-0043).');
        }
        if (mark === true) {
            if (marking !== undefined) {
                throw new TypeError(`Columns '${marking}' and '${name}' are both Mark Columns. A grid has at most one: the header's "all", `
                    + 'its state and the count would not say which marks they meant (ADR-0043).');
            }
            marking = name;
        }
        if (editable === true && (actions !== undefined || template !== undefined)) {
            throw new TypeError(`Column '${name}' is editable and paints ${actions === undefined ? 'a template' : 'actions'}: the Cell Editor `
                + 'opens over a value painted as text, and these cells paint none (ADR-0020).');
        }
        return Object.freeze({
            column,
            header: header ?? name,
            spec: specOf(column),
            numeric: hashesWhenOverflowing(type),
            editable: editable === true,
            filterUi: column.filterUi ?? 'condition', // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0009: the condition form is the safe default, which never enumerates
            actions: declaredActions,
            template: column.template,
            mark: mark === true,
            paintsValue: actions === undefined && template === undefined && mark !== true,
        });
    }));
}

/**
 * An Action Column (ADR-0020): cells that carry `actions` and no value, painted as the grid's own buttons. It
 * is unsortable and unfilterable, and copies as an empty cell, rather than order every row by nothing. Its
 * header is empty unless given, and an Auto width is measured from the labels.
 */
export function actionColumn<Row>(name: string, actions: readonly GridAction[],
    options: { readonly header?: string; readonly width?: GridColumnWidth } = {}): GridColumn<Row> {
    const { header = '', width } = options;
    const checked = checkedActions(name, actions);
    return Object.freeze({
        name, type: 'text', value: () => undefined, queryable: false, header, actions: checked,
        ...(width === undefined ? {} : { width }),
    });
}

/**
 * The Mark Column (ADR-0043): a checkbox on every Detail row, and one in the header that marks every row of the
 * current result. The marks are the Consumer's: the grid reports each gesture and asks each painted row whether
 * it is marked, so a grid declaring this column needs someone holding them, its source's marks or the `marks`
 * option. At most one per grid. Not a boolean field shown as a checkbox: a Row Mark is not the row's data and is
 * never written to it. Like an Action Column it has no value, so it is unsortable, unfilterable, and copies as
 * an empty cell. Named `Mark` and headed by nothing unless given.
 */
export function markColumn<Row>(options: { readonly name?: string; readonly header?: string; readonly width?: GridColumnWidth } = {}): GridColumn<Row> {
    const { name = 'Mark', header = '', width } = options;
    return Object.freeze({
        name, type: 'text', value: () => undefined, queryable: false, header, mark: true, align: 'center',
        ...(width === undefined ? {} : { width }),
    });
}

// The actions a column declares, checked as an untyped caller may hand them (CONVENTIONS.md), and frozen.
function checkedActions(column: unknown, actions: unknown): readonly GridAction[] {
    if (!Array.isArray(actions) || actions.length === 0) {
        throw new TypeError(`Action Column '${String(column)}' declares no actions: it is a non-empty array of { name, label } (ADR-0020).`);
    }
    const names = new Set<string>();
    return Object.freeze((actions as readonly unknown[]).map(action => {
        if (typeof action !== 'object' || action === null) {
            throw new TypeError(`Action Column '${String(column)}' declares an action that is not { name, label } (ADR-0020).`);
        }
        const { name, label, className } = action as { readonly name?: unknown; readonly label?: unknown; readonly className?: unknown };
        if (typeof name !== 'string' || name === '') {
            throw new TypeError(`Action Column '${String(column)}' declares an action with no name: the name is how a press is reported back.`);
        }
        if (names.has(name)) {
            throw new TypeError(`Action Column '${String(column)}' declares two actions named '${name}': the name is how one is told from another when it is reported back.`);
        }
        names.add(name);
        if (typeof label !== 'string' || label === '') {
            throw new TypeError(`Action '${name}' of column '${String(column)}' has no label: it is the button's accessible name, even where an icon paints it.`);
        }
        if (className !== undefined && typeof className !== 'string') {
            throw new TypeError(`Action '${name}' of column '${String(column)}': className is a string.`);
        }
        return Object.freeze(className === undefined ? { name, label } : { name, label, className });
    }));
}

function specOf<Row>(column: GridColumn<Row>): ColumnWidthSpec {
    const { width } = column;
    if (width === undefined) {
        return new ColumnWidthSpec({ width: ColumnWidth.auto });
    }
    const bounded = typeof width === 'number' ? { px: width } : width;
    const { auto, px }: { readonly auto?: unknown; readonly px?: unknown } = bounded;
    const bounds = {
        ...('minPx' in bounded ? { minWidthPx: bounded.minPx } : {}),
        ...('maxPx' in bounded ? { maxWidthPx: bounded.maxPx } : {}),
    };
    if (auto === true && px === undefined) return new ColumnWidthSpec({ width: ColumnWidth.auto, ...bounds });
    if (auto !== undefined || typeof px !== 'number') {
        throw new TypeError(`Column '${column.name}': a width is a number of pixels, { px, minPx, maxPx } or { auto: true, minPx, maxPx }.`);
    }
    const spec = new ColumnWidthSpec({ width: ColumnWidth.fixed(px), ...bounds });
    if (px < spec.minWidthPx) {
        // Refused, not clamped (ADR-0016, FN-12): a column narrower than its MinWidth could not be
        // dragged back to the width it was declared at.
        throw new RangeError(`Column '${column.name}': a Fixed width of ${px} is below its MinWidth (${spec.minWidthPx}) — refused, not clamped (ADR-0016).`);
    }
    return spec;
}
