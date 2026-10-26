import { useMemo, useRef, useState, type ReactElement } from 'react';
import type { WorkbookDocument } from '@ex-grid/sheet-engine';
import { ExSheet, type ExSheetApi } from '@ex-grid/sheet-react';
import { expensesWorkbook } from './Expenses';

export default function FindReplace(): ReactElement {
    // The claim, and a summary that reads it.
    const opened = useMemo(expensesWorkbook, []);
    const sheet = useRef<ExSheetApi>(null);
    const [said, setSaid] = useState('');

    // Ctrl+F and Ctrl+H are the Sheet's own: there is nothing to wire. A Replace All over the Workbook arrives here
    // once, however many Sheets it wrote on.
    const changed = (workbook: WorkbookDocument): void => {
        setSaid(`onWorkbookChanged: ${String(workbook.sheets.reduce((count, s) => count + s.cells.length, 0))} cells hold an Entry.`);
    };

    // A Replace All is one step: one Undo takes it back on every Sheet. With nothing to undo, the Sheet says so.
    const undo = (): void => {
        const api = sheet.current;
        if (api === null) throw new Error('The Sheet is not mounted.');
        if (api.undo()) setSaid('Undone.');
    };

    return (
        <>
            <div className="bar">
                <button type="button" onClick={undo}>Undo</button>
                <span>{said}</span>
            </div>
            <ExSheet ref={sheet} document={opened} onWorkbookChanged={changed} viewportHeight={280} viewportWidth="stretch" />
        </>
    );
}
