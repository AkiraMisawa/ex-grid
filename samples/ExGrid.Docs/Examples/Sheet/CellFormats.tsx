import { useMemo, type ReactElement } from 'react';
import { ExSheet } from '@ex-grid/sheet-react';
import { quarterlyPnl } from './QuarterlyPnl';

export default function CellFormats(): ReactElement {
    // Number formats, fonts, fills and borders travel in the document with the Entries.
    const document = useMemo(quarterlyPnl, []);
    return <ExSheet document={document} viewportHeight={300} viewportWidth="stretch" />;
}
