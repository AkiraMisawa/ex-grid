import { useMemo, type ReactElement } from 'react';
import { CellAddress, Culture, SheetEdit, Workbook, WorkbookDocument } from '@ex-grid/sheet-engine';

// The engine on its own, over a Workbook of two Sheets, read into a table.
function computed(): { readonly workbook: Workbook; readonly recalculated: string } {
    const workbook = new Workbook(Culture.of('en-US'), ['Deposit', 'Rates']);
    const enter = (sheet: string, address: string, typed: string) => workbook.do(sheet, SheetEdit.enter(CellAddress.parse(address), typed));

    // Every edit names the Sheet it is made on.
    enter('Rates', 'A1', '4.25%');
    enter('Deposit', 'A1', '2500000');
    enter('Deposit', 'A2', '92');
    // A Reference qualified with a Sheet's name reads that Sheet's cells.
    enter('Deposit', 'A3', '=ROUND(A1*Rates!A1*A2/360, 2)');

    // A change on one Sheet recalculates what reads it on every other.
    const change = enter('Rates', 'A1', '4.5%').change;
    const recalculated = change.sheets.flatMap(sheet => change.for(sheet).valueChanges.map(address => `${sheet.name}!${address.toString()}`)).join(' and ');

    // Sheet operations are steps too: a rename rewrites every Formula that names the Sheet.
    workbook.do('Rates', SheetEdit.rename('Market'));
    workbook.do('Market', SheetEdit.copySheet());
    // A row inserted on one Sheet moves the References to it on every other.
    workbook.do('Market', SheetEdit.insertRows(0));

    // A Workbook Document carries every Sheet's Entries; opening one computes their Values again.
    const saved = workbook.toDocument().toJson();
    return { workbook: Workbook.open(WorkbookDocument.fromJson(saved)), recalculated };
}

export default function EngineWorkbook(): ReactElement {
    const { workbook, recalculated } = useMemo(computed, []);
    return (
        <>
            <table style={{ borderSpacing: '16px 4px', marginLeft: -16 }}>
                <thead>
                    <tr><th>Sheet</th><th>Cell</th><th>Entry</th><th>Value, as shown</th></tr>
                </thead>
                <tbody>
                    {workbook.sheets.flatMap(sheet => sheet.entryAddresses.map(address => (
                        <tr key={`${sheet.name}!${address.toString()}`}>
                            <td>{sheet.name}</td>
                            <td>{address.toString()}</td>
                            <td><code>{sheet.getEntryText(address)}</code></td>
                            <td>{sheet.getDisplay(address).text}</td>
                        </tr>
                    )))}
                </tbody>
            </table>
            <p>Changing the rate on Rates recalculated {recalculated}.</p>
        </>
    );
}
