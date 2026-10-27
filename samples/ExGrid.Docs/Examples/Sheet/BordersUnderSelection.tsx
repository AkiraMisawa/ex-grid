import { useMemo, type ReactElement } from 'react';
import { ExSheet } from '@ex-grid/sheet-react';
import { quarterlyPnl } from './QuarterlyPnl';

export default function BordersUnderSelection(): ReactElement {
    // Column A stays in place as the rest scrolls, as a frozen pane does in Excel.
    const document = useMemo(quarterlyPnl, []);
    return <ExSheet document={document} pinnedColumnCount={1} viewportHeight={300} viewportWidth="stretch" />;
}
