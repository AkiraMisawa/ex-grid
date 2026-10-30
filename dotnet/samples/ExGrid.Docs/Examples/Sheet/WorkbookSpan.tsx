import { useMemo, useState, type ReactElement } from 'react';
import { ExSheet } from '@ex-grid/sheet-react';
import { quarters } from './Quarters';

export default function WorkbookSpan(): ReactElement {
    // Year reads Jan to Mar with References such as Jan:Mar!B1.
    const opened = useMemo(quarters, []);
    const [said, setSaid] = useState(() => `Sheets: ${opened.sheets.map(sheet => sheet.name).join(', ')}.`);
    // Heard when a Sheet is added, deleted, renamed, moved or copied, with the names before and after: an Entry
    // typed does not render the Example again.
    return (
        <>
            <p className="first">{said}</p>
            <ExSheet document={opened} onSheetsChanged={change => { setSaid(`Sheets: ${change.after.join(', ')}.`); }}
                viewportHeight={240} viewportWidth="stretch" />
        </>
    );
}
