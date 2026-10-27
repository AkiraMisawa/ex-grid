import { useMemo, type ReactElement } from 'react';
import { MantineExSheet } from '@ex-grid/sheet-mantine';
import { quarterlyPnl } from './QuarterlyPnl';

// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css and
// @ex-grid/sheet-mantine/styles.css loaded: the Sheet in Mantine's Paper, its toolbar, Format Cells (Ctrl+1), tabs,
// menus and panels Mantine's components, its Chrome's colours the theme's. The cells keep the document's own look.
export default function CellFormatsMantine(): ReactElement {
    // Number formats, fonts, fills and borders travel in the document with the Entries.
    const document = useMemo(quarterlyPnl, []);
    return <MantineExSheet withBorder document={document} viewportHeight={300} viewportWidth="stretch" />;
}
