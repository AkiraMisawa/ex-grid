import { useMemo, useState, type ReactElement } from 'react';
import type { WorkbookDocument } from '@ex-grid/sheet-engine';
import { ExSheet } from '@ex-grid/sheet-react';
import { quarters } from './Quarters';

export default function WorkbookSpan(): ReactElement {
    // Year reads Jan to Mar with References such as Jan:Mar!B1.
    const opened = useMemo(quarters, []);
    const [names, setNames] = useState(() => opened.sheets.map(sheet => sheet.name));
    // Every change to the Workbook, a Sheet moved, added or renamed among them, hands back the whole document: the
    // names are taken only when they changed, so an Entry typed does not render the Example again.
    const changed = (document: WorkbookDocument): void => {
        const next = document.sheets.map(sheet => sheet.name);
        setNames(shown => (next.length === shown.length && next.every((name, at) => name === shown[at]) ? shown : next));
    };
    return (
        <>
            <p className="first">Sheets: {names.join(', ')}.</p>
            <ExSheet document={opened} onWorkbookChanged={changed} viewportHeight={240} viewportWidth="stretch" />
        </>
    );
}
