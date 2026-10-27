import { useCallback, useState, type ReactElement } from 'react';
import { Button, Stack, Typography } from '@mui/material';
import { GridSource, type GridColumn, type GridEditIntent, type GridReplacedRow, type GridReplaceRequest, type GridReplaceResult, type GridReplaceWrite } from '@ex-grid/core';
import { MuiExGrid } from '@ex-grid/mui';
import { sampleTrades, type Trade } from './Trades';

const source = GridSource.from(sampleTrades(2_000));

// Trader and Price are Editable: Replace writes there, and a match anywhere else refuses Replace All.
const columns: readonly GridColumn<Trade>[] = [
    { name: 'Id', type: 'number', value: t => t.id },
    { name: 'Book', type: 'text', value: t => t.book },
    { name: 'Trader', type: 'text', value: t => t.trader, editable: true },
    { name: 'Instrument', type: 'text', value: t => t.instrument },
    { name: 'Price', type: 'number', value: t => t.price, editable: true },
];

// A price as typed: digits, an optional sign, thousands separators and a decimal point. Anything else is not one.
const price = /^[+-]?(\d{1,3}(,\d{3})+|\d+)(\.\d+)?$/;

// A new row with one value changed, or a refusal in your words.
function write(row: Trade, column: string, text: string): GridReplaceWrite<Trade> {
    if (column === 'Trader') return { row: { ...row, trader: text } };
    if (column === 'Price' && price.test(text.trim())) return { row: { ...row, price: Number(text.trim().replaceAll(',', '')) } };
    return { refusal: `'${text}' is not a price` };
}

function said(request: GridReplaceRequest, result: GridReplaceResult): string {
    if (result.rowSequenceVersion !== request.rowSequenceVersion) return 'Nothing replaced: the rows moved while it was asked.';
    return result.kind === 'replaced'
        ? `Replaced ${String(result.count)} cells in one step.`
        : `Nothing replaced: ${String(result.cells.length)} cells could not be written.`;
}

// One step per gesture, an edit or a whole Replace All, so one Ctrl+Z undoes it. Kept beside the source it undoes.
const undo: (readonly GridReplacedRow<Trade>[])[] = [];

export default function FindReplaceMui(): ReactElement {
    const [steps, setSteps] = useState(undo.length);
    const [message, setMessage] = useState('Press Ctrl+H in the grid: replace Novak with Rossi, one at a time or all at once.');
    // Made once: a handler made again on every render would be handed to the grid again each time.
    // Replace (one) is an ordinary edit of the current match.
    const onEdit = useCallback((edit: GridEditIntent<Trade>) => {
        const written = write(edit.row, edit.column, edit.value);
        if ('refusal' in written) {
            edit.refuse(written.refusal);
            return;
        }
        source.replaceRow(edit.row, written.row);
        undo.push([{ before: edit.row, after: written.row }]);
        setSteps(undo.length);
    }, []);
    // Replace All: GridSource.from finds every match and writes them all, or none, through your write.
    const onReplace = useCallback((request: GridReplaceRequest) => {
        const answer = source.replace(request, write);
        if (answer.changes.length > 0) undo.push(answer.changes);
        setSteps(undo.length);
        setMessage(said(request, answer.result));
        return answer.result;
    }, []);
    const onUndo = useCallback(() => {
        const step = undo.at(-1);
        if (step === undefined) return;
        for (const change of [...step].reverse()) source.replaceRow(change.after, change.before);
        undo.pop();
        setSteps(undo.length);
        setMessage(`Undone: ${String(step.length)} rows.`);
    }, []);
    return (
        <>
            <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', mb: 1.5 }}>
                <Button size="small" variant="outlined" disabled={steps === 0} onClick={onUndo}>Undo</Button>
                <Typography variant="body2">{message}</Typography>
            </Stack>
            <MuiExGrid source={source} columns={columns} onEdit={onEdit} onReplace={onReplace} onUndo={onUndo}
                viewportHeight={320} viewportWidth="stretch" />
        </>
    );
}
