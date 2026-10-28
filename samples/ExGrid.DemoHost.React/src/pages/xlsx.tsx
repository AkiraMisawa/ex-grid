import { useEffect, useRef, useState, type ChangeEvent, type ReactElement } from 'react';
import { CellAddress, SheetDocumentError, SheetEdit, SheetRefusedError, Workbook, type WorkbookDocument } from '@ex-grid/sheet-engine';
import { ExSheet, type ExSheetApi } from '@ex-grid/sheet-react';
import { openXlsxWorkbook, saveXlsxWorkbook, XlsxOpenError, XlsxSaveError, type OpenReport } from '@ex-grid/sheet-xlsx';
import { DemoNav } from '../demo-nav';
import { useQuery } from '../router';
import { enUs } from '../sheet-cases';

const culture = 'en-US';

// The longest address Chromium takes, which a data: link is.
const maxLink = 2 * 1024 * 1024;

function before(): WorkbookDocument {
    const workbook = new Workbook(enUs, ['Before']);
    workbook.do('Before', SheetEdit.enter(CellAddress.parse('A1'), 'Before the file'));
    return workbook.toDocument();
}

/** A written file as a data: link, which layer 3 reads the bytes back from, as XlsxPage.razor's links are. */
async function linkOf(file: Blob): Promise<string> {
    const bytes = new Uint8Array(await file.arrayBuffer());
    let binary = '';
    for (const byte of bytes) binary += String.fromCharCode(byte);
    return `data:${file.type};base64,${btoa(binary)}`;
}

// Two Sheets, so a key that shows the next one acts only on the Workbook the file opens.
async function writtenSample(): Promise<string> {
    const workbook = new Workbook(enUs, ['One', 'Two']);
    workbook.do('One', SheetEdit.enter(CellAddress.parse('A1'), '21'));
    workbook.do('Two', SheetEdit.enter(CellAddress.parse('A1'), '=One!A1*2'));
    return linkOf(await saveXlsxWorkbook(workbook));
}

const reportLines = (report: OpenReport): string[] => [
    ...report.notCarried.map(item => item.description),
    ...report.unsupportedFunctions.map(cell => `${cell.sheet}!${cell.cell}: ${cell.functions.join(', ')}`),
];

/** The page /xlsx of the Blazor hosts, XlsxPage.razor: one ExSheet opening a workbook from an .xlsx and saving it to
 * one at its place in the Input Order (ADR-0330), with ?hold=1 holding each read until "Let the read go" is pressed. */
export function XlsxDemo(): ReactElement {
    const hold = useQuery('hold') === '1';
    const [document] = useState(before);
    const sheet = useRef<ExSheetApi>(null);
    const [said, setSaid] = useState('Choose a workbook, or save this one.');
    const [report, setReport] = useState<readonly string[]>([]);
    const [saved, setSaved] = useState<string | undefined>(undefined);
    const [sample, setSample] = useState<string | undefined>(undefined);
    const [held, setHeld] = useState<(() => void) | undefined>(undefined);
    useEffect(() => {
        let mounted = true;
        void writtenSample().then(link => { if (mounted) setSample(link); });
        return () => { mounted = false; };
    }, []);

    const api = (): ExSheetApi => {
        const current = sheet.current;
        if (current === null) throw new Error('The Sheet is not mounted.');
        return current;
    };

    const open = async (event: ChangeEvent<HTMLInputElement>): Promise<void> => {
        const file = event.target.files?.[0];
        event.target.value = '';
        if (file === undefined) return;
        const read: { report?: OpenReport } = {};
        try {
            const opened = await api().open(async signal => {
                if (hold) {
                    await new Promise<void>((resolve, reject) => {
                        const release = (): void => { setHeld(undefined); resolve(); };
                        signal.addEventListener('abort', () => { setHeld(undefined); reject(signal.reason); }, { once: true });
                        setHeld(() => release);
                    });
                }
                const result = await openXlsxWorkbook(file, culture, { signal });
                read.report = result.report;
                return result.document;
            });
            setSaid(opened ? `Opened ${file.name}.` : `${file.name} was not opened: the opening was cancelled.`);
            setReport(opened && read.report !== undefined ? reportLines(read.report) : []);
        } catch (refused) {
            // A refusal by name, a document the engine does not read, an edit open.
            if (!(refused instanceof XlsxOpenError || refused instanceof SheetDocumentError || refused instanceof SheetRefusedError)) throw refused;
            setSaid(`${file.name} was refused: ${refused.message}`);
            setReport([]);
        }
    };

    const save = async (): Promise<void> => {
        try {
            const written: { file?: Blob } = {};
            await api().save(async workbook => { written.file = await saveXlsxWorkbook(workbook); });
            const { file } = written;
            if (file === undefined) throw new Error('The save wrote no file.');
            const link = await linkOf(file);
            const bytes = file.size.toLocaleString('en-US');
            // The link carries the file in its address, which Chromium caps at 2 MB: refused by name, not cut short.
            setSaved(link.length <= maxLink ? link : undefined);
            setSaid(link.length <= maxLink
                ? `Saved ${bytes} bytes.`
                : `Saved ${bytes} bytes, too many for this page's download link: a browser takes at most ${maxLink.toLocaleString('en-US')} characters in its address.`);
        } catch (refused) {
            if (!(refused instanceof XlsxSaveError || refused instanceof SheetRefusedError)) throw refused;
            setSaved(undefined);
            setSaid(`Not saved: ${refused.message}`);
        }
    };

    return (
        <>
            <DemoNav route="xlsx" />
            <h1>Open and save .xlsx</h1>
            <p>
                One ExSheet opening a workbook from an <code>.xlsx</code> and saving it to one (ADR-0330), through
                <code> @ex-grid/sheet-xlsx</code>. Each takes its place in the Input Order: what is typed while the file is
                read waits, and lands on the Workbook it opened; Escape cancels the opening, and the Workbook stays as it
                was. The page chooses the file and delivers the written one: ExSheet references no file format.
            </p>
            <div className="demo-toolbar">
                <label>Open a workbook <input id="xlsx-file" type="file" accept=".xlsx" onChange={event => { void open(event); }} /></label>
                <button id="xlsx-save" type="button" onClick={() => { void save(); }}>Save</button>
                {saved === undefined ? null : <a id="xlsx-saved" download="workbook.xlsx" href={saved}>Download the saved workbook</a>}
                {sample === undefined ? null : <a id="xlsx-sample" download="sample.xlsx" href={sample}>A sample workbook of two Sheets</a>}
                {held === undefined ? null : <button id="xlsx-release" type="button" onClick={held}>Let the read go</button>}
            </div>
            <p id="xlsx-said" role="status">{said}</p>
            {report.length > 0 ? <ul id="xlsx-report">{report.map(line => <li key={line}>{line}</li>)}</ul> : null}
            <div id="xlsx">
                <ExSheet ref={sheet} document={document} locale={culture} viewportHeight={360} viewportWidth={640} />
            </div>
        </>
    );
}
