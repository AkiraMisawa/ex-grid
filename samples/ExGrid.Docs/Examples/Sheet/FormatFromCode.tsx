import { useMemo, useRef, useState, type ReactElement } from 'react';
import {
    BorderChange, BorderLine, CellAddress, CellColour, CellFill, CellFormatChange, NumberFormat, type CellFormatChangeParts,
} from '@ex-grid/sheet-engine';
import type { GridSelection } from '@ex-grid/core';
import { ExSheet, type ExSheetApi } from '@ex-grid/sheet-react';
import { quarterlyPnl } from './QuarterlyPnl';

const thousands = '#,##0;(#,##0)';
const percent = '0.0%';

export default function FormatFromCode(): ReactElement {
    const opened = useMemo(quarterlyPnl, []);
    const sheet = useRef<ExSheetApi>(null);
    const selection = useRef<GridSelection | undefined>(undefined);
    const [editing, setEditing] = useState(false);
    const [focus, setFocus] = useState('');

    // What the Focus cell shows: from the cell, its row or its column. The Focus is on the Sheet shown, which the read
    // names: with several Sheets, a read naming none is refused.
    const readFocus = (): void => {
        const at = selection.current?.focus;
        const api = sheet.current;
        if (at === undefined || api === null) return;
        const format = api.cellFormatAt(api.workbook.shown.name, new CellAddress(at.row, at.column));
        setFocus(`${format.numberFormat.code}${format.font.bold ? ', bold' : ''}${format.fill.isNone ? '' : ', filled'}`);
    };

    const mounted = (): ExSheetApi => {
        const api = sheet.current;
        if (api === null) throw new Error('The Sheet is not mounted.');
        return api;
    };

    // Each press sets only the parts it names, on every range of the Selection, as one undo step.
    const set = (parts: CellFormatChangeParts): void => { mounted().setCellFormat(new CellFormatChange(parts)); };

    return (
        <>
            <div className="bar">
                <button type="button" disabled={editing} onClick={() => { set({ bold: true }); }}>Bold</button>
                <button type="button" disabled={editing} onClick={() => { set({ numberFormat: NumberFormat.parse(thousands) }); }}>Thousands</button>
                <button type="button" disabled={editing} onClick={() => { set({ numberFormat: NumberFormat.parse(percent) }); }}>Percent</button>
                <button type="button" disabled={editing} onClick={() => { set({ fill: CellFill.solid(CellColour.fromRgb(0xFFF2CC)) }); }}>Highlight</button>
                <button type="button" disabled={editing} onClick={() => { set({ borders: BorderChange.outline(new BorderLine('medium')) }); }}>Outline</button>
                <button type="button" disabled={editing} onClick={() => { mounted().openFormatCells(); }}>Format Cells…</button>
            </div>
            <ExSheet ref={sheet} document={opened} onWorkbookChanged={readFocus} onEditingChanged={setEditing}
                onSelectionChanged={moved => { selection.current = moved; readFocus(); }} viewportHeight={260} viewportWidth="stretch" />
            <p>Focus cell: {focus}</p>
        </>
    );
}
