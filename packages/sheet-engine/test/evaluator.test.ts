// A first check that the ported evaluator and function library answer, over a Sheet of no cells. The engine's
// facts are held by the shared cases (tests/cases) and the Excel corpus once the Sheet is ported; this file only
// keeps `npm test` honest until then.
import { describe, expect, it } from 'vitest';
import { CellAddress } from '../src/cell-address';
import { Culture } from '../src/culture';
import type { CellReader } from '../src/formulas/cell-reader';
import { Evaluator } from '../src/formulas/evaluator';
import { allFunctions } from '../src/formulas/function-library';
import { Operand } from '../src/formulas/operand';
import { parseFormula } from '../src/formulas/parser';

const noCells: CellReader = {
    read: () => undefined,
    nonBlankIn: () => [],
    tableColumn: () => Operand.ofError('#NAME?'),
    isLinkedTable: () => false,
    today: undefined,
    now: undefined,
    resolve: name => (name === undefined ? noCells : undefined),
    resolveSpan: () => undefined,
    draw: () => undefined,
    spill: () => Operand.ofError('#REF!'),
    formulaAt: () => undefined,
};

function evaluate(formula: string): string {
    const evaluator = new Evaluator(noCells, Culture.of('en-US'));
    evaluator.self = new CellAddress(0, 0);
    return evaluator.evaluate(parseFormula(formula)).toString();
}

describe('evaluator', () => {
    it.each([
        ['=1+2', '3'],
        ['=SUM(0.1,0.2,-0.3)', '0'],
        ['=ROUND(2.5,0)', '3'],
        ['=PMT(0.05/12,360,200000)', '-1073.643246024278'],
        ['=SIN(PI())', '1.22514845490862E-16'],
        ['=TEXTJOIN("-",TRUE,"a","","b")', 'a-b'],
        ['=COUNTIF(A1:A3,"")', '3'],
        ['=SUBTOTAL(9,A1:A3)', '0'],
        ['=1/0', '#DIV/0!'],
    ])('%s is %s', (formula, expected) => {
        expect(evaluate(formula)).toBe(expected);
    });

    it('declares the functions the C# engine declares', () => {
        expect(allFunctions()).toHaveLength(148);
    });
});
