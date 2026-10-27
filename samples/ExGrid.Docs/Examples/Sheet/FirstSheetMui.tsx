import { useMemo, useState, type ReactElement } from 'react';
import type { WorkbookDocument } from '@ex-grid/sheet-engine';
import { MuiExSheet } from '@ex-grid/sheet-mui';
import { expenses } from './Expenses';

// Under a ThemeProvider whose theme is made with createTheme({ cssVariables: true }), with @ex-grid/mui/styles.css and
// @ex-grid/sheet-mui/styles.css loaded: the Sheet in MUI's Paper, its toolbar, Format Cells (Ctrl+1), tabs, menus and
// panels MUI's components, its Chrome's colours the theme's. The cells keep the document's own look.
export default function FirstSheetMui(): ReactElement {
    // The document is yours: ExSheet opens on it, and hands a new one back after every change.
    const opened = useMemo(expenses, []);
    const [document, setDocument] = useState<WorkbookDocument>(opened);
    const cells = document.sheets.reduce((count, sheet) => count + sheet.cells.length, 0);
    return (
        <>
            <MuiExSheet document={opened} onWorkbookChanged={setDocument} viewportHeight={300} viewportWidth="stretch" />
            <p>The document holds {cells} cells. Type into an empty one and the count follows.</p>
        </>
    );
}
