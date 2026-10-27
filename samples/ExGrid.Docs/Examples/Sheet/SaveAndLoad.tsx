import { useMemo, useRef, useState, type ReactElement } from 'react';
import { Culture, Workbook, WorkbookDocument } from '@ex-grid/sheet-engine';
import { ExSheet } from '@ex-grid/sheet-react';
import { expenses } from './Expenses';

export default function SaveAndLoad(): ReactElement {
    const first = useMemo(expenses, []);
    // The document the Sheet opens: a different one replaces the Sheet and clears its undo stack, and none is an empty
    // Sheet in the locale given.
    const [opened, setOpened] = useState<WorkbookDocument | undefined>(first);
    // The document as it stands after each change, and the saved copy: where your application would write to a
    // database or a file.
    const current = useRef<WorkbookDocument | undefined>(first);
    const [saved, setSaved] = useState<string | undefined>(undefined);
    const [said, setSaid] = useState('');

    const save = (): void => {
        // A new Sheet not yet typed in has handed back no document: it is saved as the empty Sheet it is.
        const json = (current.current ?? new Workbook(Culture.of('en-US')).toDocument()).toJson();
        setSaved(json);
        setSaid(`Saved ${json.length.toLocaleString('en-US')} characters of JSON.`);
    };

    const load = (): void => {
        if (saved === undefined) return;
        const loaded = WorkbookDocument.fromJson(saved);
        current.current = loaded;
        setOpened(loaded);
        setSaid('Loaded the saved copy.');
    };

    const startNew = (): void => {
        current.current = undefined;
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
            <ExSheet document={opened} locale="en-US" onWorkbookChanged={document => { current.current = document; }}
                viewportHeight={280} viewportWidth="stretch" />
        </>
    );
}
