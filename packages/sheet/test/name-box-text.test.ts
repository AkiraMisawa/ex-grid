// The Name Box read as a place on the Sheet (ADR-0051, ADR-0050 item 4, ADR-0331 §5), ported from the address
// facts of dotnet/tests/ExSheet.Components/NameBoxWiringTests.cs.
import { describe, expect, it } from 'vitest';
import { readNameBoxPlace, readNameBoxRange } from '../src/index';

describe('Name Box', () => {
    it.each([
        ['d200', 199, 3, 1, 1],
        ['A1:C3', 0, 0, 3, 3],
        ['$C$3', 2, 2, 1, 1],
        [' B2 ', 1, 1, 1, 1],
        ['B:D', 0, 1, 1_048_576, 3],
        ['5:6', 4, 0, 2, 16_384],
        ['XFD1048576', 1_048_575, 16_383, 1, 1],
    ])('ADR-0050 item 4, ADR-0051: %j reads as Excel\'s Name Box reads it', (typed, top, left, rows, columns) => {
        expect.hasAssertions();
        const range = readNameBoxRange(typed);
        expect(range === undefined ? undefined : [range.first.row, range.first.column, range.rowCount, range.columnCount])
            .toEqual([top, left, rows, columns]);
    });

    it.each(['zz', 'A0', 'XFE1', 'A1048577', '', '5:', '05:6', 'A1:'])('ADR-0051, principle 1: %j is not an address', typed => {
        expect.hasAssertions();
        expect(readNameBoxRange(typed)).toBeUndefined();
    });

    it('ADR-0331 §5: a Sheet\'s name before the address names the Sheet, quoted with a doubled quote or bare', () => {
        expect.hasAssertions();
        const quoted = readNameBoxPlace("'Q1 ''Sales'''!A1:C3");
        expect([quoted?.sheet, quoted?.range.toString()]).toEqual(["Q1 'Sales'", 'A1:C3']);
        const bare = readNameBoxPlace('Sheet2!B5');
        expect([bare?.sheet, bare?.range.toString()]).toEqual(['Sheet2', 'B5']);
        const none = readNameBoxPlace('B5');
        expect([none?.sheet, none?.range.toString()]).toEqual([undefined, 'B5']);
        // The Sheet named is checked by the Sheet; an address that is not one is refused here.
        expect(readNameBoxPlace('Nowhere!A0')).toBeUndefined();
        expect(readNameBoxPlace('!A1')).toBeUndefined();
        expect(readNameBoxPlace("''!A1")).toBeUndefined();
        expect(readNameBoxPlace("'Q1!A1")).toBeUndefined();
    });
});
