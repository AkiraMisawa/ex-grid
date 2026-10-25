import { CellAddress } from '../cell-address';
import { parseNumber } from '../constant-parser';
import type { Culture } from '../culture';
import { writtenText } from '../number-text';
import { Value, type ErrorValue } from '../value';
import { approximatelyEqual, finalAdd, power } from './arithmetic';
import type { CellReader } from './cell-reader';
import { isUndeclaredExcelFunction } from './excel-function-names';
import { FunctionCall } from './function-definition';
import type { FunctionNode, LetBinding, LetNode, Node } from './nodes';
import { Operand } from './operand';
import { compareText } from './text-order';
import { ValueArray } from './value-array';

/** A number, or the Error Value met where one was wanted (C#'s `out ErrorValue? error`). */
export type NumberOrError = number | ErrorValue;

/** A binding's one Value, once it has been read. */
interface BindingSlot {
    value: Operand | undefined;
}

/** A node to compute, or (`operands`) one whose operands are computed and stand on the stack of results. */
interface Step {
    readonly node: Node;
    readonly operands: boolean;
}

/**
 * Evaluates a parsed Formula to a Value, as Excel does: IEEE doubles, a blank read as 0, Error Values carried
 * through, left to right (ADR-0047).
 */
export class Evaluator {
    readonly culture: Culture;
    readonly cells: CellReader;
    /** The cell whose Formula is being evaluated, which `ROW()` and `COLUMN()` read; `undefined` for a Formula evaluated in no cell. */
    self: CellAddress | undefined;

    /**
     * The refusal of the Formula being evaluated, or `undefined`: `Value.refusal` once a function or an operator
     * has refused it, else `Value.refusedName` once it has called a function Excel has and ExSheet does not
     * declare, or named a Linked Table bare.
     */
    #refusal: Value | undefined;

    /** The `LET` bindings in scope while their `LET` is evaluated, each computed when first read (ADR-0181). */
    readonly #bindings = new Map<LetBinding, BindingSlot>();

    /** The slots of the bindings `#operator` has taken out of scope while their value is computed. */
    readonly #bindingsOutOfScope = new Map<LetBinding, BindingSlot>();

    constructor(cells: CellReader, culture: Culture) {
        this.cells = cells;
        this.culture = culture;
    }

    /**
     * A Formula's result. It is never blank: a Formula that reads an empty cell shows 0. When the Formula's last
     * operation is an addition or a subtraction whose result nearly cancels, the result is 0, as Excel's is.
     */
    evaluate(node: Node): Value {
        const value = this.scalarOf(this.evaluateResult(node));
        return value === undefined ? Value.fromNumber(0) : value;
    }

    /**
     * A Formula's result as it spills (ADR-0125): a range or an array of more than one Value is returned as it
     * is, for the Sheet to spill; anything else is one Value, never blank. A Formula a call or an operator
     * refused is the refusal, `#VALUE!`, whatever the rest of it made of it.
     */
    evaluateResult(node: Node): Operand {
        // A Formula computed on demand while another is evaluated has its own refusal; the outer Formula's is put back after it.
        const outerRefusal = this.#refusal;
        this.#refusal = undefined;
        try {
            const result = this.#result(node);
            const refusal = this.#refusalSoFar();
            return refusal === undefined ? result : Operand.of(refusal);
        } finally {
            this.#refusal = outerRefusal;
        }
    }

    /**
     * Refuses the Formula being evaluated: its result is the refusal, `#VALUE!` (`Value.refusal`), whatever the
     * functions and operators around it make of the Error Value returned here, so `IFERROR` cannot catch it
     * (ADR-0047; 2026-10-12, decided with the user: a refusal is never caught, anywhere).
     */
    refuse(): Operand {
        this.#refusal = Value.refusal;
        return Operand.of(Value.refusal);
    }

    /**
     * Refuses the Formula being evaluated with `#NAME?` (`Value.refusedName`): a call of a function Excel has and
     * ExSheet does not declare, or a declared Linked Table's bare name (2026-10-13, decided with the user). A
     * refusal by `refuse` in the same Formula comes before it.
     */
    refuseName(): Operand {
        this.#refusal ??= Value.refusedName;
        return Operand.of(Value.refusedName);
    }

    /** `refuse`, for a function that answers with a Value: the Formula is refused, and the refusal is returned to stand where the Value would. */
    refused(): Value {
        this.#refusal = Value.refusal;
        return Value.refusal;
    }

    /** The refusal of the Formula being evaluated, read after a computation that may have set it. */
    #refusalSoFar(): Value | undefined {
        return this.#refusal;
    }

    #result(node: Node): Operand {
        if (node.kind === 'binary' && (node.operator === '+' || node.operator === '-')) {
            const left = this.operand(node.left);
            const right = this.operand(node.right);
            if (isMulti(left) || isMulti(right)) return this.#elementwise(node.operator, left, right);
            return Operand.of(this.#binaryValues(node.operator, this.scalarOf(left), this.scalarOf(right), true));
        }
        const result = this.operand(node);
        if (isMulti(result)) return result;
        const value = this.scalarOf(result);
        return Operand.of(value === undefined ? Value.fromNumber(0) : value);
    }

    /** An operand read whole, as an array: a range's cells, a Linked Table's column, or one Value. */
    toArray(operand: Operand): ValueArray {
        switch (operand.kind) {
            case 'array':
                return operand.arrayOf();
            case 'area': {
                const area = operand.area;
                if (area.rows * area.columns > ValueArray.mostCells) return ValueArray.single(Value.fromError('#NUM!'));
                const array = new ValueArray(area.rows, area.columns);
                const cells = operand.cellsOf();
                for (const address of cells.nonBlankIn(area)) array.set(address.row - area.row1, address.column - area.column1, cells.read(address));
                return array;
            }
            case 'column': {
                const column = operand.columnOf();
                if (column.length === 0) return ValueArray.single(Value.fromError('#VALUE!'));
                const values = new ValueArray(column.length, 1);
                for (let i = 0; i < column.length; i++) values.set(i, 0, column[i]);
                return values;
            }
            case 'scalar':
            case 'missing':
            case 'threeD':
                return ValueArray.single(this.scalarOf(operand));
            default:
                throw new Error(`Internal: ${String(operand.kind)} is no kind of operand.`);
        }
    }

    /** An operator applied element by element, the two arrays broadcast as Excel broadcasts them (ADR-0125). */
    #elementwise(op: string, leftOperand: Operand, rightOperand: Operand): Operand {
        const left = this.toArray(leftOperand);
        const right = this.toArray(rightOperand);
        const rows = left.rows === 1 ? right.rows : right.rows === 1 ? left.rows : Math.max(left.rows, right.rows);
        const columns = left.columns === 1 ? right.columns : right.columns === 1 ? left.columns : Math.max(left.columns, right.columns);
        if (rows * columns > ValueArray.mostCells) return Operand.ofError('#NUM!');
        const result = new ValueArray(rows, columns);
        for (let r = 0; r < rows; r++) {
            for (let c = 0; c < columns; c++) result.set(r, c, this.#binaryValues(op, left.broadcast(r, c), right.broadcast(r, c), false));
        }
        return Operand.ofArray(result);
    }

    /** A function of one Value applied to each Value of an operand that holds several. */
    #map(operand: Operand, each: (value: Value | undefined) => Value): Operand {
        if (!isMulti(operand)) return Operand.of(each(this.scalarOf(operand)));
        const source = this.toArray(operand);
        const result = new ValueArray(source.rows, source.columns);
        for (let r = 0; r < source.rows; r++) {
            for (let c = 0; c < source.columns; c++) result.set(r, c, each(source.at(r, c)));
        }
        return Operand.ofArray(result);
    }

    /**
     * What a node evaluates to. Operators, parentheses and the `LET` names read are computed in a loop with a
     * stack of their own (`#operator`), so the evaluator's recursion goes only through calls and `LET`s, which
     * the parser bounds.
     */
    operand(node: Node): Operand {
        switch (node.kind) {
            case 'binary':
            case 'parentheses':
            case 'unary':
            case 'percent':
                return this.#operator(node);
            case 'name':
                return node.binding === undefined ? this.#leaf(node) : this.#operator(node);
            case 'let':
                return this.#let(node);
            case 'function':
                return this.#call(node);
            case 'number':
            case 'text':
            case 'boolean':
            case 'error':
            case 'missing':
            case 'reference':
            case 'structuredReference':
            case 'intersection':
                return this.#leaf(node);
            default:
                return unreachableNode(node);
        }
    }

    /** A call: `#NAME?` for a name outside the declared set, refused where Excel has the function and ExSheet does not. */
    #call(f: FunctionNode): Operand {
        const definition = f.function;
        if (definition === undefined) return isUndeclaredExcelFunction(f.name) ? this.refuseName() : Operand.ofError('#NAME?');
        if (spanWhereOneSheetIsAsked(f)) return Operand.ofError('#VALUE!');
        return definition.invoke(new FunctionCall(this, f.arguments, f.place, definition.threeD));
    }

    /** A node that holds no other to compute: a constant, a Reference, a Linked Table's column, or a name nothing binds. */
    #leaf(node: Node): Operand {
        switch (node.kind) {
            case 'number': return Operand.of(Value.fromNumber(node.number));
            case 'text': return Operand.of(Value.fromText(node.text));
            case 'boolean': return Operand.of(Value.fromBoolean(node.value));
            case 'error': return Operand.ofError(node.error);
            case 'missing': return Operand.missing;
            case 'reference': {
                const reference = node.reference;
                if (reference.lastSheetName !== undefined) {
                    // A 3D Reference is its rectangle on every Sheet of its span; one that names a Sheet the
                    // Workbook lacks is #NAME?, and its spill #REF!, as Excel answered (ADR-0331 §7, ticket 12).
                    if (reference.spilled) return Operand.ofError('#REF!');
                    if (reference.sheetName === undefined) throw new Error('Internal: a 3D Reference names the Sheet that starts its span.');
                    const sheets = this.cells.resolveSpan(reference.sheetName, reference.lastSheetName);
                    return sheets === undefined ? Operand.ofError('#NAME?') : Operand.ofThreeD(reference.area, sheets);
                }
                // A Reference reads the Sheet its qualifier names; a name no Sheet of the Workbook has names nothing (ADR-0046, ADR-0331).
                const sheet = this.cells.resolve(reference.sheetName);
                if (sheet === undefined) return Operand.ofError('#REF!');
                if (reference.spilled) return sheet.spill(new CellAddress(reference.row1, reference.column1));
                return Operand.ofArea(reference.area, sheet);
            }
            case 'structuredReference': return this.cells.tableColumn(node.table, node.column);
            case 'name':
            case 'intersection':
                return this.#namesLinkedTable(node) ? this.refuseName() : Operand.ofError('#NAME?');
            case 'let':
            case 'function':
            case 'parentheses':
            case 'unary':
            case 'percent':
            case 'binary':
                throw new Error(`Internal: no evaluation for ${node.kind} as a leaf.`);
            default:
                return unreachableNode(node);
        }
    }

    /** Whether an unbound name, or a side of an intersection, is a declared Linked Table's bare name (ADR-0049). */
    #namesLinkedTable(node: Node): boolean {
        if (node.kind === 'name') return node.binding === undefined && this.cells.isLinkedTable(node.name);
        if (node.kind === 'intersection') return this.#namesLinkedTable(node.left) || this.#namesLinkedTable(node.right);
        if (node.kind === 'parentheses') return this.#namesLinkedTable(node.inner);
        return false;
    }

    /**
     * An operand used as one Value: a single cell is read; a rectangle of more than one cell refuses the Formula
     * (`refused`), because Excel 365 applies what wants one Value to each Value of it, which ExSheet does not yet
     * (ADR-0125). An empty argument is blank. A 3D Reference is `#REF!` where it is written, and `#VALUE!` where a
     * function handed it on, as Excel answered (ADR-0331 §7).
     */
    scalarOf(operand: Operand): Value | undefined {
        switch (operand.kind) {
            case 'scalar':
                return operand.scalar;
            case 'area':
                return operand.area.isSingleCell ? operand.cellsOf().read(new CellAddress(operand.area.row1, operand.area.column1)) : this.refused();
            case 'column': {
                const column = operand.columnOf();
                return column.length === 1 ? column[0] : this.refused();
            }
            case 'array': {
                const array = operand.arrayOf();
                return array.isSingle ? array.at(0, 0) : this.refused();
            }
            case 'threeD':
                return Value.fromError(operand.handedOn ? '#VALUE!' : '#REF!');
            case 'missing':
                return undefined;
            default:
                throw new Error(`Internal: ${String(operand.kind)} is no kind of operand.`);
        }
    }

    /** The Values of a range that are not blank, in order: row-major for cells, top to bottom for a Linked Table's column. */
    *rangeValues(range: Operand): Generator<Value> {
        switch (range.kind) {
            case 'area': {
                const cells = range.cellsOf();
                for (const address of cells.nonBlankIn(range.area)) yield readNonBlank(cells, address);
                return;
            }
            case 'column':
                for (const value of range.columnOf()) {
                    if (value !== undefined) yield value;
                }
                return;
            case 'array':
                yield* range.arrayOf().nonBlank();
                return;
            case 'threeD':
                // Sheet by Sheet in tab order, each row-major (ADR-0331 §7).
                for (const sheet of range.sheetsOf()) {
                    for (const address of sheet.nonBlankIn(range.area)) yield readNonBlank(sheet, address);
                }
                return;
            case 'scalar':
            case 'missing':
                throw new TypeError('Not a range.');
            default:
                throw new Error(`Internal: ${String(range.kind)} is no kind of operand.`);
        }
    }

    scalar(node: Node): Value | undefined {
        return this.scalarOf(this.operand(node));
    }

    /**
     * `LET` (ADR-0181): its calculation, with its names in scope. A binding is computed when its name is first
     * read and at most once, every read giving that one Value; one never read is never computed.
     */
    #let(let_: LetNode): Operand {
        const outer = let_.bindings.map(binding => {
            const slot = this.#bindings.get(binding);
            this.#bindings.set(binding, { value: undefined });
            return slot;
        });
        try {
            return this.operand(let_.calculation);
        } finally {
            outer.forEach((slot, i) => {
                const binding = let_.bindings[i];
                if (binding === undefined) throw new Error('Internal: a LET lost a binding.');
                if (slot === undefined) this.#bindings.delete(binding);
                else this.#bindings.set(binding, slot);
            });
        }
    }

    #negate(op: '+' | '-', value: Value | undefined): Value {
        const operand = this.toNumber(value);
        if (typeof operand !== 'number') return Value.fromError(operand);
        return Value.fromNumber(op === '-' ? -operand : operand);
    }

    #percent(value: Value | undefined): Value {
        const operand = this.toNumber(value);
        return typeof operand !== 'number' ? Value.fromError(operand) : numberValue(operand / 100);
    }

    /**
     * Operators, parentheses and the `LET` names read, computed with a stack of their own rather than by
     * recursion, so that a chain as long as Excel accepts, parentheses and signs as deep as the parser reads,
     * or a chain of names each bound to the one before, never deepen the call stack. A name's binding is
     * computed where it is first read and at most once; it is out of scope while its value is computed.
     */
    #operator(top: Node): Operand {
        const work: Step[] = [{ node: top, operands: false }];
        const done: Operand[] = [];
        // The bindings taken out of scope while their value is computed: put back if anything throws.
        const computing: LetBinding[] = [];
        try {
            for (let step = work.pop(); step !== undefined; step = work.pop()) {
                if (step.operands) this.#combine(step.node, done, computing);
                else this.#expand(step.node, work, done, computing);
            }
        } finally {
            this.#putBack(computing);
        }
        return pop(done);
    }

    /** Puts the bindings taken out of scope while their value was computed back in scope. */
    #putBack(computing: readonly LetBinding[]): void {
        for (const binding of computing) {
            const slot = this.#bindingsOutOfScope.get(binding);
            if (slot === undefined) throw new Error('Internal: a binding out of scope lost its slot.');
            this.#bindings.set(binding, slot);
            this.#bindingsOutOfScope.delete(binding);
        }
    }

    /** Puts a node's operands on the work stack, after itself, or computes a node that holds none of those. */
    #expand(node: Node, work: Step[], done: Operand[], computing: LetBinding[]): void {
        switch (node.kind) {
            case 'parentheses':
                // A 3D Reference in parentheses is #NAME?, as Excel answered =(Sheet1:Sheet3!B2) (ADR-0331 §7).
                if (node.inner.kind === 'reference' && node.inner.reference.isThreeD) {
                    done.push(Operand.ofError('#NAME?'));
                    return;
                }
                work.push({ node: node.inner, operands: false });
                return;
            case 'unary':
            case 'percent':
                work.push({ node, operands: true }, { node: node.operand, operands: false });
                return;
            case 'binary':
                work.push({ node, operands: true }, { node: node.right, operands: false }, { node: node.left, operands: false });
                return;
            case 'name': {
                const binding = node.binding;
                if (binding === undefined) {
                    done.push(this.operand(node));
                    return;
                }
                // Read: the Value once computed; else computed now, out of scope while it is.
                const slot = this.#bindings.get(binding);
                if (slot === undefined) {
                    done.push(Operand.ofError('#NAME?'));
                    return;
                }
                if (slot.value !== undefined) {
                    done.push(slot.value);
                    return;
                }
                this.#bindings.delete(binding);
                this.#bindingsOutOfScope.set(binding, slot);
                computing.push(binding);
                work.push({ node, operands: true }, { node: binding.value, operands: false });
                return;
            }
            case 'number':
            case 'text':
            case 'boolean':
            case 'error':
            case 'missing':
            case 'reference':
            case 'structuredReference':
            case 'let':
            case 'intersection':
            case 'function':
                done.push(this.operand(node));
                return;
            default:
                return unreachableNode(node);
        }
    }

    /** Computes a node from its operands' results, on top of the stack of results. */
    #combine(node: Node, done: Operand[], computing: LetBinding[]): void {
        switch (node.kind) {
            case 'unary': {
                const operator = node.operator;
                done.push(this.#map(pop(done), value => this.#negate(operator, value)));
                return;
            }
            case 'percent':
                done.push(this.#map(pop(done), value => this.#percent(value)));
                return;
            case 'binary': {
                const right = pop(done);
                const left = pop(done);
                done.push(isMulti(left) || isMulti(right)
                    ? this.#elementwise(node.operator, left, right)
                    : Operand.of(this.#binaryValues(node.operator, this.scalarOf(left), this.scalarOf(right), false)));
                return;
            }
            case 'name': {
                const binding = node.binding;
                if (binding === undefined) throw new Error('Internal: an unbound name has no operands to combine.');
                const slot = this.#bindingsOutOfScope.get(binding);
                if (slot === undefined) throw new Error('Internal: a binding computed lost its slot.');
                this.#bindingsOutOfScope.delete(binding);
                computing.splice(computing.indexOf(binding), 1);
                this.#bindings.set(binding, slot);
                // A name bound to a 3D Reference hands it on, as LET's did in Excel (ADR-0331 §7).
                const value = pop(done).handOn();
                slot.value = value;
                done.push(value);
                return;
            }
            case 'boolean':
            case 'error':
            case 'function':
            case 'intersection':
            case 'let':
            case 'missing':
            case 'number':
            case 'parentheses':
            case 'reference':
            case 'structuredReference':
            case 'text':
                throw new Error(`Internal: ${node.kind} has no operands to combine.`);
            default:
                throw new Error(`Internal: ${String(node)} is no kind of node.`);
        }
    }

    #binaryValues(op: string, left: Value | undefined, right: Value | undefined, finalOperation: boolean): Value {
        switch (op) {
            case '&':
                if (left?.isError === true) return left;
                if (right?.isError === true) return right;
                return Value.fromText(this.toText(left) + this.toText(right));
            case '=':
            case '<>':
            case '<':
            case '>':
            case '<=':
            case '>=': {
                if (left?.isError === true) return left;
                if (right?.isError === true) return right;
                const order = compareValues(left, right, approximatelyEqual);
                switch (op) {
                    case '=': return Value.fromBoolean(order === 0);
                    case '<>': return Value.fromBoolean(order !== 0);
                    case '<': return Value.fromBoolean(order < 0);
                    case '>': return Value.fromBoolean(order > 0);
                    case '<=': return Value.fromBoolean(order <= 0);
                    case '>=': return Value.fromBoolean(order >= 0);
                }
            }
            default:
                break;
        }

        const a = this.toNumber(left);
        if (typeof a !== 'number') return Value.fromError(a);
        const b = this.toNumber(right);
        if (typeof b !== 'number') return Value.fromError(b);
        switch (op) {
            case '+': return numberValue(finalOperation ? finalAdd(a, b) : a + b);
            case '-': return numberValue(finalOperation ? finalAdd(a, -b) : a - b);
            case '*': return numberValue(a * b);
            case '/': return b === 0 ? Value.fromError('#DIV/0!') : numberValue(a / b);
            case '^': {
                if (a === 0 && b === 0) return Value.fromError('#NUM!');
                if (a === 0 && b < 0) return Value.fromError('#DIV/0!');
                const raised = power(a, b);
                return raised === undefined ? Value.fromError('#NUM!') : numberValue(raised);
            }
            default: throw new Error(`Internal: no operator ${op}.`);
        }
    }

    /**
     * Excel's coercion of an operand to a number: a blank is 0, a boolean 1 or 0, text that reads as a typed
     * number would under the Sheet's culture is that number, other text is `#VALUE!`.
     */
    toNumber(value: Value | undefined): NumberOrError {
        if (value === undefined) return 0;
        switch (value.kind) {
            case 'number': return value.number;
            case 'boolean': return value.boolean ? 1 : 0;
            case 'error': return value.error;
            case 'text': {
                const parsed = parseNumber(value.text, this.culture);
                return parsed === undefined ? '#VALUE!' : parsed;
            }
            default: throw new Error(`Internal: ${String(value.kind)} is no kind of Value.`);
        }
    }

    /** Excel's coercion to text: a blank is empty, a number is written as Excel writes one into text under the Sheet's culture. */
    toText(value: Value | undefined): string {
        if (value === undefined) return '';
        switch (value.kind) {
            case 'text': return value.text;
            case 'number': return writtenText(value.number, this.culture);
            case 'boolean': return value.boolean ? 'TRUE' : 'FALSE';
            case 'error': return value.error;
            default: throw new Error(`Internal: ${String(value.kind)} is no kind of Value.`);
        }
    }
}

/** Whether an operand holds more than one Value: a range of several cells, a Linked Table's column of several, or an array. */
export function isMulti(operand: Operand): boolean {
    switch (operand.kind) {
        case 'area': return !operand.area.isSingleCell;
        case 'column': return operand.columnOf().length !== 1;
        case 'array': return true;
        case 'scalar':
        case 'missing':
        case 'threeD':
            return false;
        default: throw new Error(`Internal: ${String(operand.kind)} is no kind of operand.`);
    }
}

/** A result that is not a finite double is `#NUM!`, as in Excel. */
export function numberValue(result: number): Value {
    return Number.isFinite(result) ? Value.fromNumber(result) : Value.fromError('#NUM!');
}

/**
 * Excel's comparison: a blank is the empty value of the other side's kind (0, "" or FALSE); numbers sort before
 * text, and text before booleans; numbers compare exactly, unless `equalNumbers` says two are equal (the
 * comparison operators compare at Excel's precision); text compares as Excel collates it (`compareText`).
 */
export function compareValues(left: Value | undefined, right: Value | undefined, equalNumbers?: (a: number, b: number) => boolean): number {
    if (left === undefined && right === undefined) return 0;
    const a = left ?? emptyOf(defined(right).kind);
    const b = right ?? emptyOf(a.kind);
    if (a.kind !== b.kind) return Math.sign(rank(a) - rank(b));
    switch (a.kind) {
        case 'number':
            if (equalNumbers?.(a.number, b.number) === true) return 0;
            return a.number < b.number ? -1 : a.number > b.number ? 1 : 0;
        case 'text': return compareText(a.text, b.text);
        case 'boolean':
        case 'error':
            return Math.sign(Number(a.boolean) - Number(b.boolean));
        default: throw new Error(`Internal: ${String(a.kind)} is no kind of Value.`);
    }
}

function rank(value: Value): number {
    return value.kind === 'number' ? 0 : value.kind === 'text' ? 1 : 2;
}

function emptyOf(kind: Value['kind']): Value {
    return kind === 'text' ? Value.fromText('') : kind === 'boolean' ? Value.fromBoolean(false) : Value.fromNumber(0);
}

function defined(value: Value | undefined): Value {
    if (value === undefined) throw new Error('Internal: one side of a comparison is a Value.');
    return value;
}

function readNonBlank(cells: CellReader, address: CellAddress): Value {
    const value = cells.read(address);
    if (value === undefined) throw new Error(`Internal: ${address.toString()} was listed as not blank.`);
    return value;
}

function pop<T>(stack: T[]): T {
    const top = stack.pop();
    if (top === undefined) throw new Error('Internal: the evaluator popped an empty stack.');
    return top;
}

/**
 * Whether a 3D Reference is written as an argument where the function asks for a range of one Sheet: the call
 * is `#VALUE!` before anything of it is read, as Excel's `SUMIF(Sheet1:Sheet3!B2,">0")` is (ADR-0331 §7).
 */
function spanWhereOneSheetIsAsked(f: FunctionNode): boolean {
    const policy = f.function?.threeD;
    if (policy === undefined) return false;
    return f.arguments.some((argument, i) => argument.kind === 'reference' && argument.reference.isThreeD && policy(i, f.arguments.length) === 'oneSheet');
}

function unreachableNode(node: never): never {
    throw new Error(`Internal: no evaluation for ${String(node)}.`);
}
