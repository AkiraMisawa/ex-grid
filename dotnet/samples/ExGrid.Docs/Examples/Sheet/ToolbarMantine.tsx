import { useMemo, useRef, useState, type ReactElement } from 'react';
import { CellColour, CellFill, CellFormatChange } from '@ex-grid/sheet-engine';
import type { ExSheetApi, ExSheetToolbarRow } from '@ex-grid/sheet-react';
import { MantineExSheet } from '@ex-grid/sheet-mantine';
import { quarterlyPnl } from './QuarterlyPnl';

// An icon in the toolbar's own colour.
function Tick(): ReactElement {
    return (
        <svg width="12" height="12" viewBox="0 0 12 12" aria-hidden="true">
            <path d="M2 6.5 5 9.5 10 3" fill="none" stroke="currentColor" strokeWidth="1.5" />
        </svg>
    );
}

// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css and
// @ex-grid/sheet-mantine/styles.css loaded: the Sheet in Mantine's Paper, its toolbar, Format Cells (Ctrl+1), tabs,
// menus and panels Mantine's components, its Chrome's colours the theme's. The cells keep the document's own look.
export default function ToolbarMantine(): ReactElement {
    const opened = useMemo(quarterlyPnl, []);
    const sheet = useRef<ExSheetApi>(null);
    // Yours to keep: Ctrl+F1 hides and shows the toolbar through onShowToolbarChanged.
    const [toolbar, setToolbar] = useState(true);

    const fill = (rgb: number, bold?: boolean): void => {
        const api = sheet.current;
        if (api === null) throw new Error('The Sheet is not mounted.');
        api.setCellFormat(new CellFormatChange({ fill: CellFill.solid(CellColour.fromRgb(rgb)), ...(bold === undefined ? {} : { bold }) }));
    };

    // ExSheet's own row, then a row of yours, each item greyed out while an edit is open.
    const review: ExSheetToolbarRow = {
        label: 'Review',
        keyTip: 'R',
        items: [
            // Content of your own is drawn in place of the text, which still names the button.
            { text: 'Mark reviewed', keyTip: 'M', onPress: () => { fill(0xE2EFDA); }, content: <><Tick /> Mark reviewed</> },
            { text: 'Flag for query', keyTip: 'Q', onPress: () => { fill(0xFCE4D6, true); } },
        ],
    };

    return (
        <MantineExSheet withBorder ref={sheet} document={opened} showToolbar={toolbar} onShowToolbarChanged={setToolbar}
            toolbar={['default', review]} viewportHeight={360} viewportWidth="stretch" />
    );
}
