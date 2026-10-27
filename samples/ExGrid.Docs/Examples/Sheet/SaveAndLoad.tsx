import { useMemo, useRef, useState, type ReactElement } from 'react';
import { WorkbookDocument } from '@ex-grid/sheet-engine';
import { ExSheet, type ExSheetApi } from '@ex-grid/sheet-react';
import { expenses } from './Expenses';

export default function SaveAndLoad(): ReactElement {
    const first = useMemo(expenses, []);
    // The document the Sheet opens: a different one replaces the Sheet and clears its undo stack, and none is an empty
    // Sheet in the locale given.
    const [opened, setOpened] = useState<WorkbookDocument | undefined>(first);
    const sheet = useRef<ExSheetApi>(null);
    // The saved copy: where your application would write to a database or a file.
    const [saved, setSaved] = useState<string | undefined>(undefined);
    const [said, setSaid] = useState('');

    const save = (): void => {
        // The Workbook as the Sheet holds it now, a new Sheet not yet typed in too.
        const api = sheet.current;
        if (api === null) throw new Error('The Sheet is not mounted.');
        const json = api.workbook.toDocument().toJson();
        setSaved(json);
        setSaid(`Saved ${json.length.toLocaleString('en-US')} characters of JSON.`);
    };

    const load = (): void => {
        if (saved === undefined) return;
        setOpened(WorkbookDocument.fromJson(saved));
        setSaid('Loaded the saved copy.');
    };

    const startNew = (): void => {
        setOpened(undefined);
        setSaid('');
    };

    return (
        <>
            <div className="bar">
                <button type="button" onClick={save}>Save</button>
                <button type="button" disabled={saved === undefined} onClick={load}>Load the saved copy</button>
                <button type="button" onClick={startNew}>New sheet</button>
                <span>{said}</span>
            </div>
            <ExSheet ref={sheet} document={opened} locale="en-US" viewportHeight={280} viewportWidth="stretch" />
        </>
    );
}
