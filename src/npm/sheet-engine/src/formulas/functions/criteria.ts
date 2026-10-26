// SUMIF, SUMIFS, COUNTIF, COUNTIFS, AVERAGEIF, AVERAGEIFS, MAXIFS, MINIFS and COUNTBLANK (C#'s
// Functions.Criteria.cs): a criterion read from text, matched against each cell of a range.
import { CellAddress, columnCount, rowCount } from '../../cell-address';
import { equalsIgnoreCase, tryParseInvariantDouble } from '../../dotnet';
import { errorValues, Value } from '../../value';
import type { Culture } from '../../culture';
import { compareValues, isMulti } from '../evaluator';
import type { FunctionCall } from '../function-definition';
import { literalReference } from '../nodes';
import { Operand } from '../operand';
import { Area } from '../reference';
import { numberResult, unlifted } from './shared';

export function sumIf(call: FunctionCall): Operand { return conditionalAggregate(call, 'sum', true); }
export function sumIfs(call: FunctionCall): Operand { return conditionalAggregate(call, 'sum', false); }
export function countIf(call: FunctionCall): Operand { return conditionalAggregate(call, 'count', true); }
export function countIfs(call: FunctionCall): Operand { return conditionalAggregate(call, 'count', false); }
export function averageIf(call: FunctionCall): Operand { return conditionalAggregate(call, 'average', true); }
export function averageIfs(call: FunctionCall): Operand { return conditionalAggregate(call, 'average', false); }
export function maxIfs(call: FunctionCall): Operand { return conditionalAggregate(call, 'maximum', false); }
export function minIfs(call: FunctionCall): Operand { return conditionalAggregate(call, 'minimum', false); }

/** COUNTBLANK: the cells of a range that are blank or hold empty text. */
export function countBlank(call: FunctionCall): Operand {
    const operand = call.operand(0);
    if (operand.isError) return operand;
    const range = CriteriaRange.from(operand);
    if (range === undefined) return Operand.ofError('#VALUE!');
    let count = range.count;
    for (const position of range.occupied()) {
        const value = range.read(position);
        if (value !== undefined && !(value.kind === 'text' && value.text.length === 0)) count--;
    }
    return Operand.of(Value.fromNumber(count));
}

type CriteriaAggregate = 'sum' | 'count' | 'average' | 'maximum' | 'minimum';

interface Condition {
    readonly range: CriteriaRange;
    readonly test: Criterion;
}

function conditionalAggregate(call: FunctionCall, aggregate: CriteriaAggregate, single: boolean): Operand {
    const counting = aggregate === 'count';
    const from = single || counting ? 0 : 1;
    const conditions: Condition[] = [];
    for (let i = from; i < (single ? 2 : call.count); i += 2) {
        const operand = call.operand(i);
        if (operand.isError) return operand;
        const range = CriteriaRange.from(operand);
        if (range === undefined) return Operand.ofError('#VALUE!');
        const criterionOperand = call.operand(i + 1);
        if (isMulti(criterionOperand)) return unlifted(call);
        const criterion = Criterion.parse(call.evaluator.scalarOf(criterionOperand), call.evaluator.culture);
        // A criterion outside the domain the runs settled is refused (ADR-0047): the Formula's.
        if (criterion === undefined) return call.evaluator.refuse();
        const firstCondition = conditions[0];
        if (firstCondition !== undefined && !range.sameShape(firstCondition.range)) return Operand.ofError('#VALUE!');
        conditions.push({ range, test: criterion });
    }
    const leading = conditions[0];
    if (leading === undefined) throw new Error('Internal: a conditional aggregate is called with a condition.');
    const first = leading.range;
    let results: CriteriaRange | undefined;
    if (!counting) {
        const operand = single && call.count > 2 ? call.operand(2) : call.operand(0);
        if (operand.isError) return operand;
        results = CriteriaRange.from(operand);
        if (results === undefined) return Operand.ofError('#VALUE!');
        if (!results.sameShape(first)) {
            // The ranges of SUMIFS and its partners differ in shape: #VALUE!, as Excel's (SUMIFS-035).
            if (!single) return Operand.ofError('#VALUE!');
            // SUMIF/AVERAGEIF read a footprint starting at the result's top-left cell.
            // A computed Reference that escapes its named dependencies stays refused: the Formula's.
            const criteriaReference = literalReference(call.argument(0));
            const resultReference = call.count > 2 ? literalReference(call.argument(2)) : undefined;
            if (operand.kind !== 'area' || call.count <= 2
                || criteriaReference === undefined || criteriaReference.spilled
                || resultReference === undefined || resultReference.spilled) return call.evaluator.refuse();
            const a = operand.area;
            if (a.row1 + first.rows > rowCount || a.column1 + first.columns > columnCount) return Operand.ofError('#REF!');
            results = new CriteriaRange(operand.sameSheet(new Area(a.row1, a.column1, a.row1 + first.rows - 1, a.column1 + first.columns - 1)),
                first.rows, first.columns);
        }
    }

    // A range's absent cells matter to conditions. Count them without materialising an
    // entire column, then visit the union of occupied positions to correct that count.
    const occupied = new Set<number>();
    if (results === undefined) {
        for (const condition of conditions) for (const position of condition.range.occupied()) occupied.add(position);
    } else {
        for (const position of results.occupied()) occupied.add(position);
    }
    const positions = [...occupied].toSorted((a, b) => a - b);
    const emptyMatches = counting && conditions.every(c => c.test.matches(undefined) === true);
    let count = emptyMatches ? first.count - positions.length : 0;
    let total = 0;
    let extreme = 0;
    for (const position of positions) {
        let matches = true;
        for (const condition of conditions) {
            const match = condition.test.matches(condition.range.read(position));
            // A candidate outside the domain the runs settled refuses the Formula (ADR-0047).
            if (match === undefined) return call.evaluator.refuse();
            if (!match) matches = false;
        }
        if (!matches) continue;
        if (results === undefined) {
            count++;
            continue;
        }
        const value = results.read(position);
        if (value?.isError === true) return Operand.of(value);
        if (value?.kind !== 'number') continue;
        const number = value.number;
        if (count === 0) extreme = number;
        else extreme = aggregate === 'maximum' ? Math.max(extreme, number) : Math.min(extreme, number);
        total += number;
        count++;
    }
    switch (aggregate) {
        case 'count': return Operand.of(Value.fromNumber(count));
        case 'average': return count === 0 ? Operand.ofError('#DIV/0!') : numberResult(total / count);
        case 'maximum':
        case 'minimum':
            return Operand.of(Value.fromNumber(extreme));
        case 'sum': return numberResult(total);
        default: throw new Error(`Internal: ${String(aggregate)} is no conditional aggregate.`);
    }
}

/** A range a criterion is matched against, or whose numbers are aggregated: a rectangle of cells or a Linked Table's column. */
class CriteriaRange {
    readonly #operand: Operand;
    readonly rows: number;
    readonly columns: number;

    constructor(operand: Operand, rows: number, columns: number) {
        this.#operand = operand;
        this.rows = rows;
        this.columns = columns;
    }

    get count(): number {
        return this.rows * this.columns;
    }

    sameShape(other: CriteriaRange): boolean {
        return this.rows === other.rows && this.columns === other.columns;
    }

    static from(operand: Operand): CriteriaRange | undefined {
        switch (operand.kind) {
            case 'area': return new CriteriaRange(operand, operand.area.rows, operand.area.columns);
            case 'column': return new CriteriaRange(operand, operand.columnOf().length, 1);
            case 'scalar':
            case 'missing':
            case 'array':
            case 'threeD':
                return undefined;
            default: throw new Error(`Internal: ${String(operand.kind)} is no kind of operand.`);
        }
    }

    /** The Value at a position counted row by row from the top-left cell, blank as `undefined`. */
    read(position: number): Value | undefined {
        const operand = this.#operand;
        if (operand.kind === 'area') {
            return operand.cellsOf().read(new CellAddress(operand.area.row1 + Math.floor(position / this.columns), operand.area.column1 + position % this.columns));
        }
        return operand.columnOf()[position];
    }

    /** The positions that are not blank. */
    *occupied(): Generator<number> {
        const operand = this.#operand;
        if (operand.kind === 'area') {
            for (const address of operand.cellsOf().nonBlankIn(operand.area)) {
                yield (address.row - operand.area.row1) * this.columns + address.column - operand.area.column1;
            }
        } else {
            const column = operand.columnOf();
            for (let i = 0; i < this.rows; i++) if (column[i] !== undefined) yield i;
        }
    }
}

type CriterionOperator = '=' | '<>' | '<' | '>' | '<=' | '>=';

/** Whether text holds a character outside what the runs settled for criteria: beyond ASCII, or a separator a culture reads its own way. */
function unsettledCharacter(text: string): boolean {
    for (let i = 0; i < text.length; i++) {
        const c = text.charAt(i);
        if (text.charCodeAt(i) > 127 || c === ',' || c === '/' || c === ':' || c === '%' || c === '$') return true;
    }
    return false;
}

/** Whether text holds a character beyond ASCII. */
function beyondAscii(text: string): boolean {
    for (let i = 0; i < text.length; i++) if (text.charCodeAt(i) > 127) return true;
    return false;
}

/** .NET's `bool.TryParse`: `True` or `False` in any case, white space and NUL trimmed from either end. */
function tryParseBoolean(text: string): boolean | undefined {
    let start = 0;
    let end = text.length;
    while (start < end && trimmable(text.charCodeAt(start))) start++;
    while (end > start && trimmable(text.charCodeAt(end - 1))) end--;
    const word = text.slice(start, end).toLowerCase();
    return word === 'true' ? true : word === 'false' ? false : undefined;
}

/** White space or NUL, as .NET's `bool.TryParse` trims them, for the ASCII the criteria admit. */
function trimmable(c: number): boolean {
    return c === 0 || c === 0x20 || (c >= 0x09 && c <= 0x0d) || c === 0x85 || c === 0xa0;
}

/** Whether a numeric text holds at most fifteen digits before its exponent, the digits a double holds without doubt. */
function shortNumericText(text: string): boolean {
    let digits = 0;
    for (let i = 0; i < text.length; i++) {
        const c = text.charAt(i);
        if (c === 'e' || c === 'E') break;
        if (c >= '0' && c <= '9') digits++;
    }
    return digits <= 15;
}

/** A criterion as `COUNTIF` and its kin read one: an operator and the Value it compares with, or a test for absent cells alone. */
class Criterion {
    readonly operator: CriterionOperator;
    readonly wanted: Value | undefined;
    readonly onlyAbsent: boolean;
    readonly dotDecimal: boolean;

    private constructor(operator: CriterionOperator, wanted: Value | undefined, onlyAbsent: boolean, dotDecimal: boolean) {
        this.operator = operator;
        this.wanted = wanted;
        this.onlyAbsent = onlyAbsent;
        this.dotDecimal = dotDecimal;
    }

    /** The criterion a Value states, or `undefined` where its reading is one the runs have not settled (a refusal). */
    static parse(value: Value | undefined, culture: Culture): Criterion | undefined {
        const dotDecimal = culture.data.numberDecimalSeparator === '.';
        if (!dotDecimal && value?.kind === 'text' && value.text.includes('.')) return undefined;
        return Criterion.#parseValue(value, dotDecimal);
    }

    static #parseValue(value: Value | undefined, dotDecimal: boolean): Criterion | undefined {
        const make = (operator: CriterionOperator, wanted: Value | undefined, onlyAbsent = false): Criterion =>
            new Criterion(operator, wanted, onlyAbsent, dotDecimal);
        if (value === undefined) return make('=', Value.fromNumber(0));
        if (value.kind !== 'text') return make('=', value);
        const written = value.text;
        let s = written;
        if (s.length > 255) return undefined;
        let op: CriterionOperator = '=';
        if (s.startsWith('<>') || s.startsWith('<=') || s.startsWith('>=')) {
            op = s.slice(0, 2) as CriterionOperator; // oxlint-disable-line typescript/no-unsafe-type-assertion -- one of the three just tested
            s = s.slice(2);
        } else if (s.startsWith('=') || s.startsWith('<') || s.startsWith('>')) {
            op = s.slice(0, 1) as CriterionOperator; // oxlint-disable-line typescript/no-unsafe-type-assertion -- one of the three just tested
            s = s.slice(1);
        }
        const equality = op === '=' || op === '<>';
        if (s.length === 0 && written.length > 0) return equality ? make(op, undefined, true) : undefined;
        for (const error of errorValues) {
            if (equalsIgnoreCase(s, error)) return equality ? make(op, Value.fromError(error)) : undefined;
        }
        // Criteria numbers are culture-sensitive; only the invariant numeric spelling is
        // admitted until the other cultures have been observed. Unicode text is likewise
        // refused instead of delegating its matching to a platform-dependent collation.
        if (unsettledCharacter(s)) return undefined;
        const number = tryParseInvariantDouble(s);
        if (number !== undefined) return Number.isFinite(number) && shortNumericText(s) ? make(op, Value.fromNumber(number)) : undefined;
        const boolean = tryParseBoolean(s);
        if (boolean !== undefined) return equality ? make(op, Value.fromBoolean(boolean)) : undefined;
        return equality ? make(op, Value.fromText(s)) : undefined;
    }

    /** Whether a cell's Value meets the criterion; `undefined` where the answer is one the runs have not settled (a refusal). */
    matches(candidate: Value | undefined): boolean | undefined {
        if (this.onlyAbsent) {
            switch (this.operator) {
                case '=': return candidate === undefined;
                case '<>': return candidate !== undefined;
                case '<':
                case '>':
                case '<=':
                case '>=':
                    return false;
                default: throw new Error(`Internal: ${String(this.operator)} is no criterion operator.`);
            }
        }
        const wanted = this.wanted;
        if (wanted === undefined) throw new Error('Internal: a criterion that is not for absent cells wants a Value.');
        if (wanted.kind === 'number') {
            if (candidate?.kind === 'number') return this.#compare(compareValues(candidate, wanted));
            if (this.operator === '=' && candidate?.kind === 'text') {
                const text = candidate.text;
                if (unsettledCharacter(text) || (!this.dotDecimal && text.includes('.'))) return undefined;
                const number = tryParseInvariantDouble(text);
                if (number !== undefined) {
                    return Number.isFinite(number) && shortNumericText(text) ? compareValues(Value.fromNumber(number), wanted) === 0 : undefined;
                }
            }
            return this.operator === '<>';
        }
        if (wanted.kind === 'boolean' || wanted.kind === 'error') {
            const same = candidate !== undefined && candidate.kind === wanted.kind && candidate.equals(wanted);
            return this.operator === '=' ? same : this.operator === '<>' && !same;
        }
        if (candidate === undefined) return (wanted.text.length === 0 && this.operator === '=') || (wanted.text.length > 0 && this.operator === '<>');
        if (candidate.kind !== 'text') return this.operator === '<>';
        if (beyondAscii(candidate.text)) return undefined;
        const sameText = matchesCriteriaText(wanted.text, candidate.text);
        return this.operator === '=' ? sameText : !sameText;
    }

    #compare(comparison: number): boolean {
        switch (this.operator) {
            case '=': return comparison === 0;
            case '<>': return comparison !== 0;
            case '<': return comparison < 0;
            case '>': return comparison > 0;
            case '<=': return comparison <= 0;
            case '>=': return comparison >= 0;
            default: throw new Error(`Internal: ${String(this.operator)} is no criterion operator.`);
        }
    }
}

/** Upper case as `char.ToUpperInvariant` makes it, for the ASCII a criterion's text is held to. */
function upperAscii(c: number): number {
    return c >= 0x61 && c <= 0x7a ? c - 0x20 : c;
}

/** Whether text matches a criterion's pattern, without regard to case: `*` any run, `?` any one character, `~` before either a literal. */
function matchesCriteriaText(pattern: string, text: string): boolean {
    // Criteria escape '*' and '?' with '~'; an ordinary or repeated tilde stays literal.
    // Keep a last-star position for backtracking without recursion or a regex engine.
    let p = 0;
    let t = 0;
    let star = -1;
    let retry = 0;
    while (t < text.length) {
        if (p < pattern.length && pattern.charAt(p) === '*') {
            star = ++p;
            retry = t;
            continue;
        }
        if (p < pattern.length) {
            let c = pattern.charAt(p);
            let size = 1;
            const any = c === '?';
            const next = pattern.charAt(p + 1);
            if (c === '~' && p + 1 < pattern.length && (next === '*' || next === '?')) {
                c = next;
                size = 2;
            }
            if (any || upperAscii(c.charCodeAt(0)) === upperAscii(text.charCodeAt(t))) {
                p += size;
                t++;
                continue;
            }
        }
        if (star < 0) return false;
        p = star;
        t = ++retry;
    }
    while (p < pattern.length && pattern.charAt(p) === '*') p++;
    return p === pattern.length;
}
