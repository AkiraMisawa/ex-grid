import { useMemo, useState, type ReactElement } from 'react';
import type { WorkbookDocument } from '@ex-grid/sheet-engine';
import { ExSheet } from '@ex-grid/sheet-react';
import { expenses } from './Expenses';

export default function FirstSheet(): ReactElement {
    // The document is yours: ExSheet opens on it, and hands a new one back after every change.
    const opened = useMemo(expenses, []);
    const [document, setDocument] = useState<WorkbookDocument>(opened);
    const cells = document.sheets.reduce((count, sheet) => count + sheet.cells.length, 0);
    return (
        <>
            <ExSheet document={opened} onWorkbookChanged={setDocument} viewportHeight={300} viewportWidth="stretch" />
            <p>The document holds {cells} cells. Type into an empty one and the count follows.</p>
        </>
    );
}
