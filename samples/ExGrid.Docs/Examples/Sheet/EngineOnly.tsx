import { useMemo, type ReactElement } from 'react';
import { CellAddress, Culture, SheetDocument, SheetEdit, Workbook, type Sheet } from '@ex-grid/sheet-engine';

// The engine on its own, with no Sheet drawn: what it holds, read into a table.
function computed(): { readonly sheet: Sheet; readonly recalculated: string } {
    const workbook = new Workbook(Culture.of('en-US'));
    const name = 'Sheet1';
    const enter = (address: string, typed: string) => workbook.do(name, SheetEdit.enter(CellAddress.parse(address), typed));

    // Interest on a deposit for 92 days, on an Actual/360 basis.
    enter('A1', '2500000');
    enter('A2', '4.25%');
    enter('A3', '92');
    enter('A4', '=ROUND(A1*A2*A3/360, 2)');
    enter('A5', '=MEDIAN(A1:A3)');

    // Only what the change reaches is recalculated.
    const step = enter('A2', '4.5%');
    const recalculated = step.change.for(step.sheet).valueChanges.map(String).join(' and ');

    // A Sheet Document carries Entries; opening one computes its Values again.
    const saved = workbook.toSheetDocument(name).toJson();
    const reopened = Workbook.open(SheetDocument.fromJson(saved)).sheets[0];
    if (reopened === undefined) throw new Error('A Workbook holds at least one Sheet.');
    return { sheet: reopened, recalculated };
}

export default function EngineOnly(): ReactElement {
    const { sheet, recalculated } = useMemo(computed, []);
    const addresses = [...sheet.entryAddresses].sort((a, b) => a.row - b.row || a.column - b.column);
    return (
        <>
            <table style={{ borderSpacing: '16px 4px', marginLeft: -16 }}>
                <thead>
                    <tr><th>Cell</th><th>Entry</th><th>Value, as shown</th></tr>
                </thead>
                <tbody>
                    {addresses.map(address => (
                        <tr key={address.toString()}>
                            <td>{address.toString()}</td>
                            <td><code>{sheet.getEntryText(address)}</code></td>
                            <td>{sheet.getDisplay(address).text}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
            <p>Changing the rate to 4.5% recalculated {recalculated}.</p>
        </>
    );
}
