import { useCallback, useState, type ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { Group, Switch, Text } from '@mantine/core';
import { GridSource, defaultSummaryFigures, type GridColumn, type SelectionSummary, type SummaryFigure } from '@ex-grid/core';
import { MantineExGrid } from '@ex-grid/mantine';
import { sampleTrades, type Trade } from './Trades';

const source = GridSource.from(sampleTrades());
const notional = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
// Fifteen significant digits, as the grid's own line shows a figure: a Sum of prices keeps its third decimal.
const figure = new Intl.NumberFormat('en-US', { maximumSignificantDigits: 15 });

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Id', type: 'number', value: t => t.id },
    { name: 'Book', type: 'text', value: t => t.book },
    { name: 'Trader', type: 'text', value: t => t.trader },
    { name: 'Currency', type: 'text', value: t => t.currency },
    { name: 'Notional', type: 'number', value: t => t.notional, format: v => notional.format(v as number) },
    { name: 'Price', type: 'number', value: t => t.price },
    { name: 'TradeDate', type: 'date', value: t => t.tradeDate, header: 'Trade date', dateType: 'dateOnly' },
];

// The same figures for a status bar of the application's own. A figure is only ever the answer to the selection
// standing now, so a pending one says nothing rather than the last selection's total.
function describe(summary: SelectionSummary): string {
    switch (summary.status) {
        case 'none': return 'select two cells or more';
        case 'pending': return '…';
        case 'declined':
            if (summary.result?.kind !== 'declined') throw new Error('A declined Selection Summary carries its reason.');
            return `not summarised: ${summary.result.reason}`;
        case 'answered': {
            if (summary.result?.kind !== 'answered') throw new Error('An answered Selection Summary carries its figures.');
            const sum = summary.result.figures.sum;
            if (sum === undefined) return 'Sum is not shown';
            switch (sum.kind) {
                case 'number': return `Sum ${figure.format(sum.number)}`;
                case 'empty': return 'no numbers selected';
                case 'error': return `Sum is an error: ${sum.error}`;
            }
        }
    }
}

// On by default; the figures menu (right-click the status line) hands the choice back here, and the grid shows what
// it is handed back.
export default function SelectionFiguresMantine(): ReactElement {
    const [show, setShow] = useState(true);
    const [figures, setFigures] = useState<readonly SummaryFigure[]>(defaultSummaryFigures);
    const [elsewhere, setElsewhere] = useState('select two cells or more');
    // Made once: a handler made again on every render would be handed to the grid again each time.
    const onSelectionSummaryChanged = useCallback((summary: SelectionSummary) => { setElsewhere(describe(summary)); }, []);
    return (
        <>
            <Group justify="space-between" mb="sm">
                <Switch label="Status-bar figures" checked={show} onChange={event => { setShow(event.currentTarget.checked); }} />
                <Text size="sm">Your own status bar: {elsewhere}</Text>
            </Group>
            <MantineExGrid withBorder source={source} columns={columns} temporal={Temporal} showSelectionSummary={show} summaryFigures={figures}
                onSummaryFiguresChanged={setFigures} onSelectionSummaryChanged={onSelectionSummaryChanged}
                viewportHeight={300} viewportWidth="stretch" />
        </>
    );
}
