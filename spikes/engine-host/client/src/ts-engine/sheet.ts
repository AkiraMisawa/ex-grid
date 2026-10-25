// The TypeScript engine's Sheet (ADR-0370 section 6, the third arm): only what /core/sheet exercises,
// written to answer as ExSheet.Engine answers there, so the two can be timed side by side in the same
// Worker shape. Not a port: whatever the scenario does not reach is refused, never guessed.
//
// What it does: reads a Sheet Document's JSON (version 9; cells with a number, a text or a Formula),
// parses each Formula once into a closure, keeps the cells each cell is read by, and recalculates an
// edit's dependents in dependency order. What a Formula may hold: numbers, cell references (A1, $A$1),
// + - * / ^, unary minus, parentheses and ROW(). Anything else, a range or a circular reference among
// them, is refused with the reason.

/** Excel's grid: 1,048,576 rows of 16,384 columns, as ExSheet.Engine's Sheet.RowCount and ColumnCount. */
export const rowCount = 1_048_576;
export const columnCount = 16_384;

/** An Error Value, by its code. */
export class SheetError {
    readonly code: '#VALUE!' | '#DIV/0!' | '#NUM!';

    constructor(code: SheetError['code']) {
        this.code = code;
    }
}

/** A cell's Value; null is a blank. */
export type Value = number | string | SheetError | null;

type Evaluate = (sheet: Sheet, row: number) => Value;

interface Formula {
    readonly text: string;
    readonly evaluate: Evaluate;
    /** The cells it reads, by key. */
    readonly reads: readonly number[];
}

interface Cell {
    readonly row: number;
    readonly column: number;
    entry: { readonly number: number } | { readonly text: string } | { readonly formula: Formula };
    value: Value;
    /** "0.00E+00", which a number typed with an exponent gives the cell and the cell keeps, as Excel does
     * (ExSheet.Engine's ConstantParser.Implied); otherwise General. */
    scientific: boolean;
}

const keyOf = (row: number, column: number): number => row * columnCount + column;

/** Refused: what the spike does not do, said rather than guessed. */
export class Refused extends Error {}

// --- Addresses -----------------------------------------------------------------------------------

const columnOf = (letters: string): number => {
    let column = 0;
    for (const letter of letters.toUpperCase()) column = column * 26 + (letter.charCodeAt(0) - 64);
    return column - 1;
};

/** "B12" or "$B$12" as a zero-based row and column; null when it is not an address on the grid. */
export const parseAddress = (text: string): { row: number; column: number } | null => {
    const match = /^\$?([A-Za-z]{1,3})\$?([1-9][0-9]{0,6})$/.exec(text);
    if (!match?.[1] || !match[2]) return null;
    const column = columnOf(match[1]);
    const row = Number(match[2]) - 1;
    return column < columnCount && row < rowCount ? { row, column } : null;
};

// --- Numbers ---------------------------------------------------------------------------------------

/** A number typed under en-US, as far as the scenario types them: digits, a point, a sign, an exponent. */
const parseNumber = (text: string): number | null =>
    /^[+-]?(\d+\.?\d*|\.\d+)(E[+-]?\d+)?$/i.test(text) ? Number(text) : null;

/** .NET's "G15", which ExSheet.Engine's Sheet.GetDisplay writes a number in General with
 * (NumberText.General): fifteen significant digits, scientific below 1E-05 and from 1E+15. */
export const general = (number: number): string => {
    if (number === 0) return '0';
    const [mantissa = '', exponentText = ''] = number.toExponential(14).split('e');
    const exponent = Number(exponentText);
    if (exponent < -5 || exponent >= 15) {
        const trimmed = mantissa.replace(/\.?0+$/, '');
        const power = Math.abs(exponent);
        return `${trimmed}E${exponent < 0 ? '-' : '+'}${power < 10 ? '0' : ''}${power}`;
    }
    const fixed = number.toPrecision(15);
    return fixed.includes('.') ? fixed.replace(/\.?0+$/, '') : fixed;
};

/** "0.00E+00": two decimals of the mantissa, a signed exponent of two digits or more. */
export const scientific = (number: number): string => {
    const [mantissa = '', exponentText = ''] = number.toExponential(2).split('e');
    const exponent = Number(exponentText);
    const power = Math.abs(exponent);
    return `${mantissa}E${exponent < 0 ? '-' : '+'}${power < 10 ? '0' : ''}${power}`;
};

const display = (value: Value, inScientific: boolean): string => {
    if (value === null) return '';
    if (typeof value === 'number') return inScientific ? scientific(value) : general(value);
    if (typeof value === 'string') return value;
    return value.code;
};

// --- Formulas --------------------------------------------------------------------------------------

/** A Value as a number for arithmetic: a blank is 0, text that reads as a number is that number. */
const toNumber = (value: Value): number | SheetError => {
    if (value === null) return 0;
    if (typeof value === 'number' || value instanceof SheetError) return value;
    return parseNumber(value.trim()) ?? new SheetError('#VALUE!');
};

const arithmetic = (operator: string, left: number, right: number): number | SheetError => {
    switch (operator) {
        case '+': return left + right;
        case '-': return left - right;
        case '*': return left * right;
        case '/': return right === 0 ? new SheetError('#DIV/0!') : left / right;
        case '^': {
            const result = left ** right;
            return Number.isFinite(result) ? result : new SheetError('#NUM!');
        }
        default: throw new Refused(`The spike has no operator ${operator}.`);
    }
};

const binary = (operator: string, left: Evaluate, right: Evaluate): Evaluate => (sheet, row) => {
    const a = toNumber(left(sheet, row));
    if (a instanceof SheetError) return a;
    const b = toNumber(right(sheet, row));
    if (b instanceof SheetError) return b;
    const result = arithmetic(operator, a, b);
    return typeof result === 'number' && !Number.isFinite(result) ? new SheetError('#NUM!') : result;
};

/** Parses a Formula's text (after its "=") into a closure, and the cells it reads. */
export const parseFormula = (text: string): Formula => {
    const tokens = text.slice(1).match(/\s+|\$?[A-Za-z]+\$?\d+|[A-Za-z_][A-Za-z0-9_.]*(?=\s*\()|\d+\.?\d*(?:E[+-]?\d+)?|\.\d+(?:E[+-]?\d+)?|[-+*/^(),:]|./gi)
        ?.filter((token) => token.trim() !== '') ?? [];
    const reads: number[] = [];
    let at = 0;
    const peek = (): string | undefined => tokens[at];
    const take = (expected?: string): string => {
        const token = tokens[at++];
        if (token === undefined || (expected !== undefined && token !== expected)) {
            throw new Refused(`The spike cannot read ${text}: ${expected ?? 'more'} expected at ${token ?? 'the end'}.`);
        }
        return token;
    };
    // Precedence, lowest first: + -, then * /, then ^, then unary minus (Excel puts negation above ^).
    const sum = (): Evaluate => {
        let left = product();
        for (let op = peek(); op === '+' || op === '-'; op = peek()) {
            take();
            left = binary(op, left, product());
        }
        return left;
    };
    const product = (): Evaluate => {
        let left = power();
        for (let op = peek(); op === '*' || op === '/'; op = peek()) {
            take();
            left = binary(op, left, power());
        }
        return left;
    };
    const power = (): Evaluate => {
        let left = unary();
        while (peek() === '^') {
            take();
            left = binary('^', left, unary());
        }
        return left;
    };
    const unary = (): Evaluate => {
        if (peek() === '-') {
            take();
            const operand = unary();
            return (sheet, row) => {
                const value = toNumber(operand(sheet, row));
                return value instanceof SheetError ? value : -value;
            };
        }
        if (peek() === '+') {
            take();
            return unary();
        }
        return primary();
    };
    const primary = (): Evaluate => {
        const token = take();
        if (token === '(') {
            const inner = sum();
            take(')');
            return inner;
        }
        const number = parseNumber(token);
        if (number !== null) return () => number;
        const address = parseAddress(token);
        if (address !== null) {
            if (peek() === ':') throw new Refused(`The spike reads no ranges: ${text}.`);
            const key = keyOf(address.row, address.column);
            reads.push(key);
            return (sheet) => sheet.valueAt(key);
        }
        if (token.toUpperCase() === 'ROW' && peek() === '(') {
            take('(');
            if (peek() === ')') {
                take(')');
                return (_, row) => row + 1;
            }
            const argument = take();
            const of = parseAddress(argument);
            if (of === null) throw new Refused(`The spike's ROW takes a cell or nothing: ${text}.`);
            take(')');
            return () => of.row + 1;
        }
        throw new Refused(`The spike does not know ${token} in ${text}.`);
    };
    const evaluate = sum();
    if (at !== tokens.length) throw new Refused(`The spike cannot read ${text}: ${tokens[at] ?? ''} is left over.`);
    return { text, evaluate, reads };
};

// --- The Sheet -------------------------------------------------------------------------------------

interface DocumentJson {
    readonly version: number;
    readonly culture: string;
    readonly cells: readonly { readonly at: string; readonly number?: number; readonly text?: string; readonly formula?: string }[];
}

export class Sheet {
    readonly #cells = new Map<number, Cell>();
    /** The cells that read each cell, by key: the edges recalculation follows. */
    readonly #readBy = new Map<number, number[]>();

    /** Opens a Sheet Document's JSON and calculates every Formula. */
    static open(json: string): Sheet {
        const document = JSON.parse(json) as DocumentJson;
        if (document.version !== 9) throw new Refused(`The spike reads a Sheet Document of version 9, not ${document.version}.`);
        if (document.culture !== 'en-US') throw new Refused(`The spike reads numbers under en-US only, not ${document.culture}.`);
        const sheet = new Sheet();
        for (const cell of document.cells) {
            const address = parseAddress(cell.at);
            if (address === null) throw new Refused(`${cell.at} is not a cell.`);
            const entry = cell.formula !== undefined ? { formula: parseFormula(cell.formula) }
                : cell.number !== undefined ? { number: cell.number }
                    : cell.text !== undefined ? { text: cell.text }
                        : null;
            if (entry === null) throw new Refused(`${cell.at} holds nothing the spike reads.`);
            sheet.#place(address.row, address.column, entry);
        }
        sheet.#recalculate([...sheet.#cells.keys()]);
        return sheet;
    }

    valueAt(key: number): Value {
        return this.#cells.get(key)?.value ?? null;
    }

    /** What the cell shows, or null where it holds nothing (ExSheet.Engine's GetEntry is null). */
    displayAt(row: number, column: number): string | null {
        const cell = this.#cells.get(keyOf(row, column));
        return cell === undefined ? null : display(cell.value, cell.scientific);
    }

    /** Takes text as the user typed it into a cell and recalculates: answers the rows whose cells were calculated again. */
    enter(addressText: string, typed: string): number[] {
        const address = parseAddress(addressText);
        if (address === null) throw new Refused(`${addressText} is not a cell.`);
        const number = parseNumber(typed.trim());
        const entry = typed.startsWith('=') && typed.length > 1 ? { formula: parseFormula(typed) }
            : number !== null ? { number }
                : { text: typed };
        const cell = this.#cells.get(keyOf(address.row, address.column));
        const before = cell?.entry;
        const wasScientific = cell?.scientific ?? false;
        const key = this.#place(address.row, address.column, entry);
        const placed = this.#cells.get(key);
        if (placed !== undefined && number !== null && /e/i.test(typed)) placed.scientific = true;
        try {
            return this.#recalculate([key]);
        } catch (error) {
            // The walk refuses before it calculates anything: put the entry back, and nothing has changed.
            if (before === undefined) this.#remove(key);
            else this.#place(address.row, address.column, before);
            if (placed !== undefined) placed.scientific = wasScientific;
            throw error;
        }
    }

    #remove(key: number): void {
        const cell = this.#cells.get(key);
        if (cell === undefined) return;
        this.#place(cell.row, cell.column, { number: 0 });
        this.#cells.delete(key);
    }

    #place(row: number, column: number, entry: Cell['entry']): number {
        const key = keyOf(row, column);
        const old = this.#cells.get(key);
        if (old !== undefined && 'formula' in old.entry) {
            for (const read of old.entry.formula.reads) {
                const by = this.#readBy.get(read);
                const i = by?.indexOf(key) ?? -1;
                if (by === undefined || i < 0) throw new Refused('Internal: a Formula\'s read without its edge.');
                by.splice(i, 1);
            }
        }
        if ('formula' in entry) {
            for (const read of entry.formula.reads) {
                const by = this.#readBy.get(read);
                if (by === undefined) this.#readBy.set(read, [key]);
                else by.push(key);
            }
        }
        if (old === undefined) this.#cells.set(key, { row, column, entry, value: null, scientific: false });
        else old.entry = entry;
        return key;
    }

    /**
     * Calculates the cells changed and every cell that reads them, directly or not, each after the
     * cells it reads: a depth-first walk over the read-by edges, kept on an explicit stack (a chain of
     * 20,000 is not a call stack's depth), answers them in reverse dependency order. A cell met again
     * while it is still on the walk is a circular reference, which the spike refuses.
     */
    #recalculate(changed: readonly number[]): number[] {
        const order: number[] = [];
        const state = new Map<number, 1 | 2>(); // 1 on the walk, 2 done
        for (const root of changed) {
            if (state.has(root)) continue;
            const stack: { key: number; next: number }[] = [{ key: root, next: 0 }];
            state.set(root, 1);
            while (stack.length > 0) {
                const top = stack[stack.length - 1];
                if (top === undefined) break;
                const by = this.#readBy.get(top.key);
                const child = by?.[top.next];
                if (child !== undefined) {
                    top.next++;
                    const seen = state.get(child);
                    if (seen === 1) throw new Refused('A circular reference: the spike does not calculate one.');
                    if (seen === undefined) {
                        state.set(child, 1);
                        stack.push({ key: child, next: 0 });
                    }
                    continue;
                }
                state.set(top.key, 2);
                order.push(top.key);
                stack.pop();
            }
        }
        const rows = new Set<number>();
        for (let i = order.length - 1; i >= 0; i--) {
            const key = order[i];
            const cell = key === undefined ? undefined : this.#cells.get(key);
            if (cell === undefined) throw new Refused('Internal: a cell to calculate that the Sheet does not hold.');
            const entry = cell.entry;
            cell.value = 'formula' in entry ? entry.formula.evaluate(this, cell.row)
                : 'number' in entry ? entry.number
                    : entry.text;
            rows.add(cell.row);
        }
        return [...rows];
    }
}
