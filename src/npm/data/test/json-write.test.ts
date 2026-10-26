// JSON written as System.Text.Json writes it (ADR-0340 Q7, ADR-0370 section 3): what a Board Diff's writer stands on.
// The texts are .NET's own: `JsonValue.Create(d).ToJsonString()` and a JsonNode read and written again.
import { describe, expect, it } from 'vitest';
import { jsonDoubleText, jsonStringText, readJsonText, writeJsonText } from '../src/index';

describe('JSON written as .NET writes it', () => {
    it('writes a double as its shortest round trip in .NET\'s general layout', () => {
        expect([0, -0, 1, 0.1, 739908, 1e15, 1e16, 1e17, 1.5e17, 1e21, -1.5e-7, 0.0001, 0.00001, 123456789012345680000]
            .map(jsonDoubleText)).toEqual(['0', '-0', '1', '0.1', '739908', '1000000000000000', '10000000000000000', '1E+17', '1.5E+17',
            '1E+21', '-1.5E-07', '0.0001', '1E-05', '1.2345678901234568E+20']);
        expect(() => jsonDoubleText(Number.NaN)).toThrow(RangeError);
    });

    it('escapes what the default encoder escapes, and refuses a lone surrogate', () => {
        expect(jsonStringText('P&L <\'x\'> + `é` "q" \\ \n')).toBe('"P\\u0026L \\u003C\\u0027x\\u0027\\u003E \\u002B \\u0060\\u00E9\\u0060 \\u0022q\\u0022 \\\\ \\n"');
        expect(() => jsonStringText('\uD800')).toThrow(TypeError);
    });

    it('writes a value read again with no space, members in order and numbers as spelt', () => {
        expect(writeJsonText(readJsonText('{ "b": [1.50, true, null], "a": "x&y" }'))).toBe('{"b":[1.50,true,null],"a":"x\\u0026y"}');
    });
});
