import { useEffect, useMemo, useRef, useState, type ChangeEvent, type ReactElement } from 'react';
import { SheetRefusedError, type WorkbookDocument } from '@ex-grid/sheet-engine';
import { ExSheet, type ExSheetApi } from '@ex-grid/sheet-react';
import { OpenReport, XlsxOpenError, XlsxSaveError, openXlsxWorkbook, saveXlsxWorkbook } from '@ex-grid/sheet-xlsx';
import { expenses } from './Expenses';

// The culture typing is read in from here on, and Values are shown in: yours to declare, never the file's.
const culture = 'en-US';

// What the file held that was not carried, and the cells naming functions ExSheet does not support.
function reportLines(report: OpenReport): string[] {
    return [
        ...report.notCarried.map(item => item.description),
        ...report.unsupportedFunctions.map(cell => `${cell.sheet}!${cell.cell} names ${cell.functions.join(', ')}, and shows #NAME?.`),
    ];
}

export default function XlsxOpenSave(): ReactElement {
    const first = useMemo(expenses, []);
    const [document, setDocument] = useState<WorkbookDocument | undefined>(first);
    const sheet = useRef<ExSheetApi>(null);
    const [said, setSaid] = useState('');
    const [report, setReport] = useState<string[]>([]);
    const [saved, setSaved] = useState<string | undefined>(undefined);
    // The download link's address is the browser's to free once it is replaced.
    useEffect(() => () => { if (saved !== undefined) URL.revokeObjectURL(saved); }, [saved]);

    // The read is handed over still to do, and takes its place in the Sheet's order now: what is typed while the file
    // is read waits, and lands on the Workbook it opens. Escape cancels it while the Sheet says so.
    const open = async (event: ChangeEvent<HTMLInputElement>): Promise<void> => {
        const api = sheet.current;
        const file = event.target.files?.[0];
        event.target.value = '';
        if (api === null || file === undefined) return;
        const read: { report?: OpenReport } = {};
        try {
            const opened = await api.open(async signal => {
                const result = await openXlsxWorkbook(file, culture, { signal });
                read.report = result.report;
                return result.document;
            });
            setSaid(opened ? `Opened ${file.name}.` : 'The opening was cancelled; the Workbook is as it was.');
            setReport(opened && read.report !== undefined ? reportLines(read.report) : []);
        } catch (refused) {
            // Nothing opened: a refusal names the Sheet, the cell and what it held; a SheetRefusedError is a cell being
            // edited.
            if (!(refused instanceof XlsxOpenError || refused instanceof SheetRefusedError)) throw refused;
            setSaid(refused.message);
            setReport([]);
        }
    };

    // Written from the Workbook as the inputs before the save left it, each Formula with its Value. A browser
    // application hands the bytes to a link; a server would write them to a response or a store.
    const save = async (): Promise<void> => {
        const api = sheet.current;
        if (api === null) return;
        try {
            const written: { file?: Blob } = {};
            await api.save(async workbook => { written.file = await saveXlsxWorkbook(workbook); });
            const { file } = written;
            if (file === undefined) throw new Error('The save wrote no file.');
            setSaved(URL.createObjectURL(file));
            setSaid(`Saved ${file.size.toLocaleString('en-US')} bytes.`);
        } catch (refused) {
            // A cell being edited, or something a file cannot hold, named.
            if (!(refused instanceof XlsxSaveError || refused instanceof SheetRefusedError)) throw refused;
            setSaved(undefined);
            setSaid(refused.message);
        }
    };

    return (
        <>
            <div className="bar">
                <label>Open an .xlsx <input type="file" accept=".xlsx" onChange={event => { void open(event); }} /></label>
                <button type="button" onClick={() => { void save(); }}>Save as .xlsx</button>
                {saved !== undefined && <a href={saved} download="workbook.xlsx">Download workbook.xlsx</a>}
                <span>{said}</span>
            </div>
            {report.length > 0 && (
                <>
                    <p>Not carried from the file, and lost if it is saved again:</p>
                    <ul>{report.map(line => <li key={line}>{line}</li>)}</ul>
                </>
            )}
            <ExSheet ref={sheet} document={document} onWorkbookChanged={setDocument} locale={culture} viewportHeight={280} viewportWidth="stretch" />
        </>
    );
}
