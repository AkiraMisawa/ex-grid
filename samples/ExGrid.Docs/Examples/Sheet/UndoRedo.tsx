import { useMemo, useRef, useState, type ReactElement } from 'react';
import type { SheetHistoryState } from '@ex-grid/sheet';
import { SheetEdit, SheetRefusedError } from '@ex-grid/sheet-engine';
import { ExSheet, type ExSheetApi } from '@ex-grid/sheet-react';
import { expenses } from './Expenses';

export default function UndoRedo(): ReactElement {
    const opened = useMemo(expenses, []);
    const sheet = useRef<ExSheetApi>(null);
    // Whether there is a step to undo and one to redo, heard each time either moves.
    const [history, setHistory] = useState<SheetHistoryState>({ canUndo: false, canRedo: false });
    const [editing, setEditing] = useState(false);
    const [refused, setRefused] = useState('');

    // Your commands share the user's undo stack, and are refused while a cell is being edited.
    const run = (command: (api: ExSheetApi) => void): void => {
        const api = sheet.current;
        if (api === null) throw new Error('The Sheet is not mounted.');
        try {
            command(api);
            setRefused('');
        } catch (error) {
            if (!(error instanceof SheetRefusedError)) throw error;
            setRefused(error.message);
        }
    };

    return (
        <>
            <div className="bar">
                <button type="button" disabled={editing || !history.canUndo} onClick={() => { run(api => { api.undo(); }); }}>Undo</button>
                <button type="button" disabled={editing || !history.canRedo} onClick={() => { run(api => { api.redo(); }); }}>Redo</button>
                <button type="button" disabled={editing} onClick={() => { run(api => { api.do(SheetEdit.insertRows(1)); }); }}>Insert a row above row 2</button>
                <span>{refused}</span>
            </div>
            <ExSheet ref={sheet} document={opened} onHistoryChanged={setHistory} onEditingChanged={setEditing}
                viewportHeight={280} viewportWidth="stretch" />
        </>
    );
}
