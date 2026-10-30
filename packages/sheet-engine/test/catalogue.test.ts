// The function catalogue (docs/specs/exsheet-functions/spec.md, ADR-0047) read against the Script Core, as
// FunctionCatalogueTests.cs and LetTests.cs read it against ExSheet.Engine: its Supported rows are the declared set,
// the rest of Excel's functions are the names whose call is refused, and each one that is a name in shape is a LET
// name (ADR-0181). The catalogue's own form, a row per function and its list in order, is held on the C# side.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { CellAddress } from '../src/cell-address';
import { Culture } from '../src/culture';
import { DeclaredFunction } from '../src/declared-function';
import { excelFunctionNames, isUndeclaredExcelFunction } from '../src/formulas/excel-function-names';
import { Workbook } from '../src/workbook';

const catalogue = fs.readFileSync(
    path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../docs/specs/exsheet-functions/spec.md'), 'utf8');

/** The catalogue's rows, each a function's name and its status. */
const rows = [...catalogue.matchAll(/^\| `(?<name>[A-Z][A-Z0-9.]*)` \| (?<status>[A-Za-z]+) \| (?:P[123]|—) \|/gmu)].map(m => {
    const { name, status } = m.groups as { name: string; status: string };
    return { name, status };
});

/** The names of the catalogue's list of Excel's functions that no row queues, which C# reads line by line and checks the form of. */
function notQueued(): string[] {
    const lines = catalogue.replaceAll('\r\n', '\n').split('\n');
    const start = lines.indexOf("## Excel's functions not in the queue");
    if (start < 0) throw new Error("The catalogue has no list of Excel's functions not in the queue.");
    const end = lines.findIndex((line, i) => i > start && line.startsWith('## '));
    // A letter's line, `- **A**: `ACCRINT`, ...`, and the lines it wraps onto, indented by two spaces; not the prose between.
    const names: string[] = [];
    let inLetter = false;
    for (const line of lines.slice(start + 1, end < 0 ? undefined : end)) {
        const letter = /^- \*\*[A-Z]\*\*: (?<names>.+)$/u.exec(line);
        inLetter = letter !== null || (inLetter && line.startsWith('  `'));
        if (!inLetter) continue;
        // A wrapped line is names only.
        const listed = letter?.groups?.['names'] ?? line;
        names.push(...[...listed.matchAll(/`(?<name>[A-Z][A-Z0-9.]*)`/gu)].map(m => (m.groups as { name: string }).name));
    }
    return names;
}

const ordinal = (a: string, b: string): number => (a < b ? -1 : a > b ? 1 : 0);

describe('the function catalogue', () => {
    // ADR-0047: the catalogue's Supported rows are exactly the declared set.
    it('has the declared set as its Supported rows', () => {
        expect(rows.length).toBeGreaterThan(0);
        expect(DeclaredFunction.all.map(f => f.name).toSorted(ordinal))
            .toEqual(rows.filter(r => r.status === 'Supported').map(r => r.name).toSorted(ordinal));
    });

    // ADR-0047 (2026-10-13, decided with the user): the functions Excel has and ExSheet does not declare, whose call is
    // a Refused Formula, are exactly the catalogue's rows that are not Supported and its list of the rest.
    it("has the engine's list of Excel's undeclared functions as its rest", () => {
        const rest = [...rows.filter(r => r.status !== 'Supported').map(r => r.name), ...notQueued()];
        expect(excelFunctionNames.toSorted(ordinal)).toEqual(rest.toSorted(ordinal));
        expect(DeclaredFunction.all.filter(f => isUndeclaredExcelFunction(f.name)).map(f => f.name)).toEqual([]);
    });

    // ADR-0181 (run 17), SH-67: every one of Excel's function names that is a name in shape, and no Reference or
    // boolean, is a LET name, declared by the engine or not, as the keyboard took SUM, IRR and N (LET-101, LET-111, LET-112).
    it('binds every Excel function name that is a name as a LET name', () => {
        const names = [...rows.map(r => r.name), ...notQueued()]
            .filter(name => !name.includes('.') && name !== 'TRUE' && name !== 'FALSE' && CellAddress.tryParse(name) === undefined);
        expect(names).toEqual(expect.arrayContaining(['SUM', 'IRR', 'N', 'LET']));
        const sheet = new Workbook(Culture.of('en-US')).shown;
        const at = CellAddress.parse('ZZ1000');
        const unbound = names.filter(name => {
            sheet.enter(at, `=LET(${name},1,${name}+1)`);
            return sheet.getValue(at)?.number !== 2;
        });
        expect(unbound).toEqual([]);
    });
});
