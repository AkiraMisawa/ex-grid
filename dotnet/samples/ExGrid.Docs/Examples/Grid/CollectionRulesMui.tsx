import { useCallback, useState, type ReactElement } from 'react';
import { Button, Stack, Typography } from '@mui/material';
import {
    GridRuleset, GridSource, type GridColumn, type GridEditIntent, type GridErrorStepRequest, type GridFindResult, type GridPasteIntent,
} from '@ex-grid/core';
import { MuiExGrid } from '@ex-grid/mui';
import { oneCurvePerCsa, sampleCurveTrades, type CurveTrade } from './CurveTrades';

const trades = sampleCurveTrades();
const source = GridSource.from(trades);
const columns: readonly GridColumn<CurveTrade>[] = [
    { name: 'Id', type: 'text', value: t => t.id, width: 80 },
    { name: 'Csa', type: 'text', value: t => t.csa, header: 'CSA', width: 90 },
    { name: 'Counterparty', type: 'text', value: t => t.counterparty, width: 140 },
    { name: 'Curve', type: 'text', value: t => t.curve, header: 'Discount curve', width: 140, editable: true },
];
// Keyed by the trade id, the Row Key: a Breach names trades wherever they stand, on screen or not.
const rules = new GridRuleset<CurveTrade>(t => t.id).set(oneCurvePerCsa);
rules.evaluate(trades, columns);

// F8 and Shift+F8 step through the error cells in the order the grid shows, the marks scrolled out of view included.
async function findError(request: GridErrorStepRequest, signal: AbortSignal): Promise<GridFindResult> {
    const count = source.snapshot().totalCount;
    if (count === undefined) throw new Error('The rows are not counted yet.');
    return rules.findError(request, await source.rows({ start: 0, count }, signal));
}

// The rows a paste lands on in the Curve column, each once with the text it writes: ranges of a selection may
// overlap, and a row replaced once is no longer in the source to be replaced again.
function curveWrites(paste: GridPasteIntent): (readonly [CurveTrade, string])[] {
    const { window, windowStart } = source.snapshot();
    const curve = columns.findIndex(column => column.name === 'Curve');
    const texts = new Map<number, string>();
    for (const range of paste.plan.targets.filter(r => curve >= r.leftColumn && curve < r.leftColumn + r.columnCount)) {
        for (let row = range.topRow; row < range.topRow + range.rowCount; row++) texts.set(row, paste.valueFor({ row, column: curve }));
    }
    return [...texts].map(([row, text]) => {
        const trade = window[row - windowStart];
        if (trade === undefined) throw new RangeError(`The paste names row ${String(row)}, which the source does not hold.`);
        return [trade, text] as const;
    });
}

export default function CollectionRulesMui(): ReactElement {
    // The ruleset hands the grid new functions after every judgement: kept in state, so the grid paints them.
    const [paint, setPaint] = useState(() => ({ cellState: rules.cellState, cellMessage: rules.cellMessage }));
    const [message, setMessage] = useState('CSA ABC discounts on USD-SOFR. Move its trades to USD-FF one by one.');
    // Made once: a handler made again on every render would be handed to the grid again each time.
    const apply = useCallback((writes: readonly (readonly [CurveTrade, string])[]) => {
        for (const [row, curve] of writes) {
            const replacement = { ...row, curve: curve.trim() };
            source.replaceRow(row, replacement);
            const at = trades.findIndex(t => t.id === row.id);
            if (at < 0) throw new RangeError(`Trade ${row.id} is not in the collection.`);
            trades[at] = replacement;
        }
        // The rule is judged over the whole collection after every apply, never at the editor: the edit lands.
        rules.evaluate(trades, columns);
        setPaint({ cellState: rules.cellState, cellMessage: rules.cellMessage });
        setMessage(rules.breaches.length === 0 ? 'Every CSA has one curve.' : `${String(rules.breaches.length)} breach(es) stand.`);
    }, []);
    const onEdit = useCallback((edit: GridEditIntent<CurveTrade>) => { apply([[edit.row, edit.value]]); }, [apply]);
    // Ctrl+Enter over a selection fills every cell at once: the one gesture that moves a whole CSA in a step.
    const onPaste = useCallback((paste: GridPasteIntent) => { apply(curveWrites(paste)); }, [apply]);
    // The refusal belongs to the save, as a database's deferred constraint is checked at commit.
    const onSave = useCallback(() => {
        const first = rules.breaches[0];
        setMessage(first === undefined ? 'Saved.' : `Not saved: ${first.message}`);
    }, []);
    return (
        <>
            <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', mb: 1.5 }}>
                <Button size="small" variant="outlined" onClick={onSave}>Save</Button>
                <Typography variant="body2">{message}</Typography>
            </Stack>
            <MuiExGrid source={source} columns={columns} pinnedColumnCount={1} cellState={paint.cellState} cellMessage={paint.cellMessage}
                findError={findError} onEdit={onEdit} onPaste={onPaste} viewportHeight={300} viewportWidth="stretch" />
        </>
    );
}
