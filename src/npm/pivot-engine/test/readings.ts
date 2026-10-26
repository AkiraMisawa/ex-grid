// How a case reads a report back, as the C# runner (tests/ExPivot.Engine.Tests/Cases/PivotReportCaseTests.cs)
// reads one: its leaf headers, a line a row, its Header Group spans, and the readings `also` asks for.
import { decimalText } from '@ex-grid/data';
import type { PivotItemKey, PivotReport, PivotRowKey, PivotToggle } from '../src/index';
import { invariantNumbers } from '../src/text/invariant';
import { formatDouble } from '../src/text/number-text';
import type { DataSet, Json, JsonObject } from './cases';

const roleLetters = { item: 'i', group: 'g', subtotal: 's', grandTotal: 't', pending: 'p' } as const;

// One line a row: the role's letter, each label indented two spaces a level with [-] or [+] before an Item
// with a button, then || and each value as painted, empty for an empty cell.
function lines(report: PivotReport): string[] {
    return report.rows.map(row => {
        const labels = row.labels.map(label => {
            const button = label.toggle === undefined ? '' : label.toggle.isCollapsed ? '[+]' : '[-]';
            return ' '.repeat(label.indent * 2) + button + (label.text ?? ''); // oxlint-disable-line ex-grid/no-stand-in-default -- a label with no text reads as nothing in a line, as the C# runner writes it
        });
        // A Pending Row has no value to read (ADR-0370 section 3).
        const values = row.role === 'pending' ? [] : report.valueColumns.map((_, column) => report.valueAt(row, column)?.text ?? ''); // oxlint-disable-line ex-grid/no-stand-in-default -- an empty cell reads as nothing in a line, as the C# runner writes it
        return `${roleLetters[row.role]} ${labels.join(' | ')} || ${values.join(' | ')}`.trimEnd();
    });
}

function headers(report: PivotReport): string[] {
    return [...report.labelColumns.map(c => c.header), ...report.valueColumns.map(c => c.header)];
}

function spans(report: PivotReport): string[] {
    return report.headerSpans
        .toSorted((a, b) => (b.tier === a.tier ? a.firstColumn - b.firstColumn : b.tier - a.tier))
        .map(s => `${s.label}@${s.firstColumn}+${s.columnCount} t${s.tier}x${s.tierSpan}`);
}

// A double as a case writes one: "d" and its shortest round-trip text, which JSON numbers cannot always carry.
function double(value: number): string {
    return `d${formatDouble(value, 'R', invariantNumbers)}`;
}

export function itemKey(key: PivotItemKey): JsonObject {
    return key.value === undefined ? { kind: key.kind } : { kind: key.kind, value: key.value };
}

function rowKey(key: PivotRowKey): JsonObject {
    return { role: key.role, valueField: key.valueField, items: key.items.map(itemKey) };
}

function toggle(given: PivotToggle | undefined): Json {
    return given === undefined
        ? null
        : { field: given.field, item: itemKey(given.item), itemLabel: given.itemLabel, isCollapsed: given.isCollapsed };
}

function numbers(report: PivotReport): JsonObject {
    const rows = report.rows.map(row => ({
        labels: row.labels.map(label => (label.number === undefined ? null : double(label.number))),
        values: report.valueColumns.map((_, column): Json => {
            const value = report.valueAt(row, column);
            if (value === undefined) return null;
            if (value.error !== undefined) return value.error;
            if (value.exact !== undefined) return `m${decimalText(value.exact)}`;
            return double(value.number);
        }),
    }));
    return { rows, headers: report.valueColumns.map(c => (c.headerNumber === undefined ? null : double(c.headerNumber))) };
}

function reading(report: PivotReport, more: string, data: DataSet): Json {
    switch (more) {
        case 'names': return [...report.labelColumns.map(c => c.name), ...report.valueColumns.map(c => c.name)];
        case 'keys': return report.rows.map(r => rowKey(r.key));
        case 'numbers': return numbers(report);
        case 'captions': return [...report.valueCaptions];
        case 'empty': return report.isEmpty;
        case 'toggles': return report.rows.map(r => r.labels.map(l => toggle(l.toggle)));
        case 'columns': return report.valueColumns.map(c => ({ role: c.role, valueField: c.valueField }));
        case 'tiers': return report.headerTierCount;
        case 'keyCalls': return data.keyCalls.toSorted(ordinal);
        default: throw new Error(`'${more}' is not a reading of a report the cases name.`);
    }
}

function ordinal(a: string, b: string): number {
    return a < b ? -1 : a > b ? 1 : 0;
}

/** Every reading of a report that no data set is needed for. */
export const everyReading: readonly string[] = ['names', 'keys', 'numbers', 'captions', 'empty', 'toggles', 'columns', 'tiers'];

export function read(report: PivotReport, also: readonly string[], data: DataSet): Record<string, Json> {
    const answer: Record<string, Json> = { headers: headers(report), lines: lines(report), spans: spans(report) };
    for (const more of also) answer[more] = reading(report, more, data);
    return answer;
}
