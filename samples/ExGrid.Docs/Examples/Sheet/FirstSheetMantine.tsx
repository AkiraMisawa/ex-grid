import { useMemo, useState, type ReactElement } from 'react';
import type { WorkbookDocument } from '@ex-grid/sheet-engine';
import { MantineExSheet } from '@ex-grid/sheet-mantine';
import { expenses } from './Expenses';

// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css and
// @ex-grid/sheet-mantine/styles.css loaded: the Sheet in Mantine's Paper, its toolbar, Format Cells (Ctrl+1), tabs,
// menus and panels Mantine's components, its Chrome's colours the theme's. The cells keep the document's own look.
export default function FirstSheetMantine(): ReactElement {
    // The document is yours: ExSheet opens on it, and hands a new one back after every change.
    const opened = useMemo(expenses, []);
    const [document, setDocument] = useState<WorkbookDocument>(opened);
    const cells = document.sheets.reduce((count, sheet) => count + sheet.cells.length, 0);
    return (
        <>
            <MantineExSheet withBorder document={opened} onWorkbookChanged={setDocument} viewportHeight={300} viewportWidth="stretch" />
            <p>The document holds {cells} cells. Type into an empty one and the count follows.</p>
        </>
    );
}
