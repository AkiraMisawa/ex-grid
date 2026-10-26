// The functions of portable-math.ts held to the exact value rounded once, at random arguments over each
// function's range and at its edges (tests/cases/sheet/portable-math.json, from mpmath at 400 bits), so that the
// double every platform answers is the right one and not only the same one. xUnit holds the C# engine's
// PortableMath.cs to the same table (PortableMathCaseTests), and both engines meet the arguments where glibc and V8
// round the other way in tests/cases/sheet/differential-findings.json.
import fs from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import * as portable from '../src/formulas/portable-math';
import { root } from './sheet-case';

const table = JSON.parse(fs.readFileSync(path.join(root, 'portable-math.json'), 'utf8')) as { cases: [string, string[], string][] };

const functions: Record<string, (...args: number[]) => number> = {
    atan: portable.atan,
    asin: portable.asin,
    acos: portable.acos,
    atan2: portable.atan2,
    sinh: portable.sinh,
    cosh: portable.cosh,
    tanh: portable.tanh,
    asinh: portable.asinh,
    acosh: portable.acosh,
    atanh: portable.atanh,
    log10: portable.log10,
    powWhole: portable.powWhole,
};

function fromBits(hex: string): number {
    const view = new DataView(new ArrayBuffer(8));
    view.setBigUint64(0, BigInt(`0x${hex}`));
    return view.getFloat64(0);
}

describe('portable math', () => {
    // ADR-0047, ADR-0370: the same double on every platform, and the correctly rounded one.
    it('answers each function as its exact value rounded once', () => {
        const wrong: string[] = [];
        for (const [name, args, expected] of table.cases) {
            const fn = functions[name];
            if (fn === undefined) throw new Error(`No function ${name}.`);
            const given = args.map(fromBits);
            const answer = fn(...given);
            if (!Object.is(answer, fromBits(expected))) wrong.push(`${name}(${given.join(', ')}) is ${String(answer)}, not ${String(fromBits(expected))}`);
        }
        expect(wrong).toEqual([]);
    });
});
