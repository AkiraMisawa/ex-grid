import { useEffect, useMemo, useRef, useState, type ReactElement } from 'react';
import type { GridColumn } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { PointingScope, type PointingRefusal } from '@ex-grid/sheet';
import { RepeatedKeyError } from '@ex-grid/sheet-engine';
import { ExSheet, type ExSheetApi } from '@ex-grid/sheet-react';
import { costCentreColumns, costCentreKey, costCentreRow, costCentres, costCentresTable, type CostCentre } from './CostCentre';
import { costReport } from './CostReport';

const amount = (value: unknown): string => (typeof value === 'number' ? value.toLocaleString('en-US') : '');

// Named as the table's columns, so the Sheet's outlines land on the grid's columns.
const columns: readonly GridColumn<CostCentre>[] = [
    { name: 'Code', type: 'text', value: centre => centre.code },
    { name: 'Department', type: 'text', value: centre => centre.department },
    { name: 'Budget', type: 'number', value: centre => centre.budget, format: amount },
    { name: 'Actual', type: 'number', value: centre => centre.actual, format: amount },
];

export default function LinkedTable(): ReactElement {
    const document = useMemo(costReport, []);
    const sheet = useRef<ExSheetApi>(null);
    const [centres, setCentres] = useState(costCentres);
    const [said, setSaid] = useState('');

    // The Sheet and the grid that shows the table: while a Formula is typed, a press on the grid writes what reads
    // the pressed cell by its row's key.
    // Made together, so that a memo React makes twice and drops leaves no grid registered in the Scope it keeps.
    const [scope, pointedAt] = useMemo(() => {
        const made = new PointingScope();
        return [made, made.registerGrid<CostCentre>(costCentresTable, costCentreRow)] as const;
    }, []);

    const api = (): ExSheetApi => {
        const current = sheet.current;
        if (current === null) throw new Error('The Sheet is not mounted.');
        return current;
    };

    // The document already declares the table; declaring it again changes nothing. Every push is a whole snapshot.
    useEffect(() => {
        api().declareLinkedTable(costCentresTable, costCentreColumns, costCentreKey);
        api().pushLinkedTable(costCentresTable, centres.map(costCentreRow));
    }, [centres]);

    const postAccruals = (): void => {
        setCentres(centres.map(centre => ({ ...centre, actual: Math.round(centre.actual * 1.015) })));
        setSaid('Accruals posted.');
    };

    // Refused: its readers wait again rather than show the last snapshot's numbers.
    const pushRepeatedKey = (): void => {
        const first = centres[0];
        if (first === undefined) throw new Error('The ledger holds no cost centre.');
        try {
            api().pushLinkedTable(costCentresTable, [...centres.map(costCentreRow), costCentreRow(first)]);
        } catch (error) {
            if (!(error instanceof RepeatedKeyError)) throw error;
            setSaid(`Refused: ${error.key.text} appears twice in ${error.keyColumn}. The report waits for the next snapshot.`);
        }
    };

    return (
        <>
            <div className="bar">
                <button type="button" onClick={postAccruals}>Post accruals</button>
                <button type="button" onClick={pushRepeatedKey}>Push a snapshot with a repeated code</button>
                <span>{said}</span>
            </div>
            <ExGrid rows={centres} columns={columns} pointedAt={pointedAt} viewportHeight={200} viewportWidth="stretch" />
            <div style={{ marginTop: 24 }}>
                <ExSheet ref={sheet} document={document} pointingScope={scope} onPointingRefused={(refusal: PointingRefusal) => { setSaid(refusal.message); }}
                    viewportHeight={280} viewportWidth="stretch" />
            </div>
        </>
    );
}
