// A grid's filter as its source executes it, made the View State's again, each operand carrying the type ExBoard's C#
// grid compares the column with (ADR-0207): the inverse of the core's engineFilterOf, so that what the user filtered in
// a running Board is written in its Diff as ExBoard writes it.

import type { FilterClause, GridFilter, ViewStateOperand } from '@ex-grid/core';
import { ticksOfTemporal, type SnapshotDecimal, type SnapshotKind } from '@ex-grid/data';

/**
 * `filter`, each operand made the {@link @ex-grid/core#ViewStateOperand} of the type a column of its kind holds in
 * ExBoard's C# grid: text a String, a Decimal a Decimal, a Double a Double, an Integer an Int64, a Boolean a Boolean,
 * and a Date an Unspecified DateTime. A Blank among In's operands stays `null`. An operand that is not of its column's
 * kind is a defect of whoever set it, and is refused with a `TypeError`.
 */
export function taggedFilter(filter: GridFilter | undefined, kindOf: (column: string) => SnapshotKind | undefined): GridFilter | undefined {
    if (filter === undefined) return undefined;
    const specs = Object.entries(filter.columns).map(([name, spec]) => {
        const kind = kindOf(name);
        if (kind === undefined) throw new TypeError(`The filter is on column '${name}', which the grid does not show.`);
        const clauses = spec.clauses.map((clause): FilterClause => {
            const { value, values } = clause;
            return {
                ...clause,
                ...(value === undefined ? {} : { value: tagged(value, kind, name) }),
                ...(values === undefined ? {} : { values: values.map(one => one === null ? null : tagged(one, kind, name)) }),
            };
        });
        return [name, { ...spec, clauses }] as const;
    });
    return { ...filter, columns: Object.fromEntries(specs) };
}

function tagged(value: unknown, kind: SnapshotKind, column: string): ViewStateOperand {
    const refuse = (): never => {
        throw new TypeError(`Column '${column}' is ${kind}, and its filter holds ${typeof value === 'object' ? Object.prototype.toString.call(value) : typeof value}.`);
    };
    switch (kind) {
        case 'text': return typeof value === 'string' ? { type: 'string', value } : refuse();
        case 'boolean': return typeof value === 'boolean' ? { type: 'boolean', value } : refuse();
        case 'double': return typeof value === 'number' ? { type: 'double', value } : refuse();
        case 'integer': {
            if (typeof value === 'bigint') return { type: 'int64', value };
            return typeof value === 'number' && Number.isSafeInteger(value) ? { type: 'int64', value: BigInt(value) } : refuse();
        }
        case 'decimal': {
            if (typeof value === 'bigint') return { type: 'decimal', value: { mantissa: value, scale: 0 } };
            return typeof value === 'number' && Number.isFinite(value) ? { type: 'decimal', value: decimalOfNumber(value) } : refuse();
        }
        case 'date': {
            const ticks = typeof value === 'object' && value !== null ? ticksOfTemporal(value) : undefined;
            return ticks === undefined ? refuse() : { type: 'dateTime', ticks, kind: 'unspecified' };
        }
        default: throw new Error(`Internal: ${String(kind satisfies never)} is not a kind.`);
    }
}

// A number as the Decimal its shortest text names: 12.5 as 12.5, 1e-7 as 0.0000001.
function decimalOfNumber(value: number): SnapshotDecimal {
    const [mantissa = '', exponent = ''] = value.toExponential().split('e');
    const digits = mantissa.replace('.', '').replace('-', '');
    const places = digits.length - 1 - Number(exponent);
    if (places > 28) throw new TypeError(`${value} has more places than a Decimal holds (28).`);
    const whole = BigInt((value < 0 ? '-' : '') + digits);
    return places >= 0 ? { mantissa: whole, scale: places } : { mantissa: whole * 10n ** BigInt(-places), scale: 0 };
}
