import { useMemo, type ReactElement } from 'react';
import { MuiExSheet } from '@ex-grid/sheet-mui';
import { quarterlyPnl } from './QuarterlyPnl';

// Under a ThemeProvider whose theme is made with createTheme({ cssVariables: true }), with @ex-grid/mui/styles.css and
// @ex-grid/sheet-mui/styles.css loaded: the Sheet in MUI's Paper, its toolbar, Format Cells (Ctrl+1), tabs, menus and
// panels MUI's components, its Chrome's colours the theme's. The cells keep the document's own look.
export default function CellFormatsMui(): ReactElement {
    // Number formats, fonts, fills and borders travel in the document with the Entries.
    const document = useMemo(quarterlyPnl, []);
    return <MuiExSheet document={document} viewportHeight={300} viewportWidth="stretch" />;
}
