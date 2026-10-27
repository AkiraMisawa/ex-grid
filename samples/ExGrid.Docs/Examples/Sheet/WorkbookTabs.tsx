import { useMemo, useRef, useState, type ReactElement } from 'react';
import { SheetEdit, WorkbookDocument } from '@ex-grid/sheet-engine';
import { ExSheet, type ExSheetApi } from '@ex-grid/sheet-react';
import type { SheetsChange } from '@ex-grid/sheet';
import { expensesWorkbook } from './Expenses';

export default function WorkbookTabs(): ReactElement {
    // The claim, and a summary that reads it with References such as Claim!D7.
    const first = useMemo(expensesWorkbook, []);
    const sheet = useRef<ExSheetApi>(null);
    // The document the Sheet opens: a new one replaces the Workbook and clears its undo stack.
    const [opened, setOpened] = useState(first);
    // The document as it stands after each change, and the saved copy: where your application would write to a
    // database or a file.
    const current = useRef(first);
    const [saved, setSaved] = useState<string | undefined>(undefined);
    const [said, setSaid] = useState('');

    // A command names the Sheet it acts on: with several Sheets, one naming none is refused. A Sheet operation is one
    // step on the undo stack, as an edit is.
    const addSheet = (): void => {
        const api = sheet.current;
        if (api === null) throw new Error('The Sheet is not mounted.');
        const sheets = api.workbook.sheets;
        const last = sheets[sheets.length - 1];
        if (last === undefined) throw new Error('A Workbook holds at least one Sheet.');
        api.do(last.name, SheetEdit.insertSheet());
    };

    // Every change to the Sheets, the user's, a command's or an undo's, is heard with the names before and after.
    const sheetsChanged = (change: SheetsChange): void => { setSaid(`Sheets: ${change.after.join(', ')}.`); };

    const save = (): void => {
        setSaved(current.current.toJson());
        setSaid(`Saved ${String(current.current.sheets.length)} sheets, '${current.current.shown}' shown.`);
    };

    const load = (): void => {
        if (saved === undefined) return;
        const loaded = WorkbookDocument.fromJson(saved);
        current.current = loaded;
        setOpened(loaded);
        setSaid('Loaded the saved copy.');
    };

    return (
        <>
            <div className="bar">
                <button type="button" onClick={addSheet}>Add a sheet</button>
                <button type="button" onClick={save}>Save</button>
                <button type="button" disabled={saved === undefined} onClick={load}>Load the saved copy</button>
                <span>{said}</span>
            </div>
            <ExSheet ref={sheet} document={opened} onWorkbookChanged={document => { current.current = document; }} onSheetsChanged={sheetsChanged}
                viewportHeight={280} viewportWidth="stretch" />
        </>
    );
}
