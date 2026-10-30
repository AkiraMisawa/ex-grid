import { useCallback, useState, type ReactElement } from 'react';
import { Typography } from '@mui/material';
import {
    GridSource, copyRefusalLabelIds, pasteRefusalLabelIds, refusalColumnPlaceholder, type GridColumn, type GridPasteIntent, type GridRefusal,
} from '@ex-grid/core';
import { MuiExGrid } from '@ex-grid/mui';
import { samplePositions, type Position } from './Positions';

const source = GridSource.from(samplePositions(500));
const whole = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const money = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const columns: readonly GridColumn<Position>[] = [
    { name: 'Id', type: 'number', value: p => p.id, width: 70, align: 'left' },
    { name: 'Desk', type: 'text', value: p => p.desk, width: 100 },
    { name: 'Instrument', type: 'text', value: p => p.instrument, width: 120 },
    { name: 'Quantity', type: 'number', value: p => p.quantity, width: 110, editable: true, format: v => whole.format(v as number) },
    { name: 'Price', type: 'number', value: p => p.price, width: 100, editable: true, format: v => money.format(v as number) },
    { name: 'Pnl', type: 'number', value: p => p.pnl, header: 'P&L', width: 130, format: v => money.format(v as number) },
];

// The grid says every refusal itself, in its live region. These words replace its own for two reasons;
// undefined keeps the grid's words for the rest, and '' would silence one.
const words = (id: string): string | undefined => {
    if (id === copyRefusalLabelIds.tooLarge) return 'Not copied: that is more than 2,000 cells. Nothing was put on the clipboard — export instead.';
    if (id === pasteRefusalLabelIds.targetNotEditable) {
        return `Not pasted: only Quantity and Price take a paste, and the selection covers ${refusalColumnPlaceholder}.`;
    }
    return undefined;
};

// A number as Excel copies one: an optional sign, thousands separators and, for a price, a decimal point.
const quantity = /^[+-]?(\d{1,3}(,\d{3})+|\d+)$/;
const price = /^[+-]?(\d{1,3}(,\d{3})+|\d+)(\.\d+)?$/;
const numberOf = (text: string): number => Number(text.trim().replaceAll(',', ''));

// A new row with one value changed, or undefined where the text is not a number for that column.
function written(row: Position, column: string, text: string): Position | undefined {
    if (column === 'Quantity' && quantity.test(text.trim())) return { ...row, quantity: numberOf(text) };
    if (column === 'Price' && price.test(text.trim())) return { ...row, price: numberOf(text) };
    return undefined;
}

export default function ClipboardGridMui(): ReactElement {
    const [message, setMessage] = useState('Select cells and press Ctrl+C, then paste into Excel — or copy from Excel and paste into Quantity and Price.');
    const [refused, setRefused] = useState('');
    // Made once: a handler made again on every render would be handed to the grid again each time.
    // The event still names the rule, said or not: here, to show which one.
    const onRefused = useCallback((refusal: GridRefusal<Position>) => {
        if (refusal.kind === 'copy') setRefused(`Copy refused: ${refusal.reason}.`);
        if (refusal.kind === 'paste') setRefused(`Paste refused: ${refusal.reason}.`);
    }, []);
    // One intent for the whole paste. Each cell is resolved to its row before any row is replaced.
    const onPaste = useCallback((paste: GridPasteIntent) => {
        const { window, windowStart } = source.snapshot();
        const writes = paste.plan.targets.flatMap(range => Array.from({ length: range.rowCount }, (_, r) => range.topRow + r).flatMap(row =>
            Array.from({ length: range.columnCount }, (_, c) => range.leftColumn + c).map(column => {
                const at = window[row - windowStart];
                const name = columns[column]?.name;
                if (at === undefined || name === undefined) throw new RangeError(`The paste names a cell the source does not hold: ${String(row)}, ${String(column)}.`);
                return { row: at, column: name, text: paste.valueFor({ row, column }) };
            })));
        const latest = new Map<Position, Position>();
        let count = 0;
        for (const { row, column, text } of writes) {
            const current = latest.get(row) ?? row;
            const replacement = written(current, column, text);
            if (replacement === undefined) continue;
            source.replaceRow(current, replacement);
            latest.set(row, replacement);
            count += 1;
        }
        setMessage(`Pasted ${whole.format(count)} of ${whole.format(writes.length)} cells; ${whole.format(writes.length - count)} were not numbers.`);
    }, []);
    return (
        <>
            <MuiExGrid source={source} columns={columns} pinnedColumnCount={1} copyCellCap={2_000} onPaste={onPaste} onRefused={onRefused}
                commandLabel={words} viewportHeight={300} viewportWidth="stretch" />
            <Typography role="status" variant="body2" sx={{ mt: 1.5 }}>{message}</Typography>
            {/* Not a live region: the grid has said the refusal already, and this only shows it. */}
            <Typography variant="body2" sx={{ mt: 0.5 }}>{refused}</Typography>
        </>
    );
}
