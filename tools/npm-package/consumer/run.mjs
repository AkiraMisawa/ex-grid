// Run by check.mjs in the Consumer it installs from the packed tarballs: the packages that compute, taken on Node
// from what was packed alone. A Snapshot goes through Apache Arrow and comes back the same (DA-16, as the NuGet
// smoke check does), and a Workbook answers a Formula. A package that imports a module it did not pack, or whose
// entry point names a file that is not in it, fails here before a Consumer meets it.
import { createSnapshot, readArrowSnapshot, writeArrowSnapshot } from '@ex-grid/data';
import { CellAddress, Culture, SheetEdit, Workbook } from '@ex-grid/sheet-engine';

const fail = message => {
    console.error(`npm package check: ${message}`);
    process.exit(1);
};

const snapshot = createSnapshot({
    version: 7,
    recordKey: 'Id',
    columns: [
        { name: 'Id', kind: 'integer', values: [1, 2, 3] },
        { name: 'Desk', kind: 'text', values: ['FX', null, 'Rates'] },
    ],
});
const back = await readArrowSnapshot(writeArrowSnapshot(snapshot));
if (back.version !== 7n || back.recordKey !== 'Id' || back.rowCount !== 3) {
    fail(`the Snapshot came back from Arrow as version ${String(back.version)}, key ${String(back.recordKey)}, ${back.rowCount} rows.`);
}
const desk = back.column('Desk');
if (!desk.isBlank(1) || desk.kind !== 'text' || desk.dictionary.join() !== 'FX,Rates') {
    fail('the Snapshot\'s Desk column came back from Arrow changed.');
}

const workbook = new Workbook(Culture.of('en-US'), ['Sheet1']);
workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse('A1'), '2'));
workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse('A2'), '=A1*21'));
const sheet = workbook.find('Sheet1');
const value = sheet === undefined ? undefined : sheet.getValue(CellAddress.parse('A2'));
if (value === undefined || value.kind !== 'number' || value.number !== 42) {
    fail(`=A1*21 over 2 came out ${value === undefined ? 'blank' : value.toString()}.`);
}

console.log('npm package check: the packed data and sheet engine packages ran on Node.');
