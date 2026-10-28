import { useMemo, useState, type ReactElement } from 'react';
import { CellAddress, SheetEdit, Workbook, type WorkbookDocument } from '@ex-grid/sheet-engine';
import { ExSheet } from '@ex-grid/sheet-react';
import { DemoNav } from '../demo-nav';
import { enUs } from '../sheet-cases';

// A claim, and a summary that reads it with References qualified by the claim's name, as WorkbookPage.razor builds them.
function build(): WorkbookDocument {
    const workbook = new Workbook(enUs, ['Claim', 'Summary']);
    const claim = [
        ['Item', 'Qty', 'Unit cost', 'Amount'],
        ['Flights', '2', '1240', '=B2*C2'],
        ['Hotel, nights', '6', '289.5', '=B3*C3'],
        ['Taxis', '9', '38.25', '=B4*C4'],
        ['Total', '', '', '=SUM(D2:D4)'],
    ];
    const summary = [
        ['Claimed', '=Claim!D5'],
        ['Travel', '=Claim!D2+Claim!D4'],
    ];
    const fill = (sheet: string, rows: readonly (readonly string[])[]): void => {
        rows.forEach((cells, row) => {
            cells.forEach((typed, column) => {
                if (typed.length > 0) workbook.do(sheet, SheetEdit.enter(new CellAddress(row, column), typed));
            });
        });
    };
    fill('Claim', claim);
    fill('Summary', summary);
    // Rows enough on Claim to scroll it, so a test can see each Sheet keep its own position.
    for (let row = 40; row < 200; row++) workbook.do('Claim', SheetEdit.enter(new CellAddress(row, 0), `Row ${String(row + 1)}`));
    return workbook.toDocument();
}

/** The page /workbook of the Blazor hosts, WorkbookPage.razor: one ExSheet holding a Workbook of two Sheets behind
 * Sheet Tabs (ADR-0331). */
export function WorkbookDemo(): ReactElement {
    const document = useMemo(build, []);
    const [shown, setShown] = useState(document.shown);
    return (
        <>
            <DemoNav route="workbook" />
            <h1>Workbook — Sheets behind Sheet Tabs</h1>
            <p>
                One ExSheet holding a Workbook of two Sheets (ADR-0331): a claim, and a summary that reads it with
                References qualified by the claim&apos;s name. A tab for each Sheet stands under the grid; a press on one,
                or Ctrl+PageUp and Ctrl+PageDown, shows its Sheet, and each Sheet keeps its own Selection and scroll
                position.
            </p>
            <div id="workbook">
                <ExSheet document={document} viewportHeight={360} viewportWidth={640} onWorkbookChanged={changed => { setShown(changed.shown); }} />
            </div>
            <p>Shown: <span id="workbook-shown">{shown}</span></p>
        </>
    );
}
