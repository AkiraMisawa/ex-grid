// A grid view's filter, as the View State's JSON reads it, made the filter its grid's source executes: each
// operand, which carries its type (ADR-0207), handed over as the value of that type the core compares, exactly or
// not at all (spine rule 1).

import type { FilterClause, GridFilter, ViewStateOperand } from '@ex-grid/core';
import { BoardRefusedError, type SnapshotKind } from '@ex-grid/data';
import type { ShapedColumn } from './grid-view-spec';
import { plainDateTimeOf, significantDigits } from './snapshot-values';

/** The most significant digits a Decimal operand is compared at exactly: a grid's numbers are C#'s decimal read
 * from a double, fifteen digits at most (ADR-0023). */
const exactDigits = 15;

/**
 * `filter`, its operands made the values the core compares a column of each kind with: text a string, a Decimal,
 * a Double or an Integer a number or a `bigint`, a Date a `PlainDateTime`, a Boolean a boolean. An operand of
 * another type than its column's kind, or a Decimal of more digits than a grid's number compares exactly, is
 * refused with a {@link @ex-grid/data#BoardRefusedError} at `path`.
 */
export function engineFilter(filter: GridFilter | undefined, columns: readonly ShapedColumn[], path: string): GridFilter | undefined {
    if (filter === undefined) return undefined;
    const specs = Object.entries(filter.columns).map(([name, spec]) => {
        const kind = columns.find(column => column.column === name)?.kind;
        if (kind === undefined) throw new BoardRefusedError(path, `The filter is on column '${name}', which the grid does not show.`);
        const clauses = spec.clauses.map((clause): FilterClause => {
            const { value, values } = clause;
            return {
                ...clause,
                ...(value === undefined ? {} : { value: operand(value, kind, name, path) }),
                ...(values === undefined ? {} : { values: values.map(one => operand(one, kind, name, path)) }),
            };
        });
        return [name, { ...spec, clauses }] as const;
    });
    return { ...filter, columns: Object.fromEntries(specs) };
}

function operand(written: unknown, kind: SnapshotKind, column: string, path: string): unknown {
    if (!isOperand(written)) throw new Error(`Internal: the filter on column '${column}' holds an operand without its type.`);
    const tagged = written;
    const refuse = (): never => {
        throw new BoardRefusedError(path, `Column '${column}' is ${kind}, and its filter compares it with a ${tagged.type}.`);
    };
    switch (tagged.type) {
        case 'string': return kind === 'text' ? tagged.value : refuse();
        case 'boolean': return kind === 'boolean' ? tagged.value : refuse();
        case 'double':
        case 'single': return numeric(kind) ? tagged.value : refuse();
        case 'decimal': {
            if (!numeric(kind)) return refuse();
            if (significantDigits(tagged.value) > exactDigits) {
                throw new BoardRefusedError(path, `Column '${column}': the filter's ${significantDigits(tagged.value)} significant digits are more than a grid `
                    + `compares a number at (${exactDigits}).`);
            }
            return Number(`${tagged.value.mantissa}e-${tagged.value.scale}`);
        }
        case 'int64':
        case 'int32':
        case 'int16':
        case 'sByte':
        case 'uInt64':
        case 'uInt32':
        case 'uInt16':
        case 'byte': return numeric(kind) ? tagged.value : refuse();
        case 'dateTime': return kind === 'date' ? plainDateTimeOf(tagged.ticks) : refuse();
        case 'dateOnly':
        case 'dateTimeOffset': return refuse();
        default: throw new Error(`Internal: ${String(tagged satisfies never)} is not an operand.`);
    }
}

// The View State's reader puts only tagged operands in a filter it reads.
function isOperand(value: unknown): value is ViewStateOperand {
    return typeof value === 'object' && value !== null && typeof Reflect.get(value, 'type') === 'string';
}

function numeric(kind: SnapshotKind): boolean {
    return kind === 'decimal' || kind === 'double' || kind === 'integer';
}
