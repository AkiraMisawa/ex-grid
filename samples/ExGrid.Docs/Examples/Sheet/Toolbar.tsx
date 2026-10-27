import { useMemo, useRef, useState, type ReactElement } from 'react';
import { CellColour, CellFill, CellFormatChange } from '@ex-grid/sheet-engine';
import { ExSheet, type ExSheetApi } from '@ex-grid/sheet-react';
import type { SheetToolbarRowOptions } from '@ex-grid/sheet';
import { quarterlyPnl } from './QuarterlyPnl';

export default function Toolbar(): ReactElement {
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
    const review: SheetToolbarRowOptions = {
        label: 'Review',
        keyTip: 'R',
        items: [
            { text: 'Mark reviewed', keyTip: 'M', onPress: () => { fill(0xE2EFDA); } },
            { text: 'Flag for query', keyTip: 'Q', onPress: () => { fill(0xFCE4D6, true); } },
        ],
    };

    return (
        <ExSheet ref={sheet} document={opened} showToolbar={toolbar} onShowToolbarChanged={setToolbar}
            toolbar={['default', review]} viewportHeight={360} viewportWidth="stretch" />
    );
}
